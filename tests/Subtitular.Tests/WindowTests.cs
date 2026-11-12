using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class WindowTests
{
    [Fact]
    public void Placement_roundtrips_and_rejects_missing_broken_or_tiny()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-ventana-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "ventana.json");
        try
        {
            Assert.Null(WindowPlacement.Load(path));
            new WindowPlacement(120, 80, 1000, 800).Save(path);
            Assert.Equal(new WindowPlacement(120, 80, 1000, 800), WindowPlacement.Load(path));

            new WindowPlacement(0, 0, 200, 100).Save(path);
            Assert.Null(WindowPlacement.Load(path));

            File.WriteAllText(path, "{ roto");
            Assert.Null(WindowPlacement.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Clock_is_minutes_until_an_hour()
    {
        Assert.Equal("0:05", JobProgress.FormatClock(TimeSpan.FromSeconds(5)));
        Assert.Equal("12:34", JobProgress.FormatClock(new TimeSpan(0, 12, 34)));
        Assert.Equal("1:02:03", JobProgress.FormatClock(new TimeSpan(1, 2, 3)));
        Assert.Equal("0:00", JobProgress.FormatClock(TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void Status_estimates_remaining_only_with_a_base()
    {
        IReadOnlyList<JobStage> stages = [JobStage.Decode, JobStage.Transcribe];
        Assert.Equal("Transcribiendo · 50 % · 0:30 · quedan ~0:10", JobProgress.Of(stages, JobStage.Transcribe, 0.5).Status(TimeSpan.FromSeconds(30)));
        Assert.Equal("Decodificando el audio · 2 % · 0:03", JobProgress.Of(stages, JobStage.Decode, 0.02).Status(TimeSpan.FromSeconds(3)));
    }

    [Theory]
    [InlineData(799, 700, false)]
    [InlineData(800, 699, false)]
    [InlineData(800, 700, true)]
    [InlineData(1920, 1080, true)]
    [InlineData(-800, 700, false)]
    [InlineData(800, -700, false)]
    public void WindowPlacement_boundary_dimensions_validation(int width, int height, bool isValid)
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-win-dim-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "ventana.json");
        try
        {
            Directory.CreateDirectory(dir);
            new WindowPlacement(100, 100, width, height).Save(path);
            WindowPlacement? loaded = WindowPlacement.Load(path);

            if (isValid)
            {
                Assert.NotNull(loaded);
                Assert.Equal(width, loaded.Width);
                Assert.Equal(height, loaded.Height);
            }
            else
            {
                Assert.Null(loaded);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void WindowPlacement_supports_negative_coordinates_for_multi_monitor()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-win-coords-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "ventana.json");
        try
        {
            Directory.CreateDirectory(dir);
            WindowPlacement placement = new(-1920, -50, 1200, 900);
            placement.Save(path);

            WindowPlacement? loaded = WindowPlacement.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(-1920, loaded.X);
            Assert.Equal(-50, loaded.Y);
            Assert.Equal(1200, loaded.Width);
            Assert.Equal(900, loaded.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void WindowPlacement_load_handles_future_unknown_fields_and_empty_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-win-future-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "ventana.json");
        try
        {
            Directory.CreateDirectory(dir);
            string futureJson = """{"x":50,"y":50,"width":1000,"height":800,"isMaximized":true,"screenIndex":1}""";
            File.WriteAllText(path, futureJson);

            WindowPlacement? loaded = WindowPlacement.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(new WindowPlacement(50, 50, 1000, 800), loaded);

            File.WriteAllBytes(path, []);
            Assert.Null(WindowPlacement.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"width":900}""")]
    [InlineData("""{"x":"a","y":0,"width":900,"height":800}""")]
    [InlineData("""{"x":0.5,"y":0,"width":900,"height":800}""")]
    [InlineData("""{"x":0,"y":0,"width":2147483648,"height":800}""")]
    [InlineData("""{"x":0,"y":0,"width":900,"height":null}""")]
    public void WindowPlacement_with_wrong_types_or_out_of_range_numbers_is_ignored(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), "subtitular-win-raro-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "ventana.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            Assert.Null(WindowPlacement.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
