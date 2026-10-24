using System.Text.Json;
using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class TranscriptJsonTests
{
    [Fact]
    public void Deserializes_sample_transcript_json_properly()
    {
        string json = """
        {
          "language": "es",
          "device": "vulkan",
          "segments": [
            {
              "start": 0.0,
              "end": 3.2,
              "text": "Hola a todos.",
              "words": [
                { "start": 0.12, "end": 0.48, "text": "Hola", "p": 0.98 },
                { "start": 0.50, "end": 0.90, "text": "a", "p": 0.95 },
                { "start": 0.92, "end": 1.45, "text": "todos.", "p": 0.99 }
              ]
            }
          ]
        }
        """;

        Transcript? transcript = JsonSerializer.Deserialize(json, SubtitularJsonContext.Default.Transcript);

        Assert.NotNull(transcript);
        Assert.Equal("es", transcript.Language);
        Assert.Equal("vulkan", transcript.Device);
        Assert.Single(transcript.Segments);

        TranscriptSegment seg = transcript.Segments[0];
        Assert.Equal(0.0, seg.Start);
        Assert.Equal(3.2, seg.End);
        Assert.Equal("Hola a todos.", seg.Text);
        Assert.NotNull(seg.Words);
        Assert.Equal(3, seg.Words.Count);
        Assert.Equal("Hola", seg.Words[0].Text);
        Assert.Equal(0.98, seg.Words[0].P);
    }

    [Fact]
    public void Deserializes_transcript_without_words()
    {
        string json = """
        {
          "language": "en",
          "device": "cpu",
          "segments": [
            { "start": 1.5, "end": 4.0, "text": "Testing without words." }
          ]
        }
        """;

        Transcript? transcript = JsonSerializer.Deserialize(json, SubtitularJsonContext.Default.Transcript);

        Assert.NotNull(transcript);
        Assert.Equal("en", transcript.Language);
        Assert.Null(transcript.Segments[0].Words);
    }

    [Fact]
    public void Roundtrip_serialization()
    {
        Transcript original = new("es", "vulkan",
        [
            new TranscriptSegment(1.0, 5.0, "Frase de prueba.",
            [
                new TranscriptWord(1.0, 2.0, "Frase", 0.99),
                new TranscriptWord(2.1, 2.5, "de", 0.95),
                new TranscriptWord(2.6, 5.0, "prueba.", 0.98)
            ])
        ]);

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(original, SubtitularJsonContext.Default.Transcript);
        Transcript? roundtripped = JsonSerializer.Deserialize(utf8, SubtitularJsonContext.Default.Transcript);

        Assert.NotNull(roundtripped);
        Assert.Equal(original.Language, roundtripped.Language);
        Assert.Equal(original.Device, roundtripped.Device);
        Assert.Single(roundtripped.Segments);
        Assert.Equal(original.Segments[0].Text, roundtripped.Segments[0].Text);
        Assert.Equal(3, roundtripped.Segments[0].Words!.Count);
    }
}
