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
        Assert.Equal(Path.Combine(Path.GetTempPath(), "clip.en.wav"), SubtitleJob.OutputPath(new JobOptions(input) { TranslateTo = "en" }, ".wav"));
        Assert.Equal(Path.Combine("C:\\salida", "clip.vtt"), SubtitleJob.OutputPath(new JobOptions(input) { OutputDir = "C:\\salida" }, ".vtt"));
    }

    [Fact]
    public void Describe_turns_known_errors_into_spanish_and_leaves_the_rest()
    {
        Assert.Equal("Operación cancelada.", SubtitleJob.Describe(new OperationCanceledException()));
        Assert.Equal("falta x", SubtitleJob.Describe(new SubtitularUserException("falta x")));
        Assert.Null(SubtitleJob.Describe(new InvalidOperationException("interno")));
    }
}
