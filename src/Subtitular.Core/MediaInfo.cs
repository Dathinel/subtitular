using System.Diagnostics;
using System.Globalization;

namespace Subtitular.Core;

/// <summary>
/// Lo que se soltó o se eligió, repartido: los medios que van a la cola (los de una carpeta, por nombre), lo que no es un video
/// ni un audio y las carpetas que no tenían ninguno.
/// </summary>
public sealed record MediaSelection(IReadOnlyList<string> Media, IReadOnlyList<string> Rejected, IReadOnlyList<string> EmptyFolders)
{
    /// <summary>Qué se dejó fuera y por qué, para la barra; nulo si entró todo.</summary>
    public string? Warning
    {
        get
        {
            List<string> parts = [];
            if (Rejected.Count > 0)
            {
                string names = string.Join(", ", Rejected.Select(p => $"«{Path.GetFileName(p)}»"));
                parts.Add($"{names} {(Rejected.Count == 1 ? "no es" : "no son")} un video ni un audio que Subtitular reconozca ({string.Join(", ", MediaInfo.Extensions)}).");
            }
            if (EmptyFolders.Count > 0)
            {
                string names = string.Join(", ", EmptyFolders.Select(p => $"«{Path.GetFileName(Path.TrimEndingDirectorySeparator(p))}»"));
                parts.Add(EmptyFolders.Count == 1 ? $"La carpeta {names} no tiene videos ni audios." : $"Las carpetas {names} no tienen videos ni audios.");
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }
    }
}

/// <summary>Lo que la ventana muestra al soltar un archivo: duración y subtítulos que ya hay al lado.</summary>
public static class MediaInfo
{
    public static readonly IReadOnlyList<string> Extensions =
        [".mp4", ".mkv", ".mov", ".m4v", ".webm", ".avi", ".mts", ".m2ts", ".ts", ".wmv", ".flv", ".3gp", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus"];

    public static bool IsMedia(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Reparte archivos y carpetas: de una carpeta entran sus videos y audios por orden de nombre (no los de sus subcarpetas);
    /// un archivo que no es un medio queda en <see cref="MediaSelection.Rejected"/> y una carpeta sin ninguno en
    /// <see cref="MediaSelection.EmptyFolders"/>. Una carpeta que no se puede leer cuenta como vacía.
    /// </summary>
    public static MediaSelection Select(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<string> media = [];
        List<string> rejected = [];
        List<string> emptyFolders = [];
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                List<string> inside;
                try
                {
                    inside = [.. Directory.EnumerateFiles(path).Where(IsMedia).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    inside = [];
                }
                if (inside.Count == 0)
                {
                    emptyFolders.Add(path);
                }
                media.AddRange(inside);
            }
            else if (IsMedia(path))
            {
                media.Add(path);
            }
            else
            {
                rejected.Add(path);
            }
        }
        return new MediaSelection(media, rejected, emptyFolders);
    }

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
