using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class BatchTests
{
    private static readonly Transcript Empty = new("es", "cpu", []);

    private static JobOptions Job(string name) => new(Path.Combine(Path.GetTempPath(), name));

    [Fact]
    public async Task Batch_runs_files_in_order_and_reports_progress_per_file()
    {
        List<string> seen = [];
        List<BatchProgress> reports = [];
        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync([Job("a.mp4"), Job("b.mkv")], new Collector(reports), CancellationToken.None, async (o, p, _) =>
        {
            seen.Add(o.InputFile);
            p.Report(JobProgress.Of(SubtitleJob.StagesOf(o), JobStage.Transcribe, 0.5));
            await Task.Yield();
            return (Empty, SubtitleJob.OutputPath(o, ".srt"), null);
        });

        Assert.Equal([Job("a.mp4").InputFile, Job("b.mkv").InputFile], seen);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Ok));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "b.srt"), results[1].Subtitles);

        Assert.Contains(reports, r => r is { Index: 0, Count: 2, Progress: null });
        BatchProgress half = Assert.Single(reports, r => r is { Index: 1, Progress: { Stage: JobStage.Transcribe } });
        Assert.Equal((1 + 0.75) / 2, half.Overall, 3);
        Assert.StartsWith("Archivo 2 de 2 · b.mkv · Transcribiendo · 50 %", half.Status(TimeSpan.FromSeconds(10)), StringComparison.Ordinal);
    }

    /// <summary>Avisos en el mismo hilo, sin el encolado de <see cref="Progress{T}"/>.</summary>
    private sealed class Collector(List<BatchProgress> reports) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => reports.Add(value);
    }

    [Fact]
    public async Task One_failing_file_does_not_stop_the_rest()
    {
        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync([Job("a.mp4"), Job("b.mp4"), Job("c.mp4")], null, CancellationToken.None, (o, _, _) =>
            o.InputFile.EndsWith("b.mp4", StringComparison.Ordinal)
                ? throw new SubtitularUserException("falta el módulo")
                : Task.FromResult((Empty, SubtitleJob.OutputPath(o, ".srt"), (string?)null)));

        Assert.Equal([true, false, true], results.Select(r => r.Ok));
        Assert.Equal("falta el módulo", results[1].Error);
        Assert.Equal("2 archivos listos, 1 con error: b.mp4", JobBatch.Summary(results));
        Assert.Equal("1 archivo listo", JobBatch.Summary([results[0]]));
    }

    [Fact]
    public async Task Cancel_stops_the_queue_at_the_current_file()
    {
        using CancellationTokenSource cts = new();
        int started = 0;
        Task<IReadOnlyList<BatchResult>> run = JobBatch.RunAsync([Job("a.mp4"), Job("b.mp4")], null, cts.Token, async (_, _, token) =>
        {
            started++;
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return (Empty, "", null);
        });
        IReadOnlyList<BatchResult> results = await run;
        Assert.Equal(1, started);
        Assert.All(results, r => Assert.True(r.Cancelled && !r.Ok));
        Assert.Equal("0 de 2 listos, cancelado en a.mp4", JobBatch.Summary(results));
    }

    [Fact]
    public async Task Cancel_keeps_the_files_already_saved()
    {
        using CancellationTokenSource cts = new();
        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync([Job("a.mp4"), Job("b.mp4"), Job("c.mp4")], null, cts.Token, (o, _, token) =>
        {
            if (o.InputFile.EndsWith("b.mp4", StringComparison.Ordinal))
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
            }
            return Task.FromResult((Empty, SubtitleJob.OutputPath(o, ".srt"), (string?)null));
        });
        Assert.Equal([true, false, false], results.Select(r => r.Ok));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "a.srt"), results[0].Subtitles);
        Assert.Equal("1 de 3 listos, cancelado en b.mp4", JobBatch.Summary(results));
    }

    [Fact]
    public void Prepare_drops_repeated_inputs_and_renames_outputs_that_collide()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "subs");
        JobOptions a = new(Path.Combine(Path.GetTempPath(), "a", "clip.mp4")) { OutputDir = outDir };
        JobOptions b = new(Path.Combine(Path.GetTempPath(), "b", "clip.mp4")) { OutputDir = outDir };
        JobOptions c = new(Path.Combine(Path.GetTempPath(), "c", "clip.mkv")) { OutputDir = outDir };
        IReadOnlyList<JobOptions> prepared = JobBatch.Prepare([a, a with { Format = "vtt" }, b, c]);

        Assert.Equal(3, prepared.Count);
        Assert.Equal(Path.Combine(outDir, "clip.srt"), SubtitleJob.OutputPath(prepared[0], ".srt"));
        Assert.Equal(Path.Combine(outDir, "clip-2.srt"), SubtitleJob.OutputPath(prepared[1], ".srt"));
        Assert.Equal(Path.Combine(outDir, "clip-3.srt"), SubtitleJob.OutputPath(prepared[2], ".srt"));
        // Junto al video no chocan: cada uno queda en su carpeta con su nombre.
        Assert.All(JobBatch.Prepare([a with { OutputDir = null }, b with { OutputDir = null }]), j => Assert.Null(j.OutputName));
    }

    [Fact]
    public void Single_file_status_has_no_queue_prefix_and_starts_as_preparing()
    {
        BatchProgress preparing = new(0, 1, "clip.mp4", null);
        Assert.Equal("Preparando · 0:03", preparing.Status(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, preparing.Overall);
        JobProgress p = JobProgress.Of([JobStage.Decode, JobStage.Transcribe], JobStage.Transcribe, 0.5);
        Assert.Equal(p.Status(TimeSpan.FromSeconds(30)), new BatchProgress(0, 1, "clip.mp4", p).Status(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Result_folder_opens_the_file_selected_or_the_common_folder()
    {
        string dir = Path.GetTempPath();
        string srt = Path.Combine(dir, "clip.srt");
        string wav = Path.Combine(dir, "clip.voz.wav");
        Assert.Null(ResultFolder.Of([]));
        Assert.Null(ResultFolder.ExplorerCommand([null]));
        Assert.Equal(Path.TrimEndingDirectorySeparator(dir), ResultFolder.Of([srt, null, wav]));
        Assert.Equal<(string, string)?>(("explorer.exe", "/select,\"" + srt + "\""), ResultFolder.ExplorerCommand([srt, null]));
        Assert.Equal<(string, string)?>(("explorer.exe", "\"" + Path.TrimEndingDirectorySeparator(dir) + "\""), ResultFolder.ExplorerCommand([srt, wav]));
    }
}
