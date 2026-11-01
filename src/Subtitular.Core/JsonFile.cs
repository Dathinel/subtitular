namespace Subtitular.Core;

/// <summary>
/// Escritura atómica de los JSON de estado (preferencias, ventana): se escribe un «.tmp» al lado y se renombra encima,
/// así un apagón a medias deja el archivo anterior entero y no uno a medio escribir que al cargar se tomaría por roto.
/// </summary>
internal static class JsonFile
{
    private const int MaxRetries = 10;
    private const int RetryDelayMs = 50;

    /// <summary>Si no se puede escribir (disco lleno, permisos) se pierde el recuerdo, no la sesión: devuelve falso y no lanza.</summary>
    public static bool Write(string path, byte[] bytes)
    {
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temp, bytes);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    return true;
                }
                catch (Exception moveEx) when (attempt < MaxRetries && moveEx is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception inner) when (inner is IOException or UnauthorizedAccessException)
            {
            }
            return false;
        }
    }
}
