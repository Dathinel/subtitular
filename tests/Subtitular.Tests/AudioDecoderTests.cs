using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class AudioDecoderTests
{
    [Fact]
    public void FindFfmpeg_finds_valid_executable()
    {
        string ffmpeg = AudioDecoder.FindFfmpeg();
        Assert.True(File.Exists(ffmpeg), $"ffmpeg no existe en {ffmpeg}");
    }

    [Fact]
    public async Task Decode_nonexistent_file_throws_user_exception()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), $"no_existe_{Guid.NewGuid():N}.mp4");
        SubtitularUserException ex = await Assert.ThrowsAsync<SubtitularUserException>(() =>
            AudioDecoder.DecodeToMemoryAsync(fakePath));

        Assert.Contains("No se encontró", ex.Message);
    }
}
