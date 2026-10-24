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

    /// <summary>Localiza Rodaje.Module.Subtitles.exe (ver <see cref="Modules.Find"/>).</summary>
    public static string FindModule(string? customPath = null) =>
        Modules.Find("Rodaje.Module.Subtitles.exe", "subtitulos", customPath, "RODAJE_MODULO_SUBTITULOS", "subtítulos");

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

        ModuleProcess process = await Modules.StartAsync(FindModule(moduleExePath), "subtítulos", cancellationToken).ConfigureAwait(false);
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
}
