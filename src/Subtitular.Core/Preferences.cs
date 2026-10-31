using System.Text.Json;

namespace Subtitular.Core;

/// <summary>Lo último que eligió el usuario en la ventana; se guarda en %LOCALAPPDATA%\Dathinel\Subtitular\preferencias.json.</summary>
public sealed record Preferences
{
    /// <summary>Carpeta del último archivo elegido: el diálogo «Elegir…» vuelve a abrirse ahí.</summary>
    public string? LastInputDir { get; init; }

    /// <summary>Carpeta de salida elegida; nula = junto al video.</summary>
    public string? OutputDir { get; init; }

    public string Language { get; init; } = "auto";

    public string Format { get; init; } = "srt";

    public string? TranslateTo { get; init; }

    public bool Speak { get; init; }

    /// <summary>Abrir la carpeta del resultado en el Explorador al terminar de guardar.</summary>
    public bool OpenFolder { get; init; } = true;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dathinel", "Subtitular", "preferencias.json");

    /// <summary>Las guardadas, o las de fábrica si no hay archivo o está roto. Las carpetas que ya no existen se olvidan.</summary>
    public static Preferences Load(string path)
    {
        Preferences p;
        try
        {
            p = JsonSerializer.Deserialize(File.ReadAllBytes(path), SubtitularJsonContext.Default.Preferences) ?? new Preferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Preferences();
        }
        return p with
        {
            LastInputDir = ExistingDir(p.LastInputDir),
            OutputDir = ExistingDir(p.OutputDir),
            Language = string.IsNullOrWhiteSpace(p.Language) ? "auto" : p.Language,
            Format = p.Format is "srt" or "vtt" ? p.Format : "srt",
            TranslateTo = string.IsNullOrWhiteSpace(p.TranslateTo) ? null : p.TranslateTo,
        };
    }

    private static string? ExistingDir(string? dir) => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) ? dir : null;

    /// <summary>Si no se puede escribir (disco lleno, permisos) se pierde el recuerdo, no la sesión.</summary>
    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(this, SubtitularJsonContext.Default.Preferences));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
