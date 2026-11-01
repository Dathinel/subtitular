using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class PreferencesTests
{
    [Fact]
    public void Preferences_roundtrip_keep_missing_folders_and_survive_broken_files()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "preferencias.json");
        try
        {
            Preferences defaults = Preferences.Load(path);
            Assert.Equal(new Preferences(), defaults);
            Assert.Equal("auto", defaults.Language);
            Assert.True(defaults.OpenFolder);

            Directory.CreateDirectory(dir);
            Preferences chosen = new() { LastInputDir = dir, OutputDir = dir, Language = "es", Format = "vtt", TranslateTo = "en", Speak = true, OpenFolder = false };
            chosen.Save(path);
            Assert.Equal(chosen, Preferences.Load(path));

            // Una carpeta que no existe ahora (disco desconectado) se conserva; un formato o idioma raros vuelven a los de fábrica.
            string gone = Path.Combine(dir, "borrada");
            new Preferences { LastInputDir = gone, OutputDir = gone, Language = " ", Format = "ass", TranslateTo = "" }.Save(path);
            Preferences loaded = Preferences.Load(path);
            Assert.Equal(gone, loaded.LastInputDir);
            Assert.Equal(gone, loaded.OutputDir);
            Assert.Equal("auto", loaded.Language);
            Assert.Equal("srt", loaded.Format);
            Assert.Null(loaded.TranslateTo);

            File.WriteAllText(path, "{ roto");
            Assert.Equal(new Preferences(), Preferences.Load(path));

            // Un archivo viejo sin la casilla nueva la deja activada.
            File.WriteAllText(path, """{"language":"en"}""");
            Assert.True(Preferences.Load(path).OpenFolder);
            Assert.Equal("en", Preferences.Load(path).Language);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
