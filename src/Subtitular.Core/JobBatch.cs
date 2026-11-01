using System.Runtime.Versioning;

namespace Subtitular.Core;

/// <summary>Avance de la cola: qué archivo va (desde 0), cuántos hay y, si ya avisó, la etapa de ese archivo.</summary>
public readonly record struct BatchProgress(int Index, int Count, string InputFile, JobProgress? Progress)
{
    /// <summary>Fracción del total de la cola: cada archivo pesa igual.</summary>
    public double Overall => Count <= 0 ? 0 : (Index + (Progress?.Overall ?? 0)) / Count;

    /// <summary>Con varios archivos, «Archivo 2 de 5 · clip.mp4 · Transcribiendo · 45 % · 0:12 · quedan ~0:15»; con uno, solo la etapa.</summary>
    public string Status(TimeSpan elapsed)
    {
        string stage = Progress is { } p ? p.Status(elapsed, Overall) : "Preparando · " + JobProgress.FormatClock(elapsed);
        return Count > 1 ? $"Archivo {Index + 1} de {Count} · {Path.GetFileName(InputFile)} · {stage}" : stage;
    }
}

/// <summary>Cómo acabó un archivo de la cola: sus salidas, su error (en español si era previsto) o que se canceló antes de terminar.</summary>
public sealed record BatchResult(JobOptions Options, Transcript? Transcript, string? Subtitles, string? Audio, string? Error, bool Cancelled = false)
{
    public bool Ok => Error is null && !Cancelled;
}

/// <summary>Varios archivos uno tras otro, con los mismos ajustes; cada uno se guarda al terminar.</summary>
[SupportedOSPlatform("windows")]
public static class JobBatch
{
    /// <summary>Lo que se hace con un archivo: transcribir (y traducir) y guardar. Las pruebas ponen uno de mentira.</summary>
    public delegate Task<(Transcript Transcript, string Subtitles, string? Audio)> Worker(JobOptions options, IProgress<JobProgress> progress, CancellationToken cancellationToken);

    /// <summary>El de verdad: <see cref="SubtitleJob.RunAsync"/> y <see cref="SubtitleJob.SaveAsync"/>.</summary>
    public static async Task<(Transcript Transcript, string Subtitles, string? Audio)> RunAndSaveAsync(JobOptions options, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        Transcript transcript = await SubtitleJob.RunAsync(options, progress, cancellationToken).ConfigureAwait(false);
        (string subtitles, string? audio) = await SubtitleJob.SaveAsync(transcript, options, progress, cancellationToken).ConfigureAwait(false);
        return (transcript, subtitles, audio);
    }

    /// <summary>
    /// La cola sin repetidos (misma ruta completa) y sin que dos trabajos escriban la misma salida: el segundo «clip.mp4»
    /// de otra carpeta con la carpeta de salida fijada pasa a «clip-2», el tercero a «clip-3».
    /// </summary>
    public static IReadOnlyList<JobOptions> Prepare(IReadOnlyList<JobOptions> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        List<JobOptions> unique = [.. jobs.DistinctBy(j => Path.GetFullPath(j.InputFile), StringComparer.OrdinalIgnoreCase)];
        HashSet<string> outputs = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < unique.Count; i++)
        {
            string name = Path.GetFileNameWithoutExtension(unique[i].InputFile);
            for (int n = 2; !outputs.Add(SubtitleJob.OutputPath(unique[i], "")); n++)
            {
                unique[i] = unique[i] with { OutputName = $"{name}-{n}" };
            }
        }
        return unique;
    }

    /// <summary>
    /// Procesa los archivos en orden (tras <see cref="Prepare"/>). Un archivo que falla no para a los demás: queda con su error en el resultado.
    /// Cancelar sí para la cola: el archivo en curso suelta su ffmpeg y su módulo (los matan sus propios <c>CancellationToken</c>)
    /// y vuelve, con los que faltaban, marcado <see cref="BatchResult.Cancelled"/>; lo ya guardado se devuelve igual.
    /// </summary>
    public static async Task<IReadOnlyList<BatchResult>> RunAsync(IReadOnlyList<JobOptions> jobs, IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default, Worker? worker = null)
    {
        jobs = Prepare(jobs);
        worker ??= RunAndSaveAsync;
        List<BatchResult> results = new(jobs.Count);
        for (int i = 0; i < jobs.Count; i++)
        {
            JobOptions options = jobs[i];
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new BatchResult(options, null, null, null, null, Cancelled: true));
                continue;
            }
            int index = i;
            progress?.Report(new BatchProgress(index, jobs.Count, options.InputFile, null));
            // Relevo síncrono (no Progress<T>, que encola): el de fuera ya lleva cada aviso a su hilo.
            SyncProgress<JobProgress> stage = new(p => progress?.Report(new BatchProgress(index, jobs.Count, options.InputFile, p)));
            try
            {
                (Transcript transcript, string subtitles, string? audio) = await worker(options, stage, cancellationToken).ConfigureAwait(false);
                results.Add(new BatchResult(options, transcript, subtitles, audio, null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                results.Add(new BatchResult(options, null, null, null, null, Cancelled: true));
            }
            catch (Exception ex)
            {
                results.Add(new BatchResult(options, null, null, null, SubtitleJob.Describe(ex) ?? "Error inesperado: " + ex.Message));
            }
        }
        return results;
    }

    /// <summary>
    /// Resumen para la barra: «5 archivos listos», «3 archivos listos, 2 con error: a.mp4, b.mkv» o, si se canceló,
    /// «3 de 5 listos, cancelado en d.mp4».
    /// </summary>
    public static string Summary(IReadOnlyList<BatchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        int ok = results.Count(r => r.Ok);
        string ready = results.FirstOrDefault(r => r.Cancelled) is { } cancelled
            ? $"{ok} de {results.Count} listos, cancelado en {Path.GetFileName(cancelled.Options.InputFile)}"
            : ok == 1 ? "1 archivo listo" : $"{ok} archivos listos";
        List<string> failed = [.. results.Where(r => r.Error is not null).Select(r => Path.GetFileName(r.Options.InputFile))];
        return failed.Count == 0 ? ready : $"{ready}, {failed.Count} con error: {string.Join(", ", failed)}";
    }
}

/// <summary>Avisa en el mismo hilo (no <see cref="Progress{T}"/>, que encola): quien lo recibe ya lleva cada aviso a su hilo.</summary>
public sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

/// <summary>Dónde quedó el resultado, para abrirlo en el Explorador al terminar.</summary>
public static class ResultFolder
{
    /// <summary>Carpeta común de las salidas; si están en varias, la de la primera. Nula sin salidas.</summary>
    public static string? Of(IEnumerable<string?> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return outputs.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => Path.GetDirectoryName(Path.GetFullPath(o!))).FirstOrDefault(d => d is not null);
    }

    /// <summary>
    /// Orden para el Explorador: con un solo archivo lo abre seleccionado («/select,»); con varios o ninguno abre la carpeta.
    /// Nula si no hay nada que abrir.
    /// </summary>
    public static (string Exe, string Arguments)? ExplorerCommand(IReadOnlyList<string?> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        List<string> files = [.. outputs.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => Path.GetFullPath(o!))];
        if (files.Count == 1)
        {
            return ("explorer.exe", "/select,\"" + files[0] + "\"");
        }
        return Of(files) is { } dir ? ("explorer.exe", "\"" + dir + "\"") : null;
    }
}
