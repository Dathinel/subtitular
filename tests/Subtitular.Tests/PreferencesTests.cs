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
            Assert.False(defaults.SaveWithoutReview);
            Assert.True(defaults.CheckUpdates);
            Assert.Null(defaults.LastUpdateCheck);
            Assert.Null(defaults.SkippedVersion);

            Directory.CreateDirectory(dir);
            Preferences chosen = new()
            {
                LastInputDir = dir, OutputDir = dir, Language = "es", Format = "vtt", TranslateTo = "en", Speak = true, OpenFolder = false,
                SaveWithoutReview = true, CheckUpdates = false, LastUpdateCheck = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.FromHours(-5)), SkippedVersion = "1.1.0",
            };
            chosen.Save(path);
            Assert.Equal(chosen, Preferences.Load(path));
            // Escritura atómica: se escribe un .tmp y se renombra encima; no queda rastro.
            Assert.False(File.Exists(path + ".tmp"));

            // Una carpeta que no existe ahora (disco desconectado) se conserva; un formato o idioma raros vuelven a los de fábrica.
            string gone = Path.Combine(dir, "borrada");
            new Preferences { LastInputDir = gone, OutputDir = gone, Language = " ", Format = "ass", TranslateTo = "", SkippedVersion = " " }.Save(path);
            Preferences loaded = Preferences.Load(path);
            Assert.Equal(gone, loaded.LastInputDir);
            Assert.Equal(gone, loaded.OutputDir);
            Assert.Equal("auto", loaded.Language);
            Assert.Equal("srt", loaded.Format);
            Assert.Null(loaded.TranslateTo);
            Assert.Null(loaded.SkippedVersion);

            File.WriteAllText(path, "{ roto");
            Assert.Equal(new Preferences(), Preferences.Load(path));

            // Un archivo viejo sin las casillas nuevas las deja activadas, y el aviso de versión toca al arrancar.
            File.WriteAllText(path, """{"language":"en"}""");
            Assert.True(Preferences.Load(path).OpenFolder);
            Assert.False(Preferences.Load(path).SaveWithoutReview);
            Assert.True(Preferences.Load(path).CheckUpdates);
            Assert.Null(Preferences.Load(path).LastUpdateCheck);
            Assert.Equal("en", Preferences.Load(path).Language);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Saving_where_it_cannot_write_keeps_the_old_file_and_does_not_throw()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-" + Guid.NewGuid().ToString("N"));
        try
        {
            // La ruta es una carpeta: no se puede escribir ahí; el guardado calla y no deja un .tmp.
            Directory.CreateDirectory(Path.Combine(dir, "preferencias.json"));
            new Preferences { Language = "es" }.Save(Path.Combine(dir, "preferencias.json"));
            Assert.False(File.Exists(Path.Combine(dir, "preferencias.json.tmp")));
            Assert.Equal(new Preferences(), Preferences.Load(Path.Combine(dir, "preferencias.json")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Saving_retries_when_destination_is_temporarily_locked()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "preferencias.json");
        try
        {
            Directory.CreateDirectory(dir);
            new Preferences { Language = "es" }.Save(path);

            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Task releaseTask = Task.Run(async () =>
            {
                await Task.Delay(120);
                stream.Dispose();
            });

            new Preferences { Language = "en" }.Save(path);
            await releaseTask;

            Assert.Equal("en", Preferences.Load(path).Language);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void One_file_is_reviewed_unless_told_otherwise_and_queues_never_are()
    {
        Assert.True(SubtitleJob.ReviewsBeforeSaving(1, saveWithoutReview: false));
        Assert.False(SubtitleJob.ReviewsBeforeSaving(1, saveWithoutReview: true));
        Assert.False(SubtitleJob.ReviewsBeforeSaving(3, saveWithoutReview: false));
    }

    [Fact]
    public void Preferences_load_handles_absent_empty_and_corrupt_files()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-edge-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string absent = Path.Combine(dir, "no-existe.json");
            Assert.Equal(new Preferences(), Preferences.Load(absent));

            string empty = Path.Combine(dir, "vacio.json");
            File.WriteAllBytes(empty, []);
            Assert.Equal(new Preferences(), Preferences.Load(empty));

            string corrupt = Path.Combine(dir, "corrupto.json");
            File.WriteAllText(corrupt, "{ formato: invalido ");
            Assert.Equal(new Preferences(), Preferences.Load(corrupt));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Preferences_load_preserves_known_properties_when_future_unknown_fields_exist()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-future-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "preferencias.json");
            string futureJson = """
            {
                "language": "pt",
                "format": "vtt",
                "openFolder": false,
                "saveWithoutReview": true,
                "futurePropertyA": 12345,
                "futureObjectB": { "test": true }
            }
            """;
            File.WriteAllText(path, futureJson);

            Preferences loaded = Preferences.Load(path);
            Assert.Equal("pt", loaded.Language);
            Assert.Equal("vtt", loaded.Format);
            Assert.False(loaded.OpenFolder);
            Assert.True(loaded.SaveWithoutReview);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Preferences_load_normalizes_invalid_format_and_whitespace()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-norm-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "preferencias.json");
            string json = """
            {
                "language": "   ",
                "format": "sub",
                "lastInputDir": "  \t  ",
                "outputDir": "",
                "translateTo": "   ",
                "skippedVersion": " "
            }
            """;
            File.WriteAllText(path, json);

            Preferences loaded = Preferences.Load(path);
            Assert.Equal("auto", loaded.Language);
            Assert.Equal("srt", loaded.Format);
            Assert.Null(loaded.LastInputDir);
            Assert.Null(loaded.OutputDir);
            Assert.Null(loaded.TranslateTo);
            Assert.Null(loaded.SkippedVersion);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Preferences_save_creates_directory_structure_if_missing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-mkdir-" + Guid.NewGuid().ToString("N"));
        try
        {
            string nested = Path.Combine(dir, "sub1", "sub2", "preferencias.json");
            Assert.False(Directory.Exists(Path.GetDirectoryName(nested)));

            Preferences prefs = new() { Language = "en", Format = "vtt" };
            prefs.Save(nested);

            Assert.True(File.Exists(nested));
            Preferences loaded = Preferences.Load(nested);
            Assert.Equal("en", loaded.Language);
            Assert.Equal("vtt", loaded.Format);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"texto\"")]
    [InlineData("""{"speak":"sí"}""")]
    [InlineData("""{"openFolder":null}""")]
    [InlineData("""{"language":5}""")]
    [InlineData("""{"language":null,"format":null}""")]
    [InlineData("""{"lastUpdateCheck":"ayer"}""")]
    [InlineData("""{"lastUpdateCheck":"0001-01-01T00:00:00+14:00"}""")]
    [InlineData("""{"lastUpdateCheck":"9999-12-31T23:59:59-14:00"}""")]
    public void Preferences_with_wrong_types_or_out_of_range_values_fall_back_to_defaults(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-prefs-raro-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "preferencias.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            Assert.Equal(new Preferences(), Preferences.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
