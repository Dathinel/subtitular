namespace Subtitular.Core;

/// <summary>La vista previa editable: bloques con tiempo y texto que vuelven a ser una transcripción al guardar.</summary>
public static class SubtitlePreview
{
    public static IReadOnlyList<SubtitleCue> Cues(Transcript transcript) => SubtitleWriter.BuildCues(transcript);

    /// <summary>Texto de un bloque en una línea, para editarlo.</summary>
    public static string Text(SubtitleCue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        return string.Join(' ', cue.Lines);
    }

    /// <summary>Los bloques editados como transcripción (uno por segmento, sin los vacíos); el escritor vuelve a partir las líneas.</summary>
    public static Transcript ToTranscript(IEnumerable<(TimeSpan Start, TimeSpan End, string Text)> cues, Transcript original)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(original);
        List<TranscriptSegment> segments = [];
        foreach ((TimeSpan start, TimeSpan end, string text) in cues)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                segments.Add(new TranscriptSegment(start.TotalSeconds, end.TotalSeconds, text.Trim()));
            }
        }
        return new Transcript(original.Language, original.Device, segments);
    }
}
