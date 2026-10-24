namespace Subtitular.Core;

/// <summary>
/// Error atribuible a la entrada o configuración del usuario (archivo no encontrado, modelo faltante, etc.).
/// Termina con código de salida 1.
/// </summary>
public sealed class SubtitularUserException : Exception
{
    public SubtitularUserException(string message) : base(message)
    {
    }

    public SubtitularUserException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
