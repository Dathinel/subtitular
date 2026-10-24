using System.Runtime.Versioning;
using System.Text;
using Rodaje.Modules.Protocol;
using Subtitular.Core;

namespace Subtitular.Cli;

[SupportedOSPlatform("windows")]
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? inputFile = null;
        string idioma = "auto";
        string? formato = null;
        string? salida = null;
        string? ffmpegPath = null;
        string? moduloPath = null;
        string device = "auto";
        string? modelPath = null;
        int threads = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Equals("--idioma", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                idioma = args[++i];
            }
            else if (arg.Equals("--formato", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                formato = args[++i].ToLowerInvariant();
            }
            else if (arg.Equals("--salida", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                salida = args[++i];
            }
            else if (arg.Equals("--ffmpeg", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                ffmpegPath = args[++i];
            }
            else if (arg.Equals("--modulo", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                moduloPath = args[++i];
            }
            else if (arg.Equals("--device", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                device = args[++i].ToLowerInvariant();
            }
            else if (arg.Equals("--model", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                modelPath = args[++i];
            }
            else if (arg.Equals("--threads", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[++i], out int t))
            {
                threads = t;
            }
            else if (!arg.StartsWith('-') && inputFile is null)
            {
                inputFile = arg;
            }
            else
            {
                Console.Error.WriteLine($"Opción desconocida o no válida: {arg}");
                PrintUsage();
                return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(inputFile))
        {
            Console.Error.WriteLine("Error: Debe especificar un archivo de entrada de video o audio.");
            return 1;
        }

        // Determinar formato (srt o vtt)
        if (string.IsNullOrWhiteSpace(formato))
        {
            if (!string.IsNullOrWhiteSpace(salida) && salida.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
            {
                formato = "vtt";
            }
            else
            {
                formato = "srt";
            }
        }

        if (formato is not ("srt" or "vtt"))
        {
            Console.Error.WriteLine($"Error: El formato debe ser «srt» o «vtt», no «{formato}».");
            return 1;
        }

        // Determinar ruta de salida
        if (string.IsNullOrWhiteSpace(salida))
        {
            string ext = "." + formato;
            string? dir = Path.GetDirectoryName(inputFile);
            string baseName = Path.GetFileNameWithoutExtension(inputFile);
            salida = string.IsNullOrEmpty(dir) ? baseName + ext : Path.Combine(dir, baseName + ext);
        }

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.Error.WriteLine();
            Console.Error.WriteLine("Cancelando a petición del usuario...");
            cts.Cancel();
        };

        try
        {
            Console.Error.WriteLine($"Decodificando audio de «{Path.GetFileName(inputFile)}»...");
            byte[] audioBytes = await AudioDecoder.DecodeToMemoryAsync(inputFile, ffmpegPath, cts.Token).ConfigureAwait(false);

            Console.Error.WriteLine($"Audio decodificado ({audioBytes.Length / (AudioDecoder.TargetChannels * 4 * AudioDecoder.TargetSampleRate)} s). Iniciando módulo de subtítulos...");

            SubtitleParameters parameters = new(
                Language: idioma,
                Device: device,
                Model: modelPath,
                Threads: threads);

            string lastStage = "";
            int lastPercent = -1;
            Progress<ModuleProgress> progress = new(p =>
            {
                int percent = (int)(p.Fraction * 100);
                if (p.Stage != lastStage || percent != lastPercent)
                {
                    lastStage = p.Stage ?? "";
                    lastPercent = percent;
                    Console.Error.Write($"\rProgreso: {percent}% ({lastStage})    ");
                }
            });

            Transcript transcript = await SubtitleTranscriber.TranscribeAsync(
                audioBytes,
                parameters,
                moduloPath,
                progress,
                cts.Token).ConfigureAwait(false);

            Console.Error.WriteLine();
            Console.Error.WriteLine($"Transcripción completada ({transcript.Segments.Count} segmentos detectados). Guardando subtítulos...");

            string? outDir = Path.GetDirectoryName(salida);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (StreamWriter writer = new(salida, false, new UTF8Encoding(false)))
            {
                if (formato == "vtt")
                {
                    SubtitleWriter.WriteVtt(transcript, writer);
                }
                else
                {
                    SubtitleWriter.WriteSrt(transcript, writer);
                }
            }

            Console.Error.WriteLine($"Subtítulos generados con éxito: {salida}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Operación cancelada.");
            return 1;
        }
        catch (SubtitularUserException ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ModuleCallException ex) when (ex.Message.Contains("Falta el modelo", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ModuleException ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error del módulo: {ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error inesperado: {ex.Message}");
            return 2;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Uso: Subtitular.Cli <archivo-video-o-audio> [opciones]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Opciones:");
        Console.Error.WriteLine("  --idioma <código>     Idioma del audio (auto, es, en...). Por defecto: auto");
        Console.Error.WriteLine("  --formato <srt|vtt>   Formato de salida (srt o vtt). Por defecto: srt");
        Console.Error.WriteLine("  --salida <ruta>       Ruta del archivo de salida. Por defecto: junto al video");
        Console.Error.WriteLine("  --modulo <ruta>       Ruta del ejecutable Rodaje.Module.Subtitles.exe");
        Console.Error.WriteLine("  --ffmpeg <ruta>       Ruta del ejecutable ffmpeg.exe");
        Console.Error.WriteLine("  --device <auto|gpu|cpu> Dispositivo de ejecución. Por defecto: auto");
        Console.Error.WriteLine("  --model <ruta>        Ruta alternativa al modelo GGML Whisper");
        Console.Error.WriteLine("  --threads <n>         Número de hilos de CPU");
        Console.Error.WriteLine("  --help, -h            Muestra esta ayuda");
    }
}
