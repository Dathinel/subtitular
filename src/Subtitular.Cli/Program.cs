using System.Runtime.Versioning;
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
        string? salida = null;
        JobOptions options = new("");
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? value = i + 1 < args.Length ? args[i + 1] : null;
            switch (arg.ToLowerInvariant())
            {
                case "--idioma" when value is not null: options = options with { Language = args[++i] }; break;
                case "--formato" when value is not null: options = options with { Format = args[++i].ToLowerInvariant() }; break;
                case "--salida" when value is not null: salida = args[++i]; break;
                case "--traducir" when value is not null: options = options with { TranslateTo = args[++i] }; break;
                case "--voz": options = options with { Speak = true }; break;
                case "--ffmpeg" when value is not null: options = options with { Ffmpeg = args[++i] }; break;
                case "--modulo" when value is not null: options = options with { SubtitlesModule = args[++i] }; break;
                case "--modulo-traducir" when value is not null: options = options with { TranslateModule = args[++i] }; break;
                case "--modulo-voz" when value is not null: options = options with { VoiceModule = args[++i] }; break;
                case "--device" when value is not null: options = options with { Device = args[++i].ToLowerInvariant() }; break;
                case "--model" when value is not null: options = options with { Model = args[++i] }; break;
                case "--threads" when value is not null && int.TryParse(value, out int t): i++; options = options with { Threads = t }; break;
                default:
                    if (!arg.StartsWith('-') && inputFile is null)
                    {
                        inputFile = arg;
                        break;
                    }
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
        options = options with { InputFile = inputFile };
        if (!string.IsNullOrWhiteSpace(salida))
        {
            // --salida fija la carpeta y, por su extensión, el formato; el nombre lo pone el trabajo (archivo[.idioma].ext).
            options = options with { OutputDir = Path.GetDirectoryName(Path.GetFullPath(salida)) };
            if (salida.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
            {
                options = options with { Format = "vtt" };
            }
        }

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.Error.WriteLine();
            Console.Error.WriteLine("Cancelando a petición del usuario...");
            cts.Cancel();
        };

        string lastLine = "";
        Progress<JobProgress> progress = new(p =>
        {
            string line = $"\r{p.Label}: {(int)(p.StageFraction * 100)} % (total {(int)(p.Overall * 100)} %)    ";
            if (line != lastLine)
            {
                lastLine = line;
                Console.Error.Write(line);
            }
        });

        try
        {
            Console.Error.WriteLine($"Procesando «{Path.GetFileName(inputFile)}»...");
            Transcript transcript = await SubtitleJob.RunAsync(options, progress, cts.Token).ConfigureAwait(false);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Transcripción completada ({transcript.Segments.Count} segmentos, idioma {transcript.Language}). Guardando...");
            (string subtitles, string? wav) = await SubtitleJob.SaveAsync(transcript, options, progress, cts.Token).ConfigureAwait(false);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Subtítulos generados con éxito: {subtitles}");
            if (wav is not null)
            {
                Console.Error.WriteLine($"Voz generada: {wav}");
            }
            return 0;
        }
        catch (Exception ex) when (SubtitleJob.Describe(ex) is { } message)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(ex is OperationCanceledException or SubtitularUserException ? message : $"Error: {message}");
            return ex is OperationCanceledException or SubtitularUserException ? 1 : 2;
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
        Console.Error.WriteLine("  --salida <ruta>       Carpeta (y extensión) de salida. Por defecto: junto al video");
        Console.Error.WriteLine("  --traducir <idioma>   Traduce los subtítulos (en, pt-BR, fr, de...) con Rodaje.Module.Translate");
        Console.Error.WriteLine("  --voz                 Lee los subtítulos en voz alta y deja un WAV junto al video (es, pt, en)");
        Console.Error.WriteLine("  --modulo <ruta>       Ruta del ejecutable Rodaje.Module.Subtitles.exe");
        Console.Error.WriteLine("  --modulo-traducir <ruta>, --modulo-voz <ruta>  Rutas de los otros módulos");
        Console.Error.WriteLine("  --ffmpeg <ruta>       Ruta del ejecutable ffmpeg.exe");
        Console.Error.WriteLine("  --device <auto|gpu|cpu> Dispositivo de ejecución. Por defecto: auto");
        Console.Error.WriteLine("  --model <ruta>        Ruta alternativa al modelo GGML Whisper");
        Console.Error.WriteLine("  --threads <n>         Número de hilos de CPU");
        Console.Error.WriteLine("  --help, -h            Muestra esta ayuda");
    }
}
