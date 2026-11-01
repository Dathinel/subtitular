using System.Text.Json;
using System.Text.Json.Serialization;

namespace Subtitular.Core;

/// <summary>De dónde sale el JSON de la última release: la red de verdad es <see cref="GitHubReleaseFeed"/>; las pruebas usan uno falso.</summary>
public interface IReleaseFeed
{
    /// <summary>El JSON de la última release publicada. Cualquier excepción cuenta como «sin red» y la app calla.</summary>
    Task<string> FetchLatestAsync(CancellationToken cancellationToken);
}

/// <summary>
/// La API pública de GitHub sin autenticación (<c>releases/latest</c>), con <c>User-Agent</c> «Subtitular/&lt;versión&gt;»
/// y 5 s de tiempo límite. No hay reintentos: si falla, se vuelve a probar al cabo de 24 h.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public const string LatestUrl = "https://github.com/Dathinel/subtitular/releases/latest";
    public const string ApiUrl = "https://api.github.com/repos/Dathinel/subtitular/releases/latest";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly HttpClient Client = new() { Timeout = Timeout, MaxResponseContentBufferSize = 1 << 20 };

    public async Task<string> FetchLatestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, ApiUrl);
        request.Headers.UserAgent.ParseAdd("Subtitular/" + UpdateCheck.AppVersion.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Lo que la app necesita de una release: su versión (del <c>tag_name</c> «vX.Y.Z») y la página para «Ver novedades».</summary>
public sealed record ReleaseInfo(Version Version, string Url)
{
    /// <summary>Nulo si el JSON no es una release o el tag no es «vX.Y.Z» (una respuesta rara se calla).</summary>
    public static ReleaseInfo? Parse(string json)
    {
        GitHubRelease? release;
        try
        {
            release = JsonSerializer.Deserialize(json, GitHubJsonContext.Default.GitHubRelease);
        }
        catch (JsonException)
        {
            return null;
        }
        if (release is null || TagVersion(release.TagName) is not { } version)
        {
            return null;
        }
        // Solo se abre una página de GitHub; si html_url trae otra cosa, la lista de releases.
        string url = Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : GitHubReleaseFeed.LatestUrl;
        return new ReleaseInfo(version, url);
    }

    /// <summary>«v1.2.3» o «1.2.3» a versión de tres partes; cualquier otra cosa (sufijos, dos partes) es nulo.</summary>
    public static Version? TagVersion(string? tag)
    {
        string text = (tag ?? "").Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }
        string[] parts = text.Split('.');
        if (parts.Length != 3 || parts.Any(p => p.Length == 0 || p.Length > 9 || !p.All(char.IsAsciiDigit)))
        {
            return null;
        }
        return Version.TryParse(text, out Version? version) ? version : null;
    }
}

/// <summary>Los dos campos que se leen de la respuesta de GitHub; el resto se ignora.</summary>
internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl);

[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;

public enum UpdateOutcome
{
    /// <summary>No se consultó: el aviso está apagado o no han pasado 24 h desde la última consulta.</summary>
    NotDue,
    NewVersion,
    UpToDate,
    /// <summary>Hay una versión nueva, pero es la que el usuario descartó con «Más tarde».</summary>
    Dismissed,
    /// <summary>Sin red, error de GitHub o respuesta rara: la app calla.</summary>
    Failed,
}

/// <summary><c>Preferences</c> trae la consulta ya apuntada (salvo <see cref="UpdateOutcome.NotDue"/>): hay que guardarlas.</summary>
public sealed record UpdateCheckResult(UpdateOutcome Outcome, ReleaseInfo? Release, Preferences Preferences);

/// <summary>El aviso de versión nueva: al arrancar como mucho una vez cada 24 h, o al momento con «Buscar actualizaciones».</summary>
public static class UpdateCheck
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>La versión de la app (la de <c>Directory.Build.props</c>), en tres partes.</summary>
    public static Version AppVersion => Normalize(typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Con el aviso activado y sin consulta en las últimas 24 h (o con el reloj atrasado respecto a la última).</summary>
    public static bool IsDue(Preferences prefs, DateTimeOffset now) =>
        prefs.CheckUpdates && (prefs.LastUpdateCheck is not { } last || last > now || now - last >= Interval);

    /// <summary>
    /// Consulta <paramref name="feed"/> si toca (o siempre, con <paramref name="force"/>, que además ignora la versión descartada).
    /// Nunca lanza por la red: un fallo es <see cref="UpdateOutcome.Failed"/>. Solo propaga la cancelación de <paramref name="cancellationToken"/>.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(Preferences prefs, Version current, IReleaseFeed feed, DateTimeOffset now, bool force, CancellationToken cancellationToken)
    {
        if (!force && !IsDue(prefs, now))
        {
            return new UpdateCheckResult(UpdateOutcome.NotDue, null, prefs);
        }
        Preferences stamped = prefs with { LastUpdateCheck = now };
        string json;
        try
        {
            json = await feed.FetchLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateOutcome.Failed, null, stamped);
        }
        if (ReleaseInfo.Parse(json) is not { } release)
        {
            return new UpdateCheckResult(UpdateOutcome.Failed, null, stamped);
        }
        if (release.Version <= Normalize(current))
        {
            return new UpdateCheckResult(UpdateOutcome.UpToDate, release, stamped);
        }
        bool dismissed = !force && string.Equals(prefs.SkippedVersion, release.Version.ToString(3), StringComparison.Ordinal);
        return new UpdateCheckResult(dismissed ? UpdateOutcome.Dismissed : UpdateOutcome.NewVersion, release, stamped);
    }

    /// <summary>«Más tarde»: no se vuelve a avisar de esa versión (sí de la siguiente).</summary>
    public static Preferences Dismiss(Preferences prefs, ReleaseInfo release) => prefs with { SkippedVersion = release.Version.ToString(3) };

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}

/// <summary>El Lanzador de Dathinel, que instala las versiones nuevas; si está en el PC, la franja ofrece abrirlo.</summary>
public static class Launcher
{
    public const string Exe = "Lanzador.exe";

    /// <summary>
    /// Busca <c>Lanzador.exe</c> en <c>SUBTITULAR_LANZADOR</c> (el exe o su carpeta) y en
    /// <c>%LOCALAPPDATA%\Dathinel\Lanzador</c> (junto a los módulos de <c>Dathinel\Comun</c>), ahí mismo o en la subcarpeta
    /// de versión más nueva. Nulo si no está.
    /// </summary>
    public static string? Find() => Find(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string? Find(Func<string, string?> env, string localAppData)
    {
        List<string> candidates = [];
        if (env("SUBTITULAR_LANZADOR") is { Length: > 0 } own)
        {
            candidates.Add(own);
            candidates.Add(Path.Combine(own, Exe));
        }
        string home = Path.Combine(localAppData, "Dathinel", "Lanzador");
        candidates.Add(Path.Combine(home, Exe));
        if (Directory.Exists(home))
        {
            candidates.AddRange(Directory.EnumerateDirectories(home)
                .Select(d => (Dir: d, Version: Version.TryParse(Path.GetFileName(d), out Version? v) ? v : null))
                .Where(t => t.Version is not null)
                .OrderByDescending(t => t.Version)
                .Select(t => Path.Combine(t.Dir, Exe)));
        }
        string? found = candidates.FirstOrDefault(c => string.Equals(Path.GetFileName(c), Exe, StringComparison.OrdinalIgnoreCase) && File.Exists(c));
        return found is null ? null : Path.GetFullPath(found);
    }
}
