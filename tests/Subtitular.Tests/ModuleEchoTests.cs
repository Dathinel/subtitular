using System.Diagnostics;
using System.Text.Json;
using Rodaje.Modules.Protocol;

namespace Subtitular.Tests;

public sealed class ModuleEchoTests
{
    internal static string EchoExe
    {
        get
        {
            string local = Path.Combine(AppContext.BaseDirectory, "Rodaje.Module.Echo.exe");
            if (File.Exists(local))
            {
                return local;
            }

            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string[] candidates =
                [
                    Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Echo", "bin", "Debug", "net10.0-windows", "win-x64", "Rodaje.Module.Echo.exe"),
                    Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Echo", "bin", "Release", "net10.0-windows", "win-x64", "Rodaje.Module.Echo.exe"),
                    Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Echo", "bin", "Debug", "net10.0-windows", "Rodaje.Module.Echo.exe"),
                    Path.Combine(dir.FullName, "externo", "rodaje", "modules", "Rodaje.Module.Echo", "bin", "Release", "net10.0-windows", "Rodaje.Module.Echo.exe"),
                ];
                foreach (string c in candidates)
                {
                    if (File.Exists(c))
                    {
                        return Path.GetFullPath(c);
                    }
                }
            }

            throw new FileNotFoundException("No se encontró Rodaje.Module.Echo.exe.");
        }
    }

    [Fact]
    public async Task Echo_call_succeeds_and_reports_progress()
    {
        await using ModuleProcess module = await ModuleProcess.StartAsync(EchoExe);
        Assert.Equal("rodaje-eco", module.Client.Module.Id);

        List<ModuleProgress> progressReports = [];
        Progress<ModuleProgress> progress = new(p =>
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        using JsonDocument doc = JsonDocument.Parse("""{"pasos":10,"msPorPaso":10}""");
        ModuleResult result = await module.Client.InvokeAsync("contar", doc.RootElement.Clone(), progress: progress);

        Assert.NotNull(result.Value);
        Assert.Equal(10, result.Value.Value.GetProperty("pasos").GetInt32());

        lock (progressReports)
        {
            Assert.NotEmpty(progressReports);
            Assert.Equal(1.0, progressReports[^1].Fraction);
        }
    }

    [Fact]
    public async Task Cancellation_takes_less_than_one_second()
    {
        await using ModuleProcess module = await ModuleProcess.StartAsync(EchoExe);

        using CancellationTokenSource cts = new();
        using JsonDocument doc = JsonDocument.Parse("""{"pasos":10000,"msPorPaso":10}""");

        Task call = module.Client.InvokeAsync("contar", doc.RootElement.Clone(), cancellationToken: cts.Token);
        await Task.Delay(50);

        Stopwatch watch = Stopwatch.StartNew();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"La cancelación tardó {watch.ElapsedMilliseconds} ms (límite: 1 s).");
        Assert.False(module.Exited.IsCompleted, "El módulo debe seguir vivo tras la cancelación.");

        // Verificar que sigue respondiendo
        await module.Client.PingAsync();
    }

    [Fact]
    public async Task Module_that_dies_mid_call_fails_fast()
    {
        await using ModuleProcess module = await ModuleProcess.StartAsync(EchoExe);

        using JsonDocument doc = JsonDocument.Parse("""{"codigo":3}""");
        Stopwatch watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<ModuleDisconnectedException>(() =>
            module.Client.InvokeAsync("morir", doc.RootElement.Clone()));

        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"El fallo tardó {watch.ElapsedMilliseconds} ms (límite: 1 s).");

        int exitCode = await module.Exited.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(3, exitCode);
    }
}
