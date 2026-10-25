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
}
