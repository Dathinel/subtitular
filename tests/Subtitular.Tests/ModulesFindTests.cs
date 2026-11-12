using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class ModulesFindTests : IDisposable
{
    private const string Exe = "Rodaje.Module.Subtitles.exe";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "subtitular-find-" + Guid.NewGuid().ToString("N"));
    private readonly string _local;
    private readonly string _app;
    private readonly Dictionary<string, string?> _env = [];

    public ModulesFindTests()
    {
        _local = Path.Combine(_root, "LocalAppData");
        _app = Path.Combine(_root, "app");
        Directory.CreateDirectory(_app);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Put(params string[] parts)
    {
        string path = Path.Combine([_root, .. parts, Exe]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private string Find(string? custom = null) =>
        Modules.Find(Exe, "subtitulos", custom, "RODAJE_MODULO_SUBTITULOS", "subtítulos", k => _env.GetValueOrDefault(k), _local, _app);

    [Fact]
    public void Finds_in_each_place_following_the_order()
    {
        string lanzadorOld = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.0.0");
        string lanzador = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.2.0");
        Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.10.0-rc"); // no es versión: se ignora
        string rodaje = Put("LocalAppData", "Rodaje", "complementos", "modulos", "modulo-subtitulos");
        string rodajeEnv = Put("rodaje-env", "modulo-subtitulos");
        string junto = Put("app", "modulos");
        string propia = Put("propia", "modulo-subtitulos", "2.0.0");

        Assert.Equal(lanzador, Find());
        File.Delete(lanzador);
        Assert.Equal(lanzadorOld, Find());
        File.Delete(lanzadorOld);
        Assert.Equal(rodaje, Find());
        File.Delete(rodaje);
        Assert.Equal(junto, Find());
        _env["RODAJE_MODULOS"] = Path.Combine(_root, "rodaje-env");
        Assert.Equal(rodajeEnv, Find());
        _env["SUBTITULAR_MODULOS"] = Path.Combine(_root, "propia");
        Assert.Equal(propia, Find());
        Assert.Equal(junto, Find(Path.Combine(_app, "modulos")));
    }

    [Fact]
    public void Missing_module_lists_the_places_tried_in_spanish()
    {
        _env["SUBTITULAR_MODULOS"] = Path.Combine(_root, "nada");
        var ex = Assert.Throws<SubtitularUserException>(() => Find());
        Assert.Contains("No se encontró el módulo de subtítulos", ex.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(_root, "nada"), ex.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(_local, "Dathinel", "Comun", "modulos"), ex.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(_local, "Rodaje", "complementos", "modulos"), ex.Message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(_app, "modulos"), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_strict_full_precedence_order_step_by_step()
    {
        string customFile = Put("custom-direct");
        string customDir = Path.GetDirectoryName(Put("custom-folder"))!;
        string propia = Put("propia", "modulo-subtitulos", "3.0.0");
        string moduloEnv = Put("mod-env", "modulo-subtitulos");
        string lanzador = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "2.5.0");
        string rodajeLocal = Put("LocalAppData", "Rodaje", "complementos", "modulos", "modulo-subtitulos");
        string rodajeEnv = Put("rodaje-env", "modulo-subtitulos");
        string appModulos = Put("app", "modulos");
        string appDirect = Put("app");

        // 1. customPath directo a archivo
        Assert.Equal(customFile, Find(customFile));

        // 2. customPath a carpeta que contiene el exe
        Assert.Equal(Path.Combine(customDir, Exe), Find(customDir));

        // 3. SUBTITULAR_MODULOS
        _env["SUBTITULAR_MODULOS"] = Path.Combine(_root, "propia");
        _env["RODAJE_MODULO_SUBTITULOS"] = Path.Combine(_root, "mod-env");
        _env["RODAJE_MODULOS"] = Path.Combine(_root, "rodaje-env");
        Assert.Equal(propia, Find());

        // 4. Variable de entorno propia del módulo (RODAJE_MODULO_SUBTITULOS)
        _env.Remove("SUBTITULAR_MODULOS");
        Assert.Equal(moduloEnv, Find());

        // 5. Carpeta del Lanzador (Dathinel\Comun\modulos)
        _env.Remove("RODAJE_MODULO_SUBTITULOS");
        Assert.Equal(lanzador, Find());

        // 6. Rodaje complementos (%LOCALAPPDATA%\Rodaje\complementos\modulos)
        File.Delete(lanzador);
        Assert.Equal(rodajeLocal, Find());

        // 7. RODAJE_MODULOS
        File.Delete(rodajeLocal);
        Assert.Equal(rodajeEnv, Find());

        // 8. modulos\ junto a la app
        _env.Remove("RODAJE_MODULOS");
        Assert.Equal(appModulos, Find());

        // 9. Raíz de la app
        File.Delete(appModulos);
        Assert.Equal(appDirect, Find());

        // 10. Ninguno: lanza SubtitularUserException
        File.Delete(appDirect);
        Assert.Throws<SubtitularUserException>(() => Find());
    }

    [Fact]
    public void Find_lanzador_picks_newest_version_among_multiple_and_skips_invalid_version_names()
    {
        string v090 = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "0.9.0");
        string v100 = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.0.0");
        string v1100 = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.10.0");
        string v210 = Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "2.1.0");

        // Carpetas que no son versiones válidas según Version.TryParse
        Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "invalid-version");
        Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "1.2.3-beta");
        Put("LocalAppData", "Dathinel", "Comun", "modulos", "modulo-subtitulos", "v2.2.0");

        Assert.Equal(v210, Find());

        File.Delete(v210);
        Assert.Equal(v1100, Find());

        File.Delete(v1100);
        Assert.Equal(v100, Find());

        File.Delete(v100);
        Assert.Equal(v090, Find());
    }

    [Fact]
    public void Find_throws_user_exception_when_custom_path_directory_does_not_contain_exe()
    {
        string emptyCustomDir = Path.Combine(_root, "empty-custom-dir");
        Directory.CreateDirectory(emptyCustomDir);

        SubtitularUserException ex = Assert.Throws<SubtitularUserException>(() => Find(emptyCustomDir));
        Assert.Contains("No se encontró el módulo de subtítulos en:", ex.Message, StringComparison.Ordinal);
        Assert.Contains(emptyCustomDir, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_throws_user_exception_when_custom_path_does_not_exist()
    {
        string nonExistentPath = Path.Combine(_root, "no-existe", Exe);
        SubtitularUserException ex = Assert.Throws<SubtitularUserException>(() => Find(nonExistentPath));
        Assert.Contains("No se encontró el módulo de subtítulos en:", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nonExistentPath, ex.Message, StringComparison.Ordinal);
    }
}
