using System.Text.Json.Serialization;

namespace Subtitular.Core;

/// <summary>
/// Resultado de la transcripción de subtítulos.
/// </summary>
public sealed record Transcript(
    string Language,
    string Device,
    IReadOnlyList<TranscriptSegment> Segments);

/// <summary>
/// Segmento de transcripción con inicio y fin en segundos.
/// </summary>
public sealed record TranscriptSegment(
    double Start,
    double End,
    string Text,
    IReadOnlyList<TranscriptWord>? Words = null);

/// <summary>
/// Palabra individual con inicio y fin en segundos y probabilidad media de sus tokens.
/// </summary>
public sealed record TranscriptWord(
    double Start,
    double End,
    string Text,
    double P = 0);

/// <summary>
/// Parámetros para la función «subtitulos».
/// </summary>
public sealed record SubtitleParameters(
    string? Language = null,
    string? Device = null,
    string? Model = null,
    string? CpuModel = null,
    string? Prompt = null,
    int Threads = 0,
    bool Onsets = true,
    int Parallel = 1);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(Transcript))]
[JsonSerializable(typeof(TranscriptSegment))]
[JsonSerializable(typeof(TranscriptWord))]
[JsonSerializable(typeof(SubtitleParameters))]
public sealed partial class SubtitularJsonContext : JsonSerializerContext;
