using System.Text.Json;

namespace Subtitular.Core;

/// <summary>
/// Lo último que eligió el usuario en la ventana; se guarda en %LOCALAPPDATA%\Dathinel\Subtitular\preferencias.json.
/// <c>LastInputDir</c> es la carpeta del último archivo elegido (el diálogo «Elegir…» vuelve a abrirse ahí); <c>OutputDir</c> nula = junto al video;
/// <c>OpenFolder</c> abre el Explorador con el resultado al guardar; <c>SaveWithoutReview</c> guarda un solo archivo al terminar, sin pasar
/// por la vista previa (como ya hace la cola). El aviso de versión nueva: <c>CheckUpdates</c> lo enciende,
/// <c>LastUpdateCheck</c> es la última consulta a GitHub (como mucho una cada 24 h) y <c>SkippedVersion</c> la que se descartó con «Más tarde».
/// Constructor primario con valores por omisión: una propiedad que falte en un archivo viejo toma el suyo al deserializar.
/// </summary>
public sealed record Preferences(
    string? LastInputDir = null,
    string? OutputDir = null,
    string Language = "auto",
    string Format = "srt",
    string? TranslateTo = null,
    bool Speak = false,
    bool OpenFolder = true,
    bool SaveWithoutReview = false,
    bool CheckUpdates = true,
    DateTimeOffset? LastUpdateCheck = null,
    string? SkippedVersion = null)
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
            SkippedVersion = string.IsNullOrWhiteSpace(p.SkippedVersion) ? null : p.SkippedVersion,
        };
    }

    /// <summary>Escritura atómica (<see cref="JsonFile"/>); si no se puede escribir se pierde el recuerdo, no la sesión.</summary>
    public void Save(string path) => JsonFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, SubtitularJsonContext.Default.Preferences));
}
