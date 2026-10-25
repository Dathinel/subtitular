using System.Runtime.Versioning;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>Localiza y arranca los módulos de IA de Rodaje (procesos aparte, protocolo v1).</summary>
[SupportedOSPlatform("windows")]
public static class Modules
{
    /// <summary>
    /// Busca <paramref name="exe"/> en este orden: ruta dada, <c>SUBTITULAR_MODULOS</c>, la variable propia del módulo,
    /// la carpeta del Lanzador (<c>%LOCALAPPDATA%\Dathinel\Comun\modulos\modulo-*\&lt;versión&gt;</c>, la más nueva),
    /// los complementos de Rodaje (<c>%LOCALAPPDATA%\Rodaje\complementos\modulos</c>, <c>RODAJE_MODULOS</c>),
    /// <c>modulos\</c> junto a la app y, al correr desde el repositorio, su <c>bin</c>.
    /// </summary>
    public static string Find(string exe, string folder, string? customPath, string envVar, string nombre) =>
        Find(exe, folder, customPath, envVar, nombre, Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppContext.BaseDirectory);

    internal static string Find(string exe, string folder, string? customPath, string envVar, string nombre, Func<string, string?> env, string localAppData, string app)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                return Path.GetFullPath(customPath);
            }
            string inDir = Path.Combine(customPath, exe);
            if (Directory.Exists(customPath) && File.Exists(inDir))
            {
                return Path.GetFullPath(inDir);
            }
            throw new SubtitularUserException($"No se encontró el módulo de {nombre} en: {customPath}");
        }

        List<string> roots = [];
        if (env("SUBTITULAR_MODULOS") is { Length: > 0 } propia)
        {
            roots.Add(propia);
        }
        if (env(envVar) is { Length: > 0 } own)
        {
            roots.Add(own);
        }
        roots.Add(Path.Combine(localAppData, "Dathinel", "Comun", "modulos"));
        roots.Add(Path.Combine(localAppData, "Rodaje", "complementos", "modulos"));
        if (env("RODAJE_MODULOS") is { Length: > 0 } rodaje)
        {
            roots.Add(rodaje);
        }
        roots.Add(Path.Combine(app, "modulos"));
        roots.Add(app);

        IEnumerable<string> candidates = roots.SelectMany(r => InRoot(r, exe, folder));
        string project = Path.GetFileNameWithoutExtension(exe);
        for (DirectoryInfo? dir = new(app); dir is not null; dir = dir.Parent)
        {
            foreach (string root in new[] { Path.Combine(dir.FullName, "externo", "rodaje", "modules", project), Path.Combine(dir.FullName, "modules", project) })
            {
                candidates = candidates.Concat(
                [
                    Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", "publish", exe),
                    Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", "native", exe),
                    Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", exe),
                    Path.Combine(root, "bin", "Debug", "net10.0-windows", "win-x64", exe),
                    Path.Combine(root, "bin", "Release", "net10.0-windows", exe),
                    Path.Combine(root, "bin", "Debug", "net10.0-windows", exe),
                ]);
            }
        }

        string? found = candidates.FirstOrDefault(c => string.Equals(Path.GetFileName(c), exe, StringComparison.OrdinalIgnoreCase) && File.Exists(c));
        return found is not null
            ? Path.GetFullPath(found)
            : throw new SubtitularUserException(
                $"No se encontró el módulo de {nombre} ({exe}). Se buscó en: {string.Join("; ", roots.Distinct())}. Pasa --modulo <ruta> o define la variable SUBTITULAR_MODULOS.");
    }

    // Una raíz puede ser el exe, su carpeta o una carpeta de módulos: <raíz>\modulo-x\<exe> (Rodaje) o
    // <raíz>\modulo-x\<versión>\<exe> (Lanzador), probando la versión más nueva primero.
    private static IEnumerable<string> InRoot(string root, string exe, string folder)
    {
        yield return root;
        yield return Path.Combine(root, exe);
        yield return Path.Combine(root, folder, exe);
        string modulo = Path.Combine(root, "modulo-" + folder);
        yield return Path.Combine(modulo, exe);
        if (!Directory.Exists(modulo))
        {
            yield break;
        }
        IEnumerable<string> versions = Directory.EnumerateDirectories(modulo)
            .Select(d => (Dir: d, Version: Version.TryParse(Path.GetFileName(d), out Version? v) ? v : null))
            .Where(t => t.Version is not null)
            .OrderByDescending(t => t.Version)
            .Select(t => t.Dir);
        foreach (string dir in versions)
        {
            yield return Path.Combine(dir, exe);
        }
    }

    /// <summary>Arranca el módulo; los fallos de arranque salen como <see cref="ModuleException"/> con la ruta.</summary>
    public static async Task<ModuleProcess> StartAsync(string moduleExe, string nombre, CancellationToken cancellationToken)
    {
        EnsureDotnetEnvironment();
        try
        {
            return await ModuleProcess.StartAsync(moduleExe, options: new ModuleClientOptions { App = "subtitular" }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not SubtitularUserException)
        {
            throw new ModuleException("error-al-iniciar", $"No se pudo iniciar el módulo de {nombre} ({moduleExe}): {ex.Message}", ex);
        }
    }

    // Los módulos sin AOT (traducir) necesitan el runtime: el del entorno propio si no hay uno global.
    private static void EnsureDotnetEnvironment()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
        {
            return;
        }
        if (Environment.GetEnvironmentVariable("RODAJE_ENTORNO") is { Length: > 0 } env)
        {
            string dotnetPath = Path.Combine(env, "dotnet");
            if (Directory.Exists(dotnetPath))
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetPath);
            }
            return;
        }
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".entorno", "dotnet");
            if (Directory.Exists(candidate))
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT", candidate);
                return;
            }
        }
    }
}
