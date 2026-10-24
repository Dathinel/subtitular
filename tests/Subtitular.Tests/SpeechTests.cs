using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class SpeechTests
{
    [Fact]
    public void Clips_start_at_their_time_and_wait_for_the_previous_one()
    {
        float[] a = [1, 1, 1, 1];
        float[] b = [2, 2];
        // b empieza en 0,2 s (= muestra 2) pero a dura hasta la 4: se corre a la 4.
        float[] timeline = SpeechSynthesizer.Lay([(0, a), (0.2, b)], 10);
        Assert.Equal(6, timeline.Length);
        Assert.Equal([1, 1, 1, 1, 2, 2], timeline);

        float[] spaced = SpeechSynthesizer.Lay([(0.5, b), (1.0, b)], 10);
        Assert.Equal(12, spaced.Length);
        Assert.Equal(0, spaced[4]);
        Assert.Equal(2, spaced[5]);
        Assert.Equal(2, spaced[10]);
    }

    [Fact]
    public void Wav_is_16_bit_pcm_mono()
    {
        string path = Path.Combine(Path.GetTempPath(), $"subtitular-{Guid.NewGuid():N}.wav");
        try
        {
            SpeechSynthesizer.WriteWav(path, [0f, 1f, -1f], 24000);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(44 + 6, bytes.Length);
            Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
            Assert.Equal(1, BitConverter.ToInt16(bytes, 20)); // PCM
            Assert.Equal(1, BitConverter.ToInt16(bytes, 22)); // mono
            Assert.Equal(24000, BitConverter.ToInt32(bytes, 24));
            Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
            Assert.Equal(6, BitConverter.ToInt32(bytes, 40));
            Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 46));
            Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 48));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Voice_language_maps_whisper_codes_and_rejects_the_rest()
    {
        Assert.Equal("es", SpeechSynthesizer.VoiceLanguage("es"));
        Assert.Equal("en-US", SpeechSynthesizer.VoiceLanguage("en"));
        Assert.Equal("pt-BR", SpeechSynthesizer.VoiceLanguage("pt"));
        SubtitularUserException ex = Assert.Throws<SubtitularUserException>(() => SpeechSynthesizer.VoiceLanguage("fr"));
        Assert.Contains("fr", ex.Message, StringComparison.Ordinal);
    }
}
