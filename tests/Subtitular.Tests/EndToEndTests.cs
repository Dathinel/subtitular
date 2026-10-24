using System.Diagnostics;
using System.Text;
using Subtitular.Core;
using Xunit.Abstractions;

namespace Subtitular.Tests;

/// <summary>Prueba larga (RODAJE_PRUEBAS_LARGAS=1): clip del corpus → SRT + traducción al inglés + WAV, con tiempos por etapa.</summary>
public sealed class EndToEndTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Corpus_clip_to_srt_english_and_wav()
    {
        if (Environment.GetEnvironmentVariable("RODAJE_PRUEBAS_LARGAS") != "1")
        {
            output.WriteLine("Prueba larga omitida porque RODAJE_PRUEBAS_LARGAS != 1.");
            return;
        }
        string? env = Environment.GetEnvironmentVariable("RODAJE_ENTORNO");
        string tsv = Path.Combine(env ?? "", "corpus", "fleurs-es", "dev.tsv");
        if (env is null || !File.Exists(tsv))
        {
            output.WriteLine("Prueba larga omitida: falta el corpus FLEURS.");
            return;
        }

        string[] first = File.ReadLines(tsv, Encoding.UTF8).Select(l => l.Split('\t')).First(c => c.Length >= 3);
        string clip = Path.Combine(env, "corpus", "fleurs-es", "dev", first[1]);
        string outDir = Path.Combine(Path.GetTempPath(), "subtitular-e2e-" + Guid.NewGuid().ToString("N"));
        JobOptions options = new(clip) { Language = "es", TranslateTo = "en", Speak = true, OutputDir = outDir };

        Stopwatch total = Stopwatch.StartNew();
        List<(JobStage Stage, TimeSpan At)> starts = [];
        Progress<JobProgress> progress = new(p =>
        {
            if (starts.Count == 0 || starts[^1].Stage != p.Stage)
            {
                starts.Add((p.Stage, total.Elapsed));
            }
        });

        Transcript transcript = await SubtitleJob.RunAsync(options, progress);
        TimeSpan run = total.Elapsed;
        (string srt, string? wav) = await SubtitleJob.SaveAsync(transcript, options, progress);
        total.Stop();

        output.WriteLine($"Clip: {first[1]} ({first[2]})");
        output.WriteLine($"Traducción: {string.Join(" ", transcript.Segments.Select(s => s.Text))}");
        output.WriteLine($"Decodificar + transcribir + traducir: {run.TotalSeconds:0.00} s; total con voz: {total.Elapsed.TotalSeconds:0.00} s");
        for (int i = 0; i < starts.Count; i++)
        {
            TimeSpan end = i + 1 < starts.Count ? starts[i + 1].At : total.Elapsed;
            output.WriteLine($"  {starts[i].Stage}: {(end - starts[i].At).TotalSeconds:0.00} s");
        }

        Assert.Equal("en", transcript.Language);
        Assert.True(File.Exists(srt), srt);
        Assert.NotNull(wav);
        Assert.True(new FileInfo(wav).Length > 44 + 24000 * 2, "El WAV debe tener al menos un segundo de audio.");
        Assert.Contains("-->", File.ReadAllText(srt), StringComparison.Ordinal);
        Assert.DoesNotContain(transcript.Segments, s => s.Text.Contains(" el ", StringComparison.Ordinal));
        Directory.Delete(outDir, recursive: true);
    }
}
