using System.Diagnostics;
using System.Text;
using Subtitular.Core;
using Xunit.Abstractions;

namespace Subtitular.Tests;

public sealed class CorpusTests(ITestOutputHelper output)
{
    private static string? EnvironmentDir
    {
        get
        {
            if (Environment.GetEnvironmentVariable("RODAJE_ENTORNO") is { Length: > 0 } env && Directory.Exists(env))
            {
                return env;
            }

            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, ".entorno");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    private static string? FleursDevDir
    {
        get
        {
            if (EnvironmentDir is { } env)
            {
                string d = Path.Combine(env, "corpus", "fleurs-es");
                if (Directory.Exists(Path.Combine(d, "dev")) && File.Exists(Path.Combine(d, "dev.tsv")))
                {
                    return d;
                }
            }
            return null;
        }
    }

    private static string? WhisperModelPath
    {
        get
        {
            if (EnvironmentDir is { } env)
            {
                string model = Path.Combine(env, "modelos", "ggml-large-v3-turbo-q5_0.bin");
                if (File.Exists(model))
                {
                    return model;
                }
                string small = Path.Combine(env, "modelos", "ggml-small-q5_1.bin");
                if (File.Exists(small))
                {
                    return small;
                }
            }
            return null;
        }
    }

    [Fact]
    public async Task Transcribe_corpus_audio_has_wer_under_15_percent()
    {
        if (Environment.GetEnvironmentVariable("RODAJE_PRUEBAS_LARGAS") != "1")
        {
            output.WriteLine("Prueba larga omitida porque RODAJE_PRUEBAS_LARGAS != 1.");
            return;
        }

        string? fleursDir = FleursDevDir;
        if (fleursDir is null)
        {
            output.WriteLine("Prueba larga omitida: falta el corpus FLEURS en .entorno\\corpus\\fleurs-es.");
            return;
        }

        string? modelPath = WhisperModelPath;
        if (modelPath is null)
        {
            output.WriteLine("Prueba larga omitida: falta el modelo Whisper en .entorno\\modelos.");
            return;
        }

        string tsvPath = Path.Combine(fleursDir, "dev.tsv");
        string devAudioDir = Path.Combine(fleursDir, "dev");

        List<byte> audioBytes = [];
        List<string> referenceTexts = [];
        HashSet<string> seenIds = new(StringComparer.Ordinal);

        // Reunir ~60 segundos de audio del corpus
        int targetSeconds = 60;
        int bytesPerSecond = AudioDecoder.TargetChannels * 4 * AudioDecoder.TargetSampleRate;

        foreach (string line in File.ReadLines(tsvPath, Encoding.UTF8))
        {
            string[] cols = line.Split('\t');
            if (cols.Length < 3)
            {
                continue;
            }

            string id = cols[0];
            string wavName = cols[1];
            string text = cols[2];
            string wavPath = Path.Combine(devAudioDir, wavName);

            if (!File.Exists(wavPath) || !seenIds.Add(id))
            {
                continue;
            }

            byte[] decoded = await AudioDecoder.DecodeToMemoryAsync(wavPath);
            audioBytes.AddRange(decoded);
            referenceTexts.Add(text);

            if (audioBytes.Count >= targetSeconds * bytesPerSecond)
            {
                break;
            }
        }

        Assert.NotEmpty(audioBytes);
        double audioDurationSec = (double)audioBytes.Count / bytesPerSecond;
        output.WriteLine($"Audio del corpus acumulado: {audioDurationSec:0.00} s ({audioBytes.Count} bytes)");

        SubtitleParameters parameters = new(
            Language: "es",
            Device: "auto",
            Model: modelPath);

        Stopwatch watch = Stopwatch.StartNew();
        Transcript transcript = await SubtitleTranscriber.TranscribeAsync(
            audioBytes.ToArray(),
            parameters);
        watch.Stop();

        double elapsedSec = watch.Elapsed.TotalSeconds;
        double rtf = elapsedSec / audioDurationSec;
        output.WriteLine($"Tiempo de transcripción: {elapsedSec:0.00} s ({watch.ElapsedMilliseconds} ms)");
        output.WriteLine($"Factor de tiempo real (RTF): {rtf:0.000}x ({(audioDurationSec / elapsedSec):0.0}x velocidad real)");

        string fullReference = string.Join(" ", referenceTexts);
        string fullHypothesis = string.Join(" ", transcript.Segments.Select(s => s.Text));

        output.WriteLine($"Referencia: {fullReference}");
        output.WriteLine($"Hipótesis:  {fullHypothesis}");

        string[] refWords = Normalize(fullReference);
        string[] hypWords = Normalize(fullHypothesis);

        double wer = ComputeWer(refWords, hypWords);
        output.WriteLine($"WER obtenido: {wer:P2} ({wer * 100:0.0}%)");

        Assert.True(wer < 0.15, $"El WER {wer:P2} es mayor o igual al 15% esperado.");
    }

    public static string[] Normalize(string text)
    {
        StringBuilder sb = new(text.Length);
        foreach (char c in text.ToLowerInvariant().Normalize(NormalizationForm.FormC))
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static double ComputeWer(IReadOnlyList<string> reference, IReadOnlyList<string> hypothesis)
    {
        int n = reference.Count;
        int m = hypothesis.Count;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }
        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = reference[i - 1] == hypothesis[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    d[i - 1, j] + 1, // eliminación
                    Math.Min(
                        d[i, j - 1] + 1, // inserción
                        d[i - 1, j - 1] + cost)); // sustitución
            }
        }

        return n == 0 ? 0 : (double)d[n, m] / n;
    }
}
