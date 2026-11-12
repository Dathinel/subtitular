using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class JobTests
{
    [Fact]
    public void Progress_weighs_active_stages_equally()
    {
        JobOptions options = new("a.mp4") { TranslateTo = "en" };
        IReadOnlyList<JobStage> stages = SubtitleJob.StagesOf(options);
        Assert.Equal([JobStage.Decode, JobStage.Transcribe, JobStage.Translate], stages);

        JobProgress p = JobProgress.Of(stages, JobStage.Transcribe, 0.5);
        Assert.Equal(0.5, p.Overall, 3);
        Assert.Equal("Transcribiendo", p.Label);
        Assert.Equal(1.0, JobProgress.Of(stages, JobStage.Translate, 1).Overall, 3);
        Assert.Equal(0.0, JobProgress.Of(stages, JobStage.Decode, -1).Overall, 3);
    }

    [Fact]
    public void Stages_without_options_are_decode_and_transcribe()
    {
        Assert.Equal([JobStage.Decode, JobStage.Transcribe], SubtitleJob.StagesOf(new JobOptions("a.mp4")));
        Assert.Contains(JobStage.Speak, SubtitleJob.StagesOf(new JobOptions("a.mp4") { Speak = true }));
    }

    [Fact]
    public void Output_path_goes_next_to_input_with_language_suffix()
    {
        string input = Path.Combine(Path.GetTempPath(), "clip.mp4");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "clip.srt"), SubtitleJob.OutputPath(new JobOptions(input), ".srt"));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "clip.en.voz.wav"), SubtitleJob.OutputPath(new JobOptions(input) { TranslateTo = "en" }, ".voz.wav"));
        Assert.Equal(Path.Combine("C:\\salida", "clip.vtt"), SubtitleJob.OutputPath(new JobOptions(input) { OutputDir = "C:\\salida" }, ".vtt"));
    }

    [Fact]
    public void Output_path_never_overwrites_the_input()
    {
        string wav = Path.Combine(Path.GetTempPath(), "voz.wav");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "voz.voz.wav"), SubtitleJob.OutputPath(new JobOptions(wav), ".voz.wav"));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "voz.subtitular.wav"), SubtitleJob.OutputPath(new JobOptions(wav), ".wav"));
        string srt = Path.Combine(Path.GetTempPath(), "clip.srt");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "clip.subtitular.srt"), SubtitleJob.OutputPath(new JobOptions(srt), ".srt"));
        Assert.Equal(Path.Combine(Path.GetTempPath(), "clip.en.srt"), SubtitleJob.OutputPath(new JobOptions(srt) { TranslateTo = "en" }, ".srt"));
    }

    [Fact]
    public void Describe_turns_known_errors_into_spanish_and_leaves_the_rest()
    {
        Assert.Equal("Operación cancelada.", SubtitleJob.Describe(new OperationCanceledException()));
        Assert.Equal("falta x", SubtitleJob.Describe(new SubtitularUserException("falta x")));
        Assert.Null(SubtitleJob.Describe(new InvalidOperationException("interno")));
    }

    [Fact]
    public void Prepare_handles_multiple_collisions_with_progressive_suffixes()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "subs-colisiones");
        List<JobOptions> inputs = [];
        for (int i = 1; i <= 5; i++)
        {
            inputs.Add(new JobOptions(Path.Combine(Path.GetTempPath(), $"dir{i}", "video.mp4")) { OutputDir = outDir });
        }

        IReadOnlyList<JobOptions> prepared = JobBatch.Prepare(inputs);

        Assert.Equal(5, prepared.Count);
        Assert.Equal(Path.Combine(outDir, "video.srt"), SubtitleJob.OutputPath(prepared[0], ".srt"));
        Assert.Equal(Path.Combine(outDir, "video-2.srt"), SubtitleJob.OutputPath(prepared[1], ".srt"));
        Assert.Equal(Path.Combine(outDir, "video-3.srt"), SubtitleJob.OutputPath(prepared[2], ".srt"));
        Assert.Equal(Path.Combine(outDir, "video-4.srt"), SubtitleJob.OutputPath(prepared[3], ".srt"));
        Assert.Equal(Path.Combine(outDir, "video-5.srt"), SubtitleJob.OutputPath(prepared[4], ".srt"));
    }

    [Fact]
    public void Output_path_with_multiple_dots_in_filename()
    {
        string input = Path.Combine(Path.GetTempPath(), "pelicula.2026.1080p.web-dl.x264.mkv");
        JobOptions options = new(input);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "pelicula.2026.1080p.web-dl.x264.srt"), SubtitleJob.OutputPath(options, ".srt"));

        JobOptions withTrans = options with { TranslateTo = "en" };
        Assert.Equal(Path.Combine(Path.GetTempPath(), "pelicula.2026.1080p.web-dl.x264.en.vtt"), SubtitleJob.OutputPath(withTrans, ".vtt"));
    }

    [Fact]
    public void Output_path_for_an_input_without_extension()
    {
        string noExt = Path.Combine(Path.GetTempPath(), "archivo_sin_extension");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "archivo_sin_extension.srt"), SubtitleJob.OutputPath(new JobOptions(noExt), ".srt"));
    }

    [Fact]
    public void Two_distinct_inputs_with_different_extensions_in_same_folder_resolve_cleanly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mismo-nombre");
        JobOptions mp4 = new(Path.Combine(dir, "clip.mp4"));
        JobOptions mkv = new(Path.Combine(dir, "clip.mkv"));

        IReadOnlyList<JobOptions> prepared = JobBatch.Prepare([mp4, mkv]);

        Assert.Equal(2, prepared.Count);
        Assert.Equal(Path.Combine(dir, "clip.srt"), SubtitleJob.OutputPath(prepared[0], ".srt"));
        Assert.Equal(Path.Combine(dir, "clip-2.srt"), SubtitleJob.OutputPath(prepared[1], ".srt"));
    }

    [Fact]
    public async Task SaveAsync_creates_non_existent_output_directory()
    {
        string baseDir = Directory.CreateTempSubdirectory("subtitular-outdir-").FullName;
        try
        {
            string nestedDir = Path.Combine(baseDir, "nivel1", "nivel2", "nivel3");
            Assert.False(Directory.Exists(nestedDir));

            Transcript transcript = new("es", "cpu", [new TranscriptSegment(0, 2, "Test.")]);
            JobOptions options = new(Path.Combine(baseDir, "video.mp4")) { OutputDir = nestedDir };

            (string subtitles, _) = await SubtitleJob.SaveAsync(transcript, options);

            Assert.True(File.Exists(subtitles));
            Assert.Equal(Path.Combine(nestedDir, "video.srt"), subtitles);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_rejects_invalid_format_with_user_exception()
    {
        JobOptions invalid = new("video.mp4") { Format = "sub" };
        SubtitularUserException ex = await Assert.ThrowsAsync<SubtitularUserException>(() => SubtitleJob.RunAsync(invalid));
        Assert.Contains("El formato debe ser «srt» o «vtt», no «sub».", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputPath_and_StagesOf_throw_on_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => SubtitleJob.OutputPath(null!, ".srt"));
        Assert.Throws<ArgumentNullException>(() => SubtitleJob.StagesOf(null!));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(7325, "2:02:05")]
    [InlineData(86399, "23:59:59")]
    [InlineData(86400, "24:00:00")]
    [InlineData(90061, "25:01:01")]
    [InlineData(-10, "0:00")]
    public void FormatClock_formats_seconds_and_hours_accurately(int seconds, string expected)
    {
        Assert.Equal(expected, JobProgress.FormatClock(TimeSpan.FromSeconds(seconds)));
    }
    [Fact]
    public void Remaining_time_over_a_day_keeps_its_hours()
    {
        // Una cola larga: 25 % en 10 h, faltan 30 h (no «6:00:00»).
        JobProgress p = JobProgress.Of([JobStage.Transcribe], JobStage.Transcribe, 0.25);
        Assert.Equal("Transcribiendo · 25 % · 10:00:00 · quedan ~30:00:00", p.Status(TimeSpan.FromHours(10)));
    }

    [Fact]
    public void Prepare_never_gives_two_jobs_the_same_output_even_if_a_name_already_ends_in_a_suffix()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "subs-sufijos");
        string[] inputs = [@"a\clip.mp4", @"b\clip.mp4", @"c\clip-2.mp4", @"d\clip-2.mkv", @"e\CLIP.mov"];

        IReadOnlyList<JobOptions> prepared = JobBatch.Prepare([.. inputs.Select(i => new JobOptions(Path.Combine(Path.GetTempPath(), i)) { OutputDir = outDir })]);

        Assert.Equal(inputs.Length, prepared.Count);
        Assert.Equal(inputs.Length, prepared.Select(j => SubtitleJob.OutputPath(j, ".srt")).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Path.Combine(outDir, "clip.srt"), SubtitleJob.OutputPath(prepared[0], ".srt"));
    }
}
