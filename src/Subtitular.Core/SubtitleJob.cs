using System.Runtime.Versioning;
using System.Text;
using Rodaje.Modules.Protocol;

namespace Subtitular.Core;

/// <summary>Lo que pide el usuario; la CLI y la ventana solo rellenan esto.</summary>
public sealed record JobOptions(string InputFile)
{
    public string Language { get; init; } = "auto";
    public string Format { get; init; } = "srt";
    /// <summary>Carpeta de salida; nula = junto al archivo de entrada.</summary>
    public string? OutputDir { get; init; }
    public string? TranslateTo { get; init; }
    public bool Speak { get; init; }
    public string Device { get; init; } = "auto";
    public string? Model { get; init; }
    public int Threads { get; init; }
    public string? Ffmpeg { get; init; }
    public string? SubtitlesModule { get; init; }
    public string? TranslateModule { get; init; }
    public string? VoiceModule { get; init; }
}

public enum JobStage { Decode, Transcribe, Translate, Speak }

/// <summary>Avance de una etapa y del total (las etapas activas pesan igual).</summary>
public readonly record struct JobProgress(JobStage Stage, double StageFraction, double Overall)
{
    public string Label => Stage switch
    {
        JobStage.Decode => "Decodificando el audio",
        JobStage.Transcribe => "Transcribiendo",
        JobStage.Translate => "Traduciendo",
        _ => "Generando la voz",
    };

    public static JobProgress Of(IReadOnlyList<JobStage> stages, JobStage stage, double fraction)
    {
        ArgumentNullException.ThrowIfNull(stages);
        int index = Math.Max(0, stages.ToList().IndexOf(stage));
        double f = Math.Clamp(fraction, 0, 1);
        return new JobProgress(stage, f, stages.Count == 0 ? f : (index + f) / stages.Count);
    }
}

/// <summary>Decodificar → transcribir → traducir (opcional); aparte, guardar subtítulos y voz (opcional).</summary>
[SupportedOSPlatform("windows")]
public static class SubtitleJob
{
    public static IReadOnlyList<JobStage> StagesOf(JobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<JobStage> stages = [JobStage.Decode, JobStage.Transcribe];
        if (!string.IsNullOrWhiteSpace(options.TranslateTo))
        {
            stages.Add(JobStage.Translate);
        }
        if (options.Speak)
        {
            stages.Add(JobStage.Speak);
        }
        return stages;
    }

    public static string OutputPath(JobOptions options, string extension)
    {
        ArgumentNullException.ThrowIfNull(options);
        string dir = string.IsNullOrWhiteSpace(options.OutputDir) ? Path.GetDirectoryName(Path.GetFullPath(options.InputFile))! : options.OutputDir;
        string name = Path.GetFileNameWithoutExtension(options.InputFile);
        if (!string.IsNullOrWhiteSpace(options.TranslateTo))
        {
            name += "." + options.TranslateTo;
        }
        string path = Path.Combine(dir, name + extension);
        // Un .wav de entrada con «Leer en voz alta» pisaba el audio original; la salida nunca puede ser la entrada.
        return string.Equals(Path.GetFullPath(path), Path.GetFullPath(options.InputFile), StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(dir, name + ".subtitular" + extension)
            : path;
    }

    /// <summary>La transcripción lista para revisar (ya traducida si se pidió).</summary>
    public static async Task<Transcript> RunAsync(JobOptions options, IProgress<JobProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Format is not ("srt" or "vtt"))
        {
            throw new SubtitularUserException($"El formato debe ser «srt» o «vtt», no «{options.Format}».");
        }
        IReadOnlyList<JobStage> stages = StagesOf(options);
        Progress<ModuleProgress> Stage(JobStage s) => new(p => progress?.Report(JobProgress.Of(stages, s, p.Fraction)));

        progress?.Report(JobProgress.Of(stages, JobStage.Decode, 0));
        byte[] audio = await AudioDecoder.DecodeToMemoryAsync(options.InputFile, options.Ffmpeg, cancellationToken).ConfigureAwait(false);
        progress?.Report(JobProgress.Of(stages, JobStage.Decode, 1));

        Transcript transcript = await SubtitleTranscriber.TranscribeAsync(audio,
            new SubtitleParameters(Language: options.Language, Device: options.Device, Model: options.Model, Threads: options.Threads),
            options.SubtitlesModule, Stage(JobStage.Transcribe), cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(options.TranslateTo))
        {
            transcript = await SubtitleTranslator.TranslateAsync(transcript, options.TranslateTo, options.TranslateModule, options.Device,
                Stage(JobStage.Translate), cancellationToken).ConfigureAwait(false);
        }
        return transcript;
    }

    /// <summary>Escribe los subtítulos y, si se pidió, el WAV con la voz. Devuelve las rutas escritas.</summary>
    public static async Task<(string Subtitles, string? Audio)> SaveAsync(Transcript transcript, JobOptions options, IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(options);
        string subtitles = OutputPath(options, "." + options.Format);
        Directory.CreateDirectory(Path.GetDirectoryName(subtitles)!);
        using (StreamWriter writer = new(subtitles, false, new UTF8Encoding(false)))
        {
            if (options.Format == "vtt")
            {
                SubtitleWriter.WriteVtt(transcript, writer);
            }
            else
            {
                SubtitleWriter.WriteSrt(transcript, writer);
            }
        }

        string? wav = null;
        if (options.Speak)
        {
            IReadOnlyList<JobStage> stages = StagesOf(options);
            (float[] audio, int sampleRate) = await SpeechSynthesizer.SpeakAsync(transcript, options.VoiceModule, null,
                new Progress<ModuleProgress>(p => progress?.Report(JobProgress.Of(stages, JobStage.Speak, p.Fraction))), cancellationToken).ConfigureAwait(false);
            wav = OutputPath(options, ".voz.wav");
            SpeechSynthesizer.WriteWav(wav, audio, sampleRate);
        }
        return (subtitles, wav);
    }

    /// <summary>Mensaje en español para la ventana o la consola; nulo si no es un error previsto.</summary>
    public static string? Describe(Exception ex) => ex switch
    {
        OperationCanceledException => "Operación cancelada.",
        SubtitularUserException e => e.Message,
        ModuleCallException e => e.Message,
        ModuleException e => "Error del módulo: " + e.Message,
        _ => null,
    };
}
