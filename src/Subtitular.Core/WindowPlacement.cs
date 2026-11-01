using System.Text.Json;

namespace Subtitular.Core;

/// <summary>Dónde quedó la ventana (píxeles físicos); se guarda en %LOCALAPPDATA%\Dathinel\Subtitular\ventana.json.</summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height)
{
    public const int MinWidth = 800;
    public const int MinHeight = 700;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dathinel", "Subtitular", "ventana.json");

    /// <summary>Nulo si no hay archivo, está roto o la ventana guardada es más pequeña que el mínimo.</summary>
    public static WindowPlacement? Load(string path)
    {
        try
        {
            WindowPlacement? p = JsonSerializer.Deserialize(File.ReadAllBytes(path), SubtitularJsonContext.Default.WindowPlacement);
            return p is { Width: >= MinWidth, Height: >= MinHeight } ? p : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Escritura atómica (<see cref="JsonFile"/>); si no se puede escribir se pierde el recuerdo, no la sesión.</summary>
    public void Save(string path) => JsonFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, SubtitularJsonContext.Default.WindowPlacement));
}
