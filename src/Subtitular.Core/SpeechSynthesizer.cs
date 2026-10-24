using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>Lee una transcripción en voz alta con Rodaje.Module.Voice: cada segmento empieza en su tiempo.</summary>
[SupportedOSPlatform("windows")]
public static class SpeechSynthesizer
{
    public static string FindModule(string? customPath = null) =>
        Modules.Find("Rodaje.Module.Voice.exe", "voz", customPath, "RODAJE_MODULO_VOZ", "voz");

    /// <summary>El idioma de la voz para un idioma de transcripción (es, pt, en); el módulo solo lee esos tres.</summary>
    public static string VoiceLanguage(string? transcriptLanguage)
    {
        string l = (transcriptLanguage ?? "").ToLowerInvariant();
        return l switch
        {
            "es" or "es-419" or "spanish" => "es",
            "pt" or "pt-br" or "pt-pt" or "portuguese" => "pt-BR",
            "en" or "en-us" or "english" => "en-US",
            "en-gb" => "en-GB",
            _ => throw new SubtitularUserException($"La voz solo lee español, portugués e inglés; el texto está en «{transcriptLanguage}». Traduce primero a uno de esos idiomas."),
        };
    }

    /// <summary>Audio mono f32 con cada segmento colocado en su tiempo, y su frecuencia de muestreo.</summary>
    public static async Task<(float[] Audio, int SampleRate)> SpeakAsync(
        Transcript transcript,
        string? moduleExePath = null,
        string? voice = null,
        IProgress<ModuleProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        string language = VoiceLanguage(transcript.Language);
        List<(double Start, float[] Audio)> clips = [];
        int sampleRate = 24000;

        ModuleProcess process = await Modules.StartAsync(FindModule(moduleExePath), "voz", cancellationToken).ConfigureAwait(false);
        await using (process.ConfigureAwait(false))
        {
            IReadOnlyList<TranscriptSegment> segments = transcript.Segments.Where(s => !string.IsNullOrWhiteSpace(s.Text)).ToList();
            for (int i = 0; i < segments.Count; i++)
            {
                (float[] audio, sampleRate) = await SpeakOneAsync(process.Client, new SpeechRequest(segments[i].Text.Trim(), language, voice), cancellationToken).ConfigureAwait(false);
                clips.Add((segments[i].Start, audio));
                progress?.Report(new ModuleProgress((i + 1.0) / segments.Count, "voz"));
            }
        }
        return (Lay(clips, sampleRate), sampleRate);
    }

    private static async Task<(float[] Audio, int SampleRate)> SpeakOneAsync(ModuleClient client, SpeechRequest request, CancellationToken cancellationToken)
    {
        using JsonDocument paramDoc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(request, ModuleJsonContext.Default.SpeechRequest));
        ModuleResult spoken = await client.InvokeAsync("voz", paramDoc.RootElement.Clone(), cancellationToken: cancellationToken).ConfigureAwait(false);
        SpeechResult? result = spoken.Value is { ValueKind: JsonValueKind.Object } v ? v.Deserialize(ModuleJsonContext.Default.SpeechResult) : null;
        if (result is null || result.SampleRate <= 0)
        {
            throw new InvalidDataException("El módulo de voz devolvió un resultado inesperado.");
        }
        if (result.Frames == 0 || result.Audio is null)
        {
            return ([], result.SampleRate);
        }

        // Sin búfer en «voz» el módulo guarda el audio y «entregar» lo copia al tamaño exacto.
        long bytes = result.Frames * 4;
        using SharedMemoryRegion region = client.CreateRegion(bytes);
        using JsonDocument deliver = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new SpeechResult(result.SampleRate, result.Frames, result.Audio), ModuleJsonContext.Default.SpeechResult));
        await client.InvokeAsync("entregar", deliver.RootElement.Clone(), buffers: [region.Describe(0, bytes)], cancellationToken: cancellationToken).ConfigureAwait(false);
        float[] audio = new float[result.Frames];
        region.GetSpan(0, bytes).CopyTo(MemoryMarshal.AsBytes(audio.AsSpan()));
        return (audio, result.SampleRate);
    }

    /// <summary>Coloca cada clip en su tiempo; si el anterior todavía suena, el siguiente espera a que termine.</summary>
    internal static float[] Lay(IReadOnlyList<(double Start, float[] Audio)> clips, int sampleRate)
    {
        long cursor = 0;
        List<(long At, float[] Audio)> placed = new(clips.Count);
        foreach ((double start, float[] audio) in clips)
        {
            long at = Math.Max(cursor, (long)Math.Round(Math.Max(0, start) * sampleRate));
            placed.Add((at, audio));
            cursor = at + audio.Length;
        }
        float[] timeline = new float[cursor];
        foreach ((long at, float[] audio) in placed)
        {
            audio.CopyTo(timeline, (int)at);
        }
        return timeline;
    }

    /// <summary>WAV PCM de 16 bits mono.</summary>
    public static void WriteWav(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        using FileStream fs = File.Create(path);
        using BinaryWriter w = new(fs);
        int data = samples.Length * 2;
        w.Write("RIFF"u8);
        w.Write(36 + data);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(data);
        foreach (float s in samples)
        {
            w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
