using System.Diagnostics;

namespace Subtitular.Core;

/// <summary>
/// Decodifica archivos de audio y video usando FFmpeg a muestras flotantes de 32 bits (f32le), mono, a 16 kHz.
/// </summary>
public static class AudioDecoder
{
    public const int TargetSampleRate = 16000;
    public const int TargetChannels = 1;

    /// <summary>
    /// Localiza el ejecutable de ffmpeg.exe buscando en la ruta indicada, en RODAJE_ENTORNO,
    /// en directorios superiores o en el PATH.
    /// </summary>
    public static string FindFfmpeg(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                return Path.GetFullPath(customPath);
            }
            throw new SubtitularUserException($"No se encontró el ejecutable de FFmpeg en: {customPath}");
        }

        // RODAJE_ENTORNO
        if (Environment.GetEnvironmentVariable("RODAJE_ENTORNO") is { Length: > 0 } env && Directory.Exists(env))
        {
            string[] envCandidates =
            [
                Path.Combine(env, "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(env, "ffmpeg-lgpl", "bin", "ffmpeg.exe"),
                Path.Combine(env, "ffmpeg-gpl", "bin", "ffmpeg.exe"),
                Path.Combine(env, "bin", "ffmpeg.exe"),
            ];
            foreach (string c in envCandidates)
            {
                if (File.Exists(c))
                {
                    return Path.GetFullPath(c);
                }
            }
        }

        // Directorios superiores (.entorno/ffmpeg/bin/ffmpeg.exe)
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".entorno", "ffmpeg", "bin", "ffmpeg.exe");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        // PATH
        if (Environment.GetEnvironmentVariable("PATH") is { Length: > 0 } pathVar)
        {
            string[] paths = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in paths)
            {
                string candidate = Path.Combine(p.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        throw new SubtitularUserException("No se encontró ffmpeg.exe. Pasa --ffmpeg <ruta>, define la variable RODAJE_ENTORNO o agrégalo al PATH.");
    }

    /// <summary>
    /// Decodifica el archivo de medios a un búfer en memoria con muestras f32le mono a 16 kHz.
    /// </summary>
    public static async Task<byte[]> DecodeToMemoryAsync(string inputMediaFile, string? ffmpegPath = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputMediaFile))
        {
            throw new SubtitularUserException($"No se encontró el archivo de entrada: {inputMediaFile}");
        }

        string ffmpeg = FindFfmpeg(ffmpegPath);

        ProcessStartInfo startInfo = new(ffmpeg)
        {
            Arguments = $"-hide_banner -loglevel error -i \"{inputMediaFile}\" -vn -sn -dn -f f32le -ac {TargetChannels} -ar {TargetSampleRate} pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new SubtitularUserException($"No se pudo iniciar FFmpeg ({ffmpeg}): {ex.Message}", ex);
        }

        using MemoryStream audioStream = new();
        Task copyOutputTask = process.StandardOutput.BaseStream.CopyToAsync(audioStream, cancellationToken);
        Task<string> readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await Task.WhenAll(copyOutputTask, readErrorTask).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignorar error al matar proceso cancelado
            }
            throw;
        }

        if (process.ExitCode != 0)
        {
            string stderr = await readErrorTask.ConfigureAwait(false);
            throw new SubtitularUserException($"FFmpeg falló con código {process.ExitCode}: {stderr.Trim()}");
        }

        byte[] audioBytes = audioStream.ToArray();
        if (audioBytes.Length == 0)
        {
            throw new SubtitularUserException($"El archivo «{Path.GetFileName(inputMediaFile)}» no produjo audio decodificado.");
        }

        if (audioBytes.Length % 4 != 0)
        {
            throw new SubtitularUserException("El audio decodificado contiene muestras f32le incompletas.");
        }

        return audioBytes;
    }
}
