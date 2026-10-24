using System.Globalization;
using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class SubtitleWriterTests
{
    [Fact]
    public void Format_timestamps_srt_and_vtt()
    {
        TimeSpan t1 = TimeSpan.Zero;
        TimeSpan t2 = TimeSpan.FromSeconds(1.234);
        TimeSpan t3 = TimeSpan.FromSeconds(3665.078);

        Assert.Equal("00:00:00,000", SubtitleWriter.FormatSrtTimestamp(t1));
        Assert.Equal("00:00:01,234", SubtitleWriter.FormatSrtTimestamp(t2));
        Assert.Equal("01:01:05,078", SubtitleWriter.FormatSrtTimestamp(t3));

        Assert.Equal("00:00:00.000", SubtitleWriter.FormatVttTimestamp(t1));
        Assert.Equal("00:00:01.234", SubtitleWriter.FormatVttTimestamp(t2));
        Assert.Equal("01:01:05.078", SubtitleWriter.FormatVttTimestamp(t3));
    }

    [Fact]
    public void Escape_vtt_special_characters()
    {
        string raw = "Tom & Jerry <en acción> dijeron \"¡hola!\"";
        string escaped = SubtitleWriter.EscapeVtt(raw);

        Assert.Equal("Tom &amp; Jerry &lt;en acción&gt; dijeron \"¡hola!\"", escaped);
    }

    [Fact]
    public void Cues_strictly_respect_42_chars_and_max_2_lines()
    {
        string longSentence = "Esta es una frase de prueba diseñada para verificar que los subtítulos se cortan a cuarenta y dos caracteres y no más de dos líneas por bloque de subtítulo.";
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 8.0, longSentence)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count is >= 1 and <= SubtitleWriter.MaxLinesPerCue,
                $"El bloque {cue.Index} tiene {cue.Lines.Count} líneas (máximo 2).");

            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= SubtitleWriter.MaxLineLength,
                    $"La línea «{line}» supera los {SubtitleWriter.MaxLineLength} caracteres (longitud {line.Length}).");
            }
        }
    }

    [Fact]
    public void Ultra_long_single_word_is_split_into_chunks_under_42_chars()
    {
        string ultraLongWord = new('A', 100);
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 5.0, ultraLongWord)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);

        Assert.NotEmpty(cues);
        foreach (SubtitleCue cue in cues)
        {
            Assert.True(cue.Lines.Count <= 2);
            foreach (string line in cue.Lines)
            {
                Assert.True(line.Length <= 42, $"Línea de largo {line.Length}: {line}");
            }
        }
    }

    [Fact]
    public void Lines_are_balanced_when_possible()
    {
        // 54 caracteres en total: si fuera desbalanceado, podría ser 42 + 11.
        // Con balanceo, debe ser dos líneas de largo similar (ej. ~27 caracteres).
        string text = "Esta es una frase moderada que debería ser dividida.";
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.0, 4.0, text)
        ]);

        IReadOnlyList<SubtitleCue> cues = SubtitleWriter.BuildCues(transcript);
        Assert.Single(cues);
        Assert.Equal(2, cues[0].Lines.Count);

        int len1 = cues[0].Lines[0].Length;
        int len2 = cues[0].Lines[1].Length;
        Assert.True(len1 <= 42);
        Assert.True(len2 <= 42);
        Assert.True(Math.Abs(len1 - len2) <= 15, $"Diferencia entre líneas excesiva: {len1} vs {len2}");
    }

    [Fact]
    public void Srt_output_structure_and_numbering()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(1.0, 3.5, "Hola mundo.", [
                new TranscriptWord(1.0, 2.0, "Hola"),
                new TranscriptWord(2.0, 3.5, "mundo.")
            ]),
            new TranscriptSegment(4.0, 7.0, "Segunda frase.", [
                new TranscriptWord(4.0, 5.5, "Segunda"),
                new TranscriptWord(5.5, 7.0, "frase.")
            ])
        ]);

        string srt = SubtitleWriter.ToSrt(transcript);

        Assert.Contains("1\r\n00:00:01,000 --> 00:00:03,500\r\nHola mundo.\r\n", srt.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.Contains("2\r\n00:00:04,000 --> 00:00:07,000\r\nSegunda frase.\r\n", srt.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
    }

    [Fact]
    public void Vtt_output_has_header_and_correct_format()
    {
        Transcript transcript = new("es", "cpu",
        [
            new TranscriptSegment(0.5, 2.5, "Prueba <VTT> & subtítulos.")
        ]);

        string vtt = SubtitleWriter.ToVtt(transcript);

        Assert.StartsWith("WEBVTT", vtt.TrimStart());
        Assert.Contains("00:00:00.500 --> 00:00:02.500", vtt);
        Assert.Contains("Prueba &lt;VTT&gt; &amp; subtítulos.", vtt);
    }
}
