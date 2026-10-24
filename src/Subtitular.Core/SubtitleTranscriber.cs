using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>
/// Orquesta la llamada al módulo de subtítulos (Rodaje.Module.Subtitles) a través del protocolo v1.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SubtitleTranscriber
{
    private const int OutputRegionBytes = 8 * 1024 * 1024; // 8 MiB para transcripts largos

    /// <summary>
    /// Localiza el ejecutable Rodaje.Module.Subtitles.exe en la ruta especificada, en variables
    /// de entorno, junto al ejecutable o en el repositorio.
    /// </summary>
    public static string FindModule(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                return Path.GetFullPath(customPath);
            }
            if (Directory.Exists(customPath))
            {
                string inDir = Path.Combine(customPath, "Rodaje.Module.Subtitles.exe");
                if (File.Exists(inDir))
                {
                    return Path.GetFullPath(inDir);
                }
            }
            throw new SubtitularUserException($"No se encontró el módulo de subtítulos en: {customPath}");
        }

        // SUBTITULAR_MODULOS
        if (Environment.GetEnvironmentVariable("SUBTITULAR_MODULOS") is { Length: > 0 } envSub)
        {
            if (File.Exists(envSub))
            {
                return Path.GetFullPath(envSub);
            }
            if (Directory.Exists(envSub))
            {
                string[] candidates =
                [
                    Path.Combine(envSub, "Rodaje.Module.Subtitles.exe"),
                    Path.Combine(envSub, "subtitulos", "Rodaje.Module.Subtitles.exe"),
                ];
                foreach (string c in candidates)
                {
                    if (File.Exists(c))
                    {
                        return Path.GetFullPath(c);
                    }
                }
            }
        }

        // RODAJE_MODULO_SUBTITULOS
        if (Environment.GetEnvironmentVariable("RODAJE_MODULO_SUBTITULOS") is { Length: > 0 } envMod && File.Exists(envMod))
        {
            return Path.GetFullPath(envMod);
        }

        // Junto a la aplicación
        string[] localCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Rodaje.Module.Subtitles.exe"),
            Path.Combine(AppContext.BaseDirectory, "modulos", "Rodaje.Module.Subtitles.exe"),
            Path.Combine(AppContext.BaseDirectory, "modulos", "subtitulos", "Rodaje.Module.Subtitles.exe"),
        ];
        foreach (string c in localCandidates)
        {
            if (File.Exists(c))
            {
                return Path.GetFullPath(c);
            }
        }

        // En LocalAppData de Rodaje
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDataCandidate = Path.Combine(localAppData, "Rodaje", "complementos", "modulos", "subtitulos", "Rodaje.Module.Subtitles.exe");
        if (File.Exists(appDataCandidate))
        {
            return Path.GetFullPath(appDataCandidate);
        }

        // En directorios superiores (entorno de desarrollo/pruebas)
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string[] repoCandidates =
            [
                Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Subtitles", "bin", "Release", "net10.0-windows", "win-x64", "publish", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Subtitles", "bin", "Release", "net10.0-windows", "win-x64", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Subtitles", "bin", "Debug", "net10.0-windows", "win-x64", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Subtitles", "bin", "Release", "net10.0-windows", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Subtitles", "bin", "Debug", "net10.0-windows", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "modules", "Rodaje.Module.Subtitles", "bin", "Release", "net10.0-windows", "win-x64", "publish", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "modules", "Rodaje.Module.Subtitles", "bin", "Release", "net10.0-windows", "win-x64", "Rodaje.Module.Subtitles.exe"),
                Path.Combine(dir.FullName, "modules", "Rodaje.Module.Subtitles", "bin", "Debug", "net10.0-windows", "win-x64", "Rodaje.Module.Subtitles.exe"),
            ];
            foreach (string c in repoCandidates)
            {
                if (File.Exists(c))
                {
                    return Path.GetFullPath(c);
                }
            }
        }

        throw new SubtitularUserException(
            "No se encontró el módulo de subtítulos (Rodaje.Module.Subtitles.exe). Pasa --modulo <ruta> o define la variable SUBTITULAR_MODULOS.");
    }

    /// <summary>
    /// Transcribe las muestras de audio f32le invocando al módulo de subtítulos a través del protocolo v1.
    /// </summary>
    public static async Task<Transcript> TranscribeAsync(
        byte[] audioBytes,
        SubtitleParameters parameters,
        string? moduleExePath = null,
        IProgress<ModuleProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioBytes);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!string.IsNullOrWhiteSpace(parameters.Model) && !File.Exists(parameters.Model))
        {
            throw new SubtitularUserException($"No se encontró el archivo del modelo Whisper solicitado: {parameters.Model}");
        }

        string moduleExe = FindModule(moduleExePath);
        EnsureDotnetEnvironment();

        ModuleProcess process;
        try
        {
            process = await ModuleProcess.StartAsync(
                moduleExe,
                options: new ModuleClientOptions { App = "subtitular" },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not SubtitularUserException)
        {
            throw new ModuleException("error-al-iniciar", $"No se pudo iniciar el proceso del módulo de subtítulos ({moduleExe}): {ex.Message}", ex);
        }

        await using (process.ConfigureAwait(false))
        {
            long audioLength = audioBytes.Length;
            long frames = audioLength / (AudioDecoder.TargetChannels * 4L);

            SharedMemoryRegion audioRegion = process.Client.CreateRegion(audioLength);
            audioBytes.CopyTo(audioRegion.GetSpan(0, audioLength));
            SharedBufferRef audioBuffer = audioRegion.Describe(
                0,
                audioLength,
                audio: new AudioLayout(AudioDecoder.TargetSampleRate, AudioDecoder.TargetChannels, frames));

            SharedMemoryRegion outRegion = process.Client.CreateRegion(OutputRegionBytes);
            SharedBufferRef outBuffer = outRegion.Describe(0, OutputRegionBytes);

            byte[] paramBytes = JsonSerializer.SerializeToUtf8Bytes(parameters, SubtitularJsonContext.Default.SubtitleParameters);
            using JsonDocument paramDoc = JsonDocument.Parse(paramBytes);

            ModuleResult result = await process.Client.InvokeAsync(
                "subtitulos",
                paramDoc.RootElement.Clone(),
                buffers: [audioBuffer, outBuffer],
                progress: progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Value is { ValueKind: JsonValueKind.Object } v &&
                v.TryGetProperty("bytes", out JsonElement bytesEl) &&
                bytesEl.TryGetInt32(out int n))
            {
                ReadOnlySpan<byte> jsonBytes = outRegion.GetSpan(0, n);
                return JsonSerializer.Deserialize(jsonBytes, SubtitularJsonContext.Default.Transcript)
                    ?? throw new InvalidDataException("La respuesta del módulo de subtítulos está vacía.");
            }

            if (result.Value is { ValueKind: JsonValueKind.Object } obj)
            {
                return obj.Deserialize(SubtitularJsonContext.Default.Transcript)
                    ?? throw new InvalidDataException("La respuesta del módulo de subtítulos está vacía.");
            }

            throw new InvalidDataException("El módulo devolvió un resultado inesperado.");
        }
    }

    private static void EnsureDotnetEnvironment()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
        {
            if (Environment.GetEnvironmentVariable("RODAJE_ENTORNO") is { Length: > 0 } env)
            {
                string dotnetPath = Path.Combine(env, "dotnet");
                if (Directory.Exists(dotnetPath))
                {
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetPath);
                }
            }
            else
            {
                for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, ".entorno", "dotnet");
                    if (Directory.Exists(candidate))
                    {
                        Environment.SetEnvironmentVariable("DOTNET_ROOT", candidate);
                        break;
                    }
                }
            }
        }
    }
}
