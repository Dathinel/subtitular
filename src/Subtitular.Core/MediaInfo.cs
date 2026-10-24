using System.Diagnostics;
using System.Globalization;

namespace Subtitular.Core;

/// <summary>Lo que la ventana muestra al soltar un archivo: duración y subtítulos que ya hay al lado.</summary>
public static class MediaInfo
{
    public static readonly IReadOnlyList<string> Extensions =
        [".mp4", ".mkv", ".mov", ".m4v", ".webm", ".avi", ".mts", ".m2ts", ".ts", ".wmv", ".flv", ".3gp", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus"];

    /// <summary>Duración por ffprobe (junto a ffmpeg), o nula si no la da.</summary>
    public static async Task<TimeSpan?> DurationAsync(string file, string? ffmpegPath = null, CancellationToken cancellationToken = default)
    {
        string ffprobe = Path.Combine(Path.GetDirectoryName(AudioDecoder.FindFfmpeg(ffmpegPath))!, "ffprobe.exe");
        if (!File.Exists(ffprobe))
        {
            return null;
        }
        ProcessStartInfo start = new(ffprobe) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string a in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", file })
        {
            start.ArgumentList.Add(a);
        }
        using Process process = Process.Start(start) ?? throw new SubtitularUserException($"No se pudo iniciar ffprobe ({ffprobe}).");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ? TimeSpan.FromSeconds(seconds) : null;
    }

    /// <summary>Los .srt y .vtt con el mismo nombre que ya existen junto al archivo.</summary>
    public static IReadOnlyList<string> ExistingSubtitles(string file)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        string name = Path.GetFileNameWithoutExtension(file);
        return new[] { ".srt", ".vtt" }.Select(e => Path.Combine(dir, name + e)).Where(File.Exists).ToList();
    }
}
