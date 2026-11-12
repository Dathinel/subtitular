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
    public async Task Cancel_before_starting_cancels_all_jobs_without_invoking_worker()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        int workerInvocations = 0;

        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync(
            [Job("a.mp4"), Job("b.mp4"), Job("c.mp4")],
            null,
            cts.Token,
            (o, _, _) =>
            {
                workerInvocations++;
                return Task.FromResult((Empty, "", (string?)null));
            });

        Assert.Equal(0, workerInvocations);
        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.False(r.Ok);
            Assert.True(r.Cancelled);
            Assert.Null(r.Error);
        });
        Assert.Equal("0 de 3 listos, cancelado en a.mp4", JobBatch.Summary(results));
    }

    [Fact]
    public async Task Cancel_midway_marks_in_progress_and_remaining_jobs_cancelled()
    {
        using CancellationTokenSource cts = new();
        List<string> processed = [];

        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync(
            [Job("a.mp4"), Job("b.mp4"), Job("c.mp4"), Job("d.mp4")],
            null,
            cts.Token,
            (o, _, token) =>
            {
                processed.Add(Path.GetFileName(o.InputFile));
                if (o.InputFile.EndsWith("b.mp4", StringComparison.Ordinal))
                {
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                return Task.FromResult((Empty, SubtitleJob.OutputPath(o, ".srt"), (string?)null));
            });

        Assert.Equal(["a.mp4", "b.mp4"], processed);
        Assert.Equal(4, results.Count);

        Assert.True(results[0].Ok);
        Assert.False(results[0].Cancelled);

        Assert.False(results[1].Ok);
        Assert.True(results[1].Cancelled);

        Assert.False(results[2].Ok);
        Assert.True(results[2].Cancelled);

        Assert.False(results[3].Ok);
        Assert.True(results[3].Cancelled);

        Assert.Equal("1 de 4 listos, cancelado en b.mp4", JobBatch.Summary(results));
    }

    [Fact]
    public async Task Failure_in_second_of_three_does_not_stop_third_job()
    {
        List<string> executed = [];

        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync(
            [Job("p1.mp4"), Job("p2.mp4"), Job("p3.mp4")],
            null,
            CancellationToken.None,
            (o, _, _) =>
            {
                executed.Add(Path.GetFileName(o.InputFile));
                if (o.InputFile.EndsWith("p2.mp4", StringComparison.Ordinal))
                {
                    throw new SubtitularUserException("fallo de transcripción");
                }
                return Task.FromResult((Empty, SubtitleJob.OutputPath(o, ".srt"), (string?)null));
            });

        Assert.Equal(["p1.mp4", "p2.mp4", "p3.mp4"], executed);
        Assert.Equal(3, results.Count);

        Assert.True(results[0].Ok);
        Assert.Null(results[0].Error);

        Assert.False(results[1].Ok);
        Assert.Equal("fallo de transcripción", results[1].Error);

        Assert.True(results[2].Ok);
        Assert.Null(results[2].Error);

        Assert.Equal("2 archivos listos, 1 con error: p2.mp4", JobBatch.Summary(results));
    }

    [Fact]
    public async Task Multiple_failing_files_report_all_in_summary()
    {
        IReadOnlyList<BatchResult> results = await JobBatch.RunAsync(
            [Job("f1.mp4"), Job("f2.mp4"), Job("f3.mp4"), Job("f4.mp4")],
            null,
            CancellationToken.None,
            (o, _, _) =>
            {
                if (o.InputFile.EndsWith("f2.mp4", StringComparison.Ordinal) || o.InputFile.EndsWith("f3.mp4", StringComparison.Ordinal))
                {
                    throw new SubtitularUserException("error procesando");
                }
                return Task.FromResult((Empty, SubtitleJob.OutputPath(o, ".srt"), (string?)null));
            });

        Assert.Equal([true, false, false, true], results.Select(r => r.Ok));
        Assert.Equal("2 archivos listos, 2 con error: f2.mp4, f3.mp4", JobBatch.Summary(results));

        // Todos con error
        IReadOnlyList<BatchResult> allFailed = await JobBatch.RunAsync(
            [Job("err1.mp4"), Job("err2.mp4")],
            null,
            CancellationToken.None,
            (o, _, _) => throw new InvalidOperationException("crash"));

        Assert.All(allFailed, r => Assert.False(r.Ok));
        Assert.Equal("0 archivos listos, 2 con error: err1.mp4, err2.mp4", JobBatch.Summary(allFailed));
    }

    [Fact]
    public void Prepare_deduplicates_same_input_file_with_different_casing_or_slashes()
    {
        string p1 = Path.Combine(Path.GetTempPath(), "test_case", "clip.mp4");
        string p2 = Path.Combine(Path.GetTempPath(), "TEST_CASE", "CLIP.MP4");
        string p3 = Path.GetTempPath().Replace('\\', '/') + "test_case/clip.mp4";

        IReadOnlyList<JobOptions> prepared = JobBatch.Prepare([new JobOptions(p1), new JobOptions(p2), new JobOptions(p3)]);

        Assert.Single(prepared);
        Assert.Equal(Path.GetFullPath(p1), Path.GetFullPath(prepared[0].InputFile));
    }

    [Fact]
    public void Batch_null_argument_checks()
    {
        Assert.Throws<ArgumentNullException>(() => JobBatch.Summary(null!));
        Assert.Throws<ArgumentNullException>(() => JobBatch.Prepare(null!));
    }
}
