using System.Runtime.Versioning;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>Localiza y arranca los módulos de IA de Rodaje (procesos aparte, protocolo v1).</summary>
[SupportedOSPlatform("windows")]
public static class Modules
{
    /// <summary>
    /// Busca <paramref name="exe"/>: ruta dada, <c>SUBTITULAR_MODULOS</c>, la variable propia del módulo, junto a la app
    /// (<c>modulos\</c>), los complementos de Rodaje en LocalAppData y, al correr desde el repositorio, su <c>bin</c>.
    /// </summary>
    public static string Find(string exe, string folder, string? customPath, string envVar, string nombre)
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

        List<string> candidates = [];
        if (Environment.GetEnvironmentVariable("SUBTITULAR_MODULOS") is { Length: > 0 } env)
        {
            candidates.Add(env);
            candidates.Add(Path.Combine(env, exe));
            candidates.Add(Path.Combine(env, folder, exe));
            candidates.Add(Path.Combine(env, "modulo-" + folder, exe));
        }
        if (Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } own)
        {
            candidates.Add(own);
        }
        string app = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(app, exe));
        candidates.Add(Path.Combine(app, "modulos", exe));
        candidates.Add(Path.Combine(app, "modulos", folder, exe));
        candidates.Add(Path.Combine(app, "modulos", "modulo-" + folder, exe));
        string rodaje = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rodaje", "complementos", "modulos");
        candidates.Add(Path.Combine(rodaje, "modulo-" + folder, exe));
        candidates.Add(Path.Combine(rodaje, folder, exe));
        string project = Path.GetFileNameWithoutExtension(exe);
        for (DirectoryInfo? dir = new(app); dir is not null; dir = dir.Parent)
        {
            foreach (string root in new[] { Path.Combine(dir.FullName, "externo", "rodaje", "modules", project), Path.Combine(dir.FullName, "modules", project) })
            {
                candidates.Add(Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", "publish", exe));
                candidates.Add(Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", "native", exe));
                candidates.Add(Path.Combine(root, "bin", "Release", "net10.0-windows", "win-x64", exe));
                candidates.Add(Path.Combine(root, "bin", "Debug", "net10.0-windows", "win-x64", exe));
                candidates.Add(Path.Combine(root, "bin", "Release", "net10.0-windows", exe));
                candidates.Add(Path.Combine(root, "bin", "Debug", "net10.0-windows", exe));
            }
        }

        string? found = candidates.FirstOrDefault(c => File.Exists(c) && string.Equals(Path.GetFileName(c), exe, StringComparison.OrdinalIgnoreCase));
        return found is not null
            ? Path.GetFullPath(found)
            : throw new SubtitularUserException(
                $"No se encontró el módulo de {nombre} ({exe}). Se buscó en {rodaje}. Pasa --modulo <ruta> o define la variable SUBTITULAR_MODULOS.");
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
