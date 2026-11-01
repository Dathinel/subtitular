using System.Text.Json;

namespace Subtitular.Core;

/// <summary>
/// Lo último que eligió el usuario en la ventana; se guarda en %LOCALAPPDATA%\Dathinel\Subtitular\preferencias.json.
/// <c>LastInputDir</c> es la carpeta del último archivo elegido (el diálogo «Elegir…» vuelve a abrirse ahí); <c>OutputDir</c> nula = junto al video;
/// <c>OpenFolder</c> abre el Explorador con el resultado al guardar. Constructor primario con valores por omisión: una propiedad
/// que falte en un archivo viejo toma el suyo al deserializar.
/// </summary>
public sealed record Preferences(
    string? LastInputDir = null,
    string? OutputDir = null,
    string Language = "auto",
    string Format = "srt",
    string? TranslateTo = null,
    bool Speak = false,
    bool OpenFolder = true)
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dathinel", "Subtitular", "preferencias.json");

    /// <summary>
    /// Las guardadas, o las de fábrica si no hay archivo o está roto. Una carpeta que no exista en este arranque (disco externo o
    /// de red desconectado) se conserva: el diálogo ya cae a la carpeta de siempre si no está.
    /// </summary>
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
            LastInputDir = string.IsNullOrWhiteSpace(p.LastInputDir) ? null : p.LastInputDir,
            OutputDir = string.IsNullOrWhiteSpace(p.OutputDir) ? null : p.OutputDir,
            Language = string.IsNullOrWhiteSpace(p.Language) ? "auto" : p.Language,
            Format = p.Format is "srt" or "vtt" ? p.Format : "srt",
            TranslateTo = string.IsNullOrWhiteSpace(p.TranslateTo) ? null : p.TranslateTo,
        };
    }

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
