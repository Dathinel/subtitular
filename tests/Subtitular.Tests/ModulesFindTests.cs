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
}
