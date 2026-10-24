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

/// <summary>Un subtítulo para «traducir»: el id es el índice del segmento.</summary>
public sealed record TranslateSegment(string Id, string Text);

public sealed record TranslateRequest(
    string Target,
    IReadOnlyList<TranslateSegment> Segments,
    string Source = "auto",
    string Device = "auto",
    int MaxLineChars = SubtitleWriter.MaxLineLength);

public sealed record TranslateResult(
    IReadOnlyList<TranslateSegment> Segments,
    string? Source = null,
    string? Target = null,
    string? Device = null,
    IReadOnlyList<string>? Untranslated = null);

/// <summary>Parámetros de «voz» (Rodaje.Module.Voice).</summary>
public sealed record SpeechRequest(string Text, string Language, string? Voice = null);

public sealed record SpeechResult(int SampleRate, long Frames, string? Audio = null, bool Written = false);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TranslateRequest))]
[JsonSerializable(typeof(TranslateResult))]
[JsonSerializable(typeof(SpeechRequest))]
[JsonSerializable(typeof(SpeechResult))]
public sealed partial class ModuleJsonContext : JsonSerializerContext;
