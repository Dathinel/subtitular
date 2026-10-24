using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class PreviewTests
{
    [Fact]
    public void Edited_cues_become_a_transcript_and_reach_the_srt()
    {
        Transcript original = new("es", "cpu",
        [
            new TranscriptSegment(0, 2, "Hola a todos."),
            new TranscriptSegment(2, 4, "Segundo bloque."),
        ]);
        IReadOnlyList<SubtitleCue> cues = SubtitlePreview.Cues(original);
        Assert.Equal(2, cues.Count);
        Assert.Equal("Hola a todos.", SubtitlePreview.Text(cues[0]));

        Transcript edited = SubtitlePreview.ToTranscript(
            [(cues[0].Start, cues[0].End, "Hola a todas."), (cues[1].Start, cues[1].End, "   ")], original);

        Assert.Single(edited.Segments);
        Assert.Equal("es", edited.Language);
        string srt = SubtitleWriter.ToSrt(edited);
        Assert.Contains("Hola a todas.", srt, StringComparison.Ordinal);
        Assert.DoesNotContain("Segundo", srt, StringComparison.Ordinal);
        Assert.Contains("00:00:00,000 --> 00:00:02,000", srt, StringComparison.Ordinal);
    }
}
