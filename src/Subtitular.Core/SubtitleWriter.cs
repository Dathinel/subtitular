using System.Globalization;
using System.Text;

namespace Subtitular.Core;

/// <summary>
/// Representa un bloque de subtítulo con tiempos de inicio y fin y hasta 2 líneas de texto.
/// </summary>
public sealed record SubtitleCue(
    int Index,
    TimeSpan Start,
    TimeSpan End,
    IReadOnlyList<string> Lines);

/// <summary>
/// Escritor de subtítulos en formatos SRT y WebVTT.
/// Cumple con cortes de línea a 42 caracteres como máximo y 2 líneas por bloque de subtítulo.
/// </summary>
public static class SubtitleWriter
{
    public const int MaxLineLength = 42;
    public const int MaxLinesPerCue = 2;

    /// <summary>
    /// Construye la lista de bloques de subtítulo respetando los límites de 42 caracteres por línea
    /// y 2 líneas como máximo por bloque.
    /// </summary>
    public static IReadOnlyList<SubtitleCue> BuildCues(Transcript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        List<SubtitleCue> cues = [];
        int cueIndex = 1;
        TimeSpan lastCueEnd = TimeSpan.Zero;

        foreach (TranscriptSegment segment in transcript.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                continue;
            }

            IReadOnlyList<TranscriptWord> words = segment.Words is { Count: > 0 } w
                ? ExpandLongWords(w)
                : SynthesizeWords(segment);

            if (words.Count == 0)
            {
                continue;
            }

            int curr = 0;
            while (curr < words.Count)
            {
                // Hallar la mayor cantidad de palabras consecutivas que caben en como máximo 2 líneas de 42 caracteres.
                int count = FindMaxFittingWords(words, curr, out int splitOffset);
                if (count <= 0)
                {
                    count = 1;
                    splitOffset = 0;
                }

                IReadOnlyList<TranscriptWord> cueWords = words.Skip(curr).Take(count).ToList();
                double startSec = cueWords[0].Start;
                double endSec = cueWords[^1].End;

                if (curr == 0)
                {
                    startSec = Math.Min(startSec, segment.Start);
                }
                if (curr + count >= words.Count)
                {
                    endSec = Math.Max(endSec, segment.End);
                }

                if (endSec <= startSec)
                {
                    endSec = startSec + 0.1;
                }

                TimeSpan start = TimeSpan.FromSeconds(Math.Max(0, startSec));
                TimeSpan end = TimeSpan.FromSeconds(Math.Max(0, endSec));

                if (start < lastCueEnd)
                {
                    start = lastCueEnd;
                }
                if (end <= start)
                {
                    end = start + TimeSpan.FromMilliseconds(100);
                }

                List<string> lines = [];
                if (splitOffset <= 0 || splitOffset >= cueWords.Count)
                {
                    lines.Add(JoinWords(cueWords));
                }
                else
                {
                    lines.Add(JoinWords(cueWords.Take(splitOffset)));
                    lines.Add(JoinWords(cueWords.Skip(splitOffset)));
                }

                cues.Add(new SubtitleCue(cueIndex++, start, end, lines));
                lastCueEnd = end;
                curr += count;
            }
        }

        return cues;
    }

    /// <summary>
    /// Escribe los subtítulos en formato SRT en el flujo de texto especificado.
    /// </summary>
    public static void WriteSrt(Transcript transcript, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(writer);

        IReadOnlyList<SubtitleCue> cues = BuildCues(transcript);
        for (int i = 0; i < cues.Count; i++)
        {
            SubtitleCue cue = cues[i];
            writer.WriteLine(cue.Index.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine($"{FormatSrtTimestamp(cue.Start)} --> {FormatSrtTimestamp(cue.End)}");
            foreach (string line in cue.Lines)
            {
                writer.WriteLine(line);
            }
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Escribe los subtítulos en formato WebVTT en el flujo de texto especificado.
    /// </summary>
    public static void WriteVtt(Transcript transcript, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(writer);

        IReadOnlyList<SubtitleCue> cues = BuildCues(transcript);
        writer.WriteLine("WEBVTT");
        writer.WriteLine();

        for (int i = 0; i < cues.Count; i++)
        {
            SubtitleCue cue = cues[i];
            writer.WriteLine($"{FormatVttTimestamp(cue.Start)} --> {FormatVttTimestamp(cue.End)}");
            foreach (string line in cue.Lines)
            {
                writer.WriteLine(EscapeVtt(line));
            }
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Genera la representación en cadena en formato SRT.
    /// </summary>
    public static string ToSrt(Transcript transcript)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        WriteSrt(transcript, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Genera la representación en cadena en formato WebVTT.
    /// </summary>
    public static string ToVtt(Transcript transcript)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        WriteVtt(transcript, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Formatea un tiempo en formato SRT (hh:mm:ss,fff).
    /// </summary>
    public static string FormatSrtTimestamp(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        int hours = (int)time.TotalHours;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}");
    }

    /// <summary>
    /// Formatea un tiempo en formato WebVTT (hh:mm:ss.fff).
    /// </summary>
    public static string FormatVttTimestamp(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        int hours = (int)time.TotalHours;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}");
    }

    /// <summary>
    /// Escapa caracteres especiales según la especificación WebVTT (&amp;, &lt;, &gt;).
    /// </summary>
    public static string EscapeVtt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string JoinWords(IEnumerable<TranscriptWord> words) =>
        string.Join(' ', words.Select(w => w.Text.Trim()));

    private static IReadOnlyList<TranscriptWord> ExpandLongWords(IReadOnlyList<TranscriptWord> words)
    {
        List<TranscriptWord> result = new(words.Count);
        foreach (TranscriptWord w in words)
        {
            string clean = w.Text.Trim();
            if (clean.Length == 0)
            {
                continue;
            }

            if (clean.Length <= MaxLineLength)
            {
                result.Add(new TranscriptWord(w.Start, w.End, clean, w.P));
            }
            else
            {
                // Parte palabras que superan 42 caracteres
                double duration = Math.Max(0.01, w.End - w.Start);
                int pieces = (clean.Length + MaxLineLength - 1) / MaxLineLength;
                for (int i = 0; i < pieces; i++)
                {
                    int startChar = i * MaxLineLength;
                    int len = Math.Min(MaxLineLength, clean.Length - startChar);
                    string part = clean.Substring(startChar, len);
                    double partStart = w.Start + (duration * startChar / clean.Length);
                    double partEnd = w.Start + (duration * (startChar + len) / clean.Length);
                    result.Add(new TranscriptWord(partStart, partEnd, part, w.P));
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<TranscriptWord> SynthesizeWords(TranscriptSegment segment)
    {
        string[] rawTokens = segment.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (rawTokens.Length == 0)
        {
            return [];
        }

        List<string> tokens = [];
        foreach (string token in rawTokens)
        {
            if (token.Length <= MaxLineLength)
            {
                tokens.Add(token);
            }
            else
            {
                for (int i = 0; i < token.Length; i += MaxLineLength)
                {
                    tokens.Add(token.Substring(i, Math.Min(MaxLineLength, token.Length - i)));
                }
            }
        }

        int totalChars = tokens.Sum(t => t.Length);
        if (totalChars == 0)
        {
            return [];
        }

        double duration = Math.Max(0.1, segment.End - segment.Start);
        List<TranscriptWord> result = new(tokens.Count);
        int accumulatedChars = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            double wStart = segment.Start + (duration * accumulatedChars / totalChars);
            accumulatedChars += t.Length;
            double wEnd = segment.Start + (duration * accumulatedChars / totalChars);
            result.Add(new TranscriptWord(Math.Round(wStart, 3), Math.Round(wEnd, 3), t));
        }

        return result;
    }

    private static int FindMaxFittingWords(IReadOnlyList<TranscriptWord> words, int startIndex, out int bestSplitOffset)
    {
        bestSplitOffset = 0;
        int maxCount = 0;
        int bestSplitForMax = 0;

        // Probar longitudes crecientes de palabras hasta que ya no quepan en 2 líneas
        for (int count = 1; startIndex + count <= words.Count; count++)
        {
            if (CanFitInAtMostTwoLines(words, startIndex, count, out int split))
            {
                maxCount = count;
                bestSplitForMax = split;
            }
            else
            {
                break;
            }
        }

        bestSplitOffset = bestSplitForMax;
        return maxCount;
    }

    private static bool CanFitInAtMostTwoLines(IReadOnlyList<TranscriptWord> words, int startIndex, int count, out int bestSplit)
    {
        bestSplit = 0;
        int totalLen = 0;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                totalLen++; // espacio
            }
            totalLen += words[startIndex + i].Text.Length;
        }

        // ¿Cabe en una sola línea?
        if (totalLen <= MaxLineLength)
        {
            bestSplit = 0; // 0 significa una sola línea
            return true;
        }

        // Buscar el mejor punto de corte en 2 líneas (split: cuántas palabras van a la línea 1)
        int minDiff = int.MaxValue;
        int chosenSplit = -1;

        int line1Len = 0;
        for (int s = 1; s < count; s++)
        {
            if (s > 1)
            {
                line1Len++;
            }
            line1Len += words[startIndex + s - 1].Text.Length;

            if (line1Len > MaxLineLength)
            {
                break; // Las palabras de la primera línea ya exceden 42 caracteres
            }

            int line2Len = totalLen - line1Len - 1; // restar el espacio divisorio
            if (line2Len <= MaxLineLength)
            {
                int diff = Math.Abs(line1Len - line2Len);

                // Preferir corte tras signos de puntuación si la diferencia es razonable
                string lastWordLine1 = words[startIndex + s - 1].Text;
                if (EndsWithPunctuation(lastWordLine1) && diff <= minDiff + 4)
                {
                    diff = Math.Max(0, diff - 4);
                }

                if (diff < minDiff)
                {
                    minDiff = diff;
                    chosenSplit = s;
                }
            }
        }

        if (chosenSplit > 0)
        {
            bestSplit = chosenSplit;
            return true;
        }

        return false;
    }

    private static bool EndsWithPunctuation(string word) =>
        word.EndsWith('.') || word.EndsWith(',') || word.EndsWith(';') ||
        word.EndsWith(':') || word.EndsWith('?') || word.EndsWith('!') ||
        word.EndsWith('»') || word.EndsWith(')');
}
