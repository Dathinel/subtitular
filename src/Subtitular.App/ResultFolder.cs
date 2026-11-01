using System.Diagnostics;

namespace Subtitular.App;

/// <summary>Dónde quedó el resultado, para abrirlo en el Explorador al terminar (cosa de la ventana, no de Core).</summary>
internal static class ResultFolder
{
    /// <summary>Carpeta común de las salidas; si están en varias, la de la primera. Nula sin salidas.</summary>
    public static string? Of(IEnumerable<string?> outputs) =>
        outputs.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => Path.GetDirectoryName(Path.GetFullPath(o!))).FirstOrDefault(d => d is not null);

    /// <summary>
    /// Orden para el Explorador: con un solo archivo lo abre seleccionado («/select,»); con varios o ninguno abre la carpeta.
    /// Nula si no hay nada que abrir.
    /// </summary>
    public static (string Exe, string Arguments)? ExplorerCommand(IReadOnlyList<string?> outputs)
    {
        List<string> files = [.. outputs.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => Path.GetFullPath(o!))];
        if (files.Count == 1)
        {
            return ("explorer.exe", "/select,\"" + files[0] + "\"");
        }
        return Of(files) is { } dir ? ("explorer.exe", "\"" + dir + "\"") : null;
    }

    /// <summary>Abre el Explorador en el resultado; si no se puede, no pasa nada.</summary>
    public static void Open(IReadOnlyList<string?> outputs)
    {
        if (ExplorerCommand(outputs) is not { } command)
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(command.Exe, command.Arguments) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
