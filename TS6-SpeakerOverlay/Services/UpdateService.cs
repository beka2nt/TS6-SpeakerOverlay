using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace TS6_SpeakerOverlay.Services;

public sealed record ReleaseInfo(Version Version, string Tag, Uri PageUri, Uri? DownloadUri);

public enum UpdateCheckState { NotChecked, Checking, Current, Available, Failed, RateLimited }

public sealed class UpdateService
{
    private const string RepositoryPath = "/beka2nt/TS6-SpeakerOverlay/releases/";
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024
    };
    private readonly HttpClient _client;

    public UpdateService() : this(SharedClient) { }
    internal UpdateService(HttpClient client) => _client = client;

    public async Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/beka2nt/TS6-SpeakerOverlay/releases/latest");
        request.Headers.UserAgent.ParseAdd("TS6-SpeakerOverlay/" + CurrentVersion);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("GitHub update check was rate limited.", null, response.StatusCode);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseRelease(json);
    }

    public static string CurrentVersion => typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    internal static ReleaseInfo ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            throw new FormatException("A stable published release is required.");
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out Version? version))
            throw new FormatException("The release version is invalid.");
        string page = root.GetProperty("html_url").GetString() ?? "";
        if (!IsTrustedReleaseUrl(page, "tag/")) throw new FormatException("The release URL is invalid.");
        Uri? download = null;
        if (root.TryGetProperty("assets", out JsonElement assets))
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != "TS6-SpeakerOverlay.exe" ||
                    asset.GetProperty("state").GetString() != "uploaded" || asset.GetProperty("size").GetInt64() <= 0)
                    continue;
                string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (IsTrustedReleaseUrl(url, "download/")) download = new Uri(url);
            }
        }
        return new ReleaseInfo(version, tag, new Uri(page), download);
    }

    internal static bool IsTrustedReleaseUrl(string url, string section) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath.StartsWith(RepositoryPath + section, StringComparison.Ordinal);

    internal static bool IsNewer(Version candidate, Version current) =>
        Normalize(candidate) > Normalize(current);

    private static Version Normalize(Version value) =>
        new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}
