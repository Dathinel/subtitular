using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class PreferencesTests
{
    [Fact]
    public void Preferences_roundtrip_and_forget_missing_folders_and_broken_files()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "preferencias.json");
        try
        {
            Preferences defaults = Preferences.Load(path);
            Assert.Equal(new Preferences(), defaults);
            Assert.Equal("auto", defaults.Language);
            Assert.True(defaults.OpenFolder);
            Assert.True(defaults.CheckUpdates);
            Assert.Null(defaults.LastUpdateCheck);
            Assert.Null(defaults.SkippedVersion);

            Directory.CreateDirectory(dir);
            Preferences chosen = new()
            {
                LastInputDir = dir, OutputDir = dir, Language = "es", Format = "vtt", TranslateTo = "en", Speak = true, OpenFolder = false,
                CheckUpdates = false, LastUpdateCheck = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.FromHours(-5)), SkippedVersion = "1.1.0",
            };
            chosen.Save(path);
            Assert.Equal(chosen, Preferences.Load(path));

            // Una carpeta que ya no existe no se recuerda; un formato o idioma raros vuelven a los de fábrica.
            string gone = Path.Combine(dir, "borrada");
            new Preferences { LastInputDir = gone, OutputDir = gone, Language = " ", Format = "ass", TranslateTo = "", SkippedVersion = " " }.Save(path);
            Preferences loaded = Preferences.Load(path);
            Assert.Null(loaded.LastInputDir);
            Assert.Null(loaded.OutputDir);
            Assert.Equal("auto", loaded.Language);
            Assert.Equal("srt", loaded.Format);
            Assert.Null(loaded.TranslateTo);
            Assert.Null(loaded.SkippedVersion);

            File.WriteAllText(path, "{ roto");
            Assert.Equal(new Preferences(), Preferences.Load(path));

            // Un archivo viejo sin las casillas nuevas las deja activadas, y el aviso de versión toca al arrancar.
            File.WriteAllText(path, """{"language":"en"}""");
            Assert.True(Preferences.Load(path).OpenFolder);
            Assert.True(Preferences.Load(path).CheckUpdates);
            Assert.Null(Preferences.Load(path).LastUpdateCheck);
            Assert.Equal("en", Preferences.Load(path).Language);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
