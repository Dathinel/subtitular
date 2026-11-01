using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class UpdateCheckTests
{
    private static readonly Version Current = new(1, 0, 0);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>La respuesta de GitHub que recibe la app, o la excepción que simula la falta de red.</summary>
    private sealed class FakeFeed(string? json, Exception? error = null) : IReleaseFeed
    {
        public int Calls { get; private set; }

        public Task<string> FetchLatestAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return error is null ? Task.FromResult(json!) : Task.FromException<string>(error);
        }
    }

    private static string Release(string tag, string url = "https://github.com/Dathinel/subtitular/releases/tag/v1.1.0") =>
        $$"""{"url":"https://api.github.com/repos/Dathinel/subtitular/releases/1","html_url":"{{url}}","tag_name":"{{tag}}","name":"Subtitular {{tag}}","draft":false,"prerelease":false,"body":"Notas"}""";

    private static Task<UpdateCheckResult> Check(Preferences prefs, IReleaseFeed feed, bool force = false, DateTimeOffset? now = null) =>
        UpdateCheck.CheckAsync(prefs, Current, feed, now ?? Now, force, CancellationToken.None);

    [Fact]
    public async Task Newer_release_is_announced_and_the_check_is_stamped()
    {
        FakeFeed feed = new(Release("v1.1.0"));
        UpdateCheckResult result = await Check(new Preferences(), feed);
        Assert.Equal(UpdateOutcome.NewVersion, result.Outcome);
        Assert.Equal(new Version(1, 1, 0), result.Release!.Version);
        Assert.Equal("https://github.com/Dathinel/subtitular/releases/tag/v1.1.0", result.Release.Url);
        Assert.Equal(Now, result.Preferences.LastUpdateCheck);
        Assert.Equal(1, feed.Calls);
    }

    [Fact]
    public async Task Same_or_older_release_is_up_to_date()
    {
        Assert.Equal(UpdateOutcome.UpToDate, (await Check(new Preferences(), new FakeFeed(Release("v1.0.0")))).Outcome);
        Assert.Equal(UpdateOutcome.UpToDate, (await Check(new Preferences(), new FakeFeed(Release("v0.9.9")))).Outcome);
        // La versión de la app llega con cuatro partes (1.0.0.0) y se compara como tres.
        UpdateCheckResult four = await UpdateCheck.CheckAsync(new Preferences(), new Version(1, 0, 0, 0), new FakeFeed(Release("v1.0.0")), Now, false, CancellationToken.None);
        Assert.Equal(UpdateOutcome.UpToDate, four.Outcome);
    }

    [Theory]
    [InlineData("v1.1.0-rc1")]
    [InlineData("1.1")]
    [InlineData("latest")]
    [InlineData("")]
    public async Task Odd_tag_is_silent(string tag)
    {
        UpdateCheckResult result = await Check(new Preferences(), new FakeFeed(Release(tag)));
        Assert.Equal(UpdateOutcome.Failed, result.Outcome);
        Assert.Null(result.Release);
        Assert.Equal(Now, result.Preferences.LastUpdateCheck);
    }

    [Theory]
    [InlineData("{ roto")]
    [InlineData("[]")]
    [InlineData("{\"message\":\"Not Found\"}")]
    [InlineData("null")]
    public async Task Odd_body_is_silent(string json)
    {
        Assert.Equal(UpdateOutcome.Failed, (await Check(new Preferences(), new FakeFeed(json))).Outcome);
    }

    [Fact]
    public async Task Network_error_is_silent_and_still_counts_as_the_check_of_the_day()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("sin red"), new TaskCanceledException("5 s"), new IOException("cortado") })
        {
            UpdateCheckResult result = await Check(new Preferences(), new FakeFeed(null, error));
            Assert.Equal(UpdateOutcome.Failed, result.Outcome);
            Assert.Equal(Now, result.Preferences.LastUpdateCheck);
            Assert.False(UpdateCheck.IsDue(result.Preferences, Now.AddHours(23)));
        }
    }

    [Fact]
    public async Task Cancelling_the_caller_propagates()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        FakeFeed feed = new(null, new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateCheck.CheckAsync(new Preferences(), Current, feed, Now, false, cts.Token));
    }

    [Fact]
    public async Task Dismissed_version_stays_silent_until_the_next_one()
    {
        Preferences prefs = UpdateCheck.Dismiss(new Preferences(), new ReleaseInfo(new Version(1, 1, 0), GitHubReleaseFeed.LatestUrl));
        Assert.Equal("1.1.0", prefs.SkippedVersion);
        Assert.Equal(UpdateOutcome.Dismissed, (await Check(prefs, new FakeFeed(Release("v1.1.0")))).Outcome);
        Assert.Equal(UpdateOutcome.NewVersion, (await Check(prefs, new FakeFeed(Release("v1.2.0")))).Outcome);
        // «Buscar actualizaciones» lo pide a propósito: se vuelve a ofrecer la descartada.
        Assert.Equal(UpdateOutcome.NewVersion, (await Check(prefs, new FakeFeed(Release("v1.1.0")), force: true)).Outcome);
    }

    [Fact]
    public async Task Waits_24_hours_between_checks_unless_forced()
    {
        FakeFeed feed = new(Release("v1.1.0"));
        Preferences prefs = new() { LastUpdateCheck = Now.AddHours(-23) };
        UpdateCheckResult result = await Check(prefs, feed);
        Assert.Equal(UpdateOutcome.NotDue, result.Outcome);
        Assert.Same(prefs, result.Preferences);
        Assert.Equal(0, feed.Calls);

        Assert.Equal(UpdateOutcome.NewVersion, (await Check(prefs, feed, now: Now.AddHours(1))).Outcome);
        Assert.Equal(UpdateOutcome.NewVersion, (await Check(prefs, feed, force: true)).Outcome);
        // Un reloj atrasado respecto a la última consulta no deja la app muda para siempre.
        Assert.True(UpdateCheck.IsDue(new Preferences { LastUpdateCheck = Now.AddDays(3) }, Now));
    }

    [Fact]
    public async Task Switched_off_never_asks_unless_forced()
    {
        FakeFeed feed = new(Release("v1.1.0"));
        Preferences prefs = new() { CheckUpdates = false };
        Assert.Equal(UpdateOutcome.NotDue, (await Check(prefs, feed)).Outcome);
        Assert.Equal(0, feed.Calls);
        Assert.Equal(UpdateOutcome.NewVersion, (await Check(prefs, feed, force: true)).Outcome);
    }

    [Fact]
    public void Only_github_pages_open_in_the_browser()
    {
        Assert.Equal("https://github.com/Dathinel/subtitular/releases/tag/v1.1.0", ReleaseInfo.Parse(Release("v1.1.0"))!.Url);
        Assert.Equal(GitHubReleaseFeed.LatestUrl, ReleaseInfo.Parse(Release("v1.1.0", "http://github.com/x"))!.Url);
        Assert.Equal(GitHubReleaseFeed.LatestUrl, ReleaseInfo.Parse(Release("v1.1.0", "https://example.com/x"))!.Url);
        Assert.Equal(GitHubReleaseFeed.LatestUrl, ReleaseInfo.Parse(Release("v1.1.0", "javascript:alert(1)"))!.Url);
        Assert.Equal(GitHubReleaseFeed.LatestUrl, ReleaseInfo.Parse("""{"tag_name":"v1.1.0"}""")!.Url);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData(" v1.2.3 ", "1.2.3")]
    [InlineData("v1.2", null)]
    [InlineData("v1.2.3.4", null)]
    [InlineData("v1.2.3-beta", null)]
    [InlineData("v1..3", null)]
    [InlineData(null, null)]
    public void Tag_parses_only_three_numbers(string? tag, string? expected)
    {
        Assert.Equal(expected is null ? null : Version.Parse(expected), ReleaseInfo.TagVersion(tag));
    }

    [Fact]
    public void App_version_is_the_one_from_the_build_props()
    {
        Assert.Equal(typeof(UpdateCheck).Assembly.GetName().Version!.ToString(3), UpdateCheck.AppVersion.ToString(3));
        Assert.Equal(-1, UpdateCheck.AppVersion.Revision);
    }
}

public sealed class LauncherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "subtitular-lanzador-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _env = [];

    public LauncherTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Put(params string[] parts)
    {
        string path = Path.Combine([_root, .. parts, Launcher.Exe]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    private string? Find() => Launcher.Find(k => _env.GetValueOrDefault(k), Path.Combine(_root, "LocalAppData"));

    [Fact]
    public void Finds_the_launcher_or_nothing()
    {
        Assert.Null(Find());
        string old = Put("LocalAppData", "Dathinel", "Lanzador", "1.0.0");
        string newer = Put("LocalAppData", "Dathinel", "Lanzador", "1.2.0");
        Put("LocalAppData", "Dathinel", "Lanzador", "rota"); // no es versión: se ignora
        Assert.Equal(newer, Find());
        string flat = Put("LocalAppData", "Dathinel", "Lanzador");
        Assert.Equal(flat, Find());
        File.Delete(flat);
        File.Delete(newer);
        Assert.Equal(old, Find());

        string own = Put("propio");
        _env["SUBTITULAR_LANZADOR"] = Path.Combine(_root, "propio");
        Assert.Equal(own, Find());
        _env["SUBTITULAR_LANZADOR"] = own;
        Assert.Equal(own, Find());
        _env["SUBTITULAR_LANZADOR"] = Path.Combine(_root, "nada");
        Assert.Equal(old, Find());
    }
}
