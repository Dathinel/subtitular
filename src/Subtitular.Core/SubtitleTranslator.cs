using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>Traduce los textos de una transcripción con Rodaje.Module.Translate conservando los tiempos.</summary>
[SupportedOSPlatform("windows")]
public static class SubtitleTranslator
{
    private const int OutputRegionBytes = 8 * 1024 * 1024;

    /// <summary>Idiomas de destino que admite el módulo (id y nombre en español).</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> Languages =
    [
        ("es", "Español"), ("en", "Inglés"), ("pt-BR", "Portugués (Brasil)"), ("pt-PT", "Portugués (Portugal)"), ("fr", "Francés"),
        ("it", "Italiano"), ("de", "Alemán"), ("nl", "Neerlandés"), ("ca", "Catalán"), ("gl", "Gallego"), ("eu", "Euskera"),
        ("pl", "Polaco"), ("ru", "Ruso"), ("uk", "Ucraniano"), ("tr", "Turco"), ("ar", "Árabe"), ("hi", "Hindi"),
        ("ja", "Japonés"), ("ko", "Coreano"), ("zh-Hans", "Chino simplificado"),
    ];

    public static string FindModule(string? customPath = null) =>
        Modules.Find("Rodaje.Module.Translate.exe", "traducir", customPath, "RODAJE_MODULO_TRADUCIR", "traducción");

    public static async Task<Transcript> TranslateAsync(
        Transcript transcript,
        string target,
        string? moduleExePath = null,
        string device = "auto",
        IProgress<ModuleProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        if (!Languages.Any(l => string.Equals(l.Id, target, StringComparison.OrdinalIgnoreCase)) && target is not ("pt" or "zh"))
        {
            throw new SubtitularUserException($"El traductor no admite el idioma «{target}». Admite: {string.Join(", ", Languages.Select(l => l.Id))}.");
        }

        ModuleProcess process = await Modules.StartAsync(FindModule(moduleExePath), "traducción", cancellationToken).ConfigureAwait(false);
        await using (process.ConfigureAwait(false))
        {
            return await InvokeAsync(process.Client, "traducir", transcript, target, device, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<Transcript> InvokeAsync(ModuleClient client, string function, Transcript transcript, string target, string device,
        IProgress<ModuleProgress>? progress, CancellationToken cancellationToken)
    {
        List<TranslateSegment> segments = [];
        for (int i = 0; i < transcript.Segments.Count; i++)
        {
            segments.Add(new TranslateSegment(i.ToString(CultureInfo.InvariantCulture), transcript.Segments[i].Text.Trim()));
        }
        TranslateRequest request = new(target, segments, Source: transcript.Language is { Length: > 0 } l ? l : "auto", Device: device);
        using JsonDocument paramDoc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(request, ModuleJsonContext.Default.TranslateRequest));

        using SharedMemoryRegion outRegion = client.CreateRegion(OutputRegionBytes);
        ModuleResult result = await client.InvokeAsync(function, paramDoc.RootElement.Clone(),
            buffers: [outRegion.Describe(0, OutputRegionBytes)], progress: progress, cancellationToken: cancellationToken).ConfigureAwait(false);

        TranslateResult translated;
        if (result.Value is { ValueKind: JsonValueKind.Object } v && v.TryGetProperty("bytes", out JsonElement bytesEl) && bytesEl.TryGetInt32(out int n))
        {
            translated = JsonSerializer.Deserialize(outRegion.GetSpan(0, n), ModuleJsonContext.Default.TranslateResult)
                ?? throw new InvalidDataException("La respuesta del módulo de traducción está vacía.");
        }
        else if (result.Value is { ValueKind: JsonValueKind.Object } obj)
        {
            translated = obj.Deserialize(ModuleJsonContext.Default.TranslateResult)
                ?? throw new InvalidDataException("La respuesta del módulo de traducción está vacía.");
        }
        else
        {
            throw new InvalidDataException("El módulo de traducción devolvió un resultado inesperado.");
        }

        Dictionary<string, string> byId = translated.Segments.ToDictionary(s => s.Id, s => s.Text.Replace('\n', ' '), StringComparer.Ordinal);
        List<TranscriptSegment> output = new(transcript.Segments.Count);
        for (int i = 0; i < transcript.Segments.Count; i++)
        {
            TranscriptSegment s = transcript.Segments[i];
            // El texto cambia de idioma: las palabras con tiempo del original ya no sirven; el escritor las reparte.
            output.Add(new TranscriptSegment(s.Start, s.End, byId.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out string? t) ? t : s.Text));
        }
        return new Transcript(translated.Target ?? target, translated.Device ?? transcript.Device, output);
    }
}
