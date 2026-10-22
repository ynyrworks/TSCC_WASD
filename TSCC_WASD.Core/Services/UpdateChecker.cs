using System.Net.Http.Headers;
using System.Text.Json;

namespace TSCC_WASD.Core.Services;

/// <summary>A file attached to a GitHub release. <paramref name="Sha256"/> is GitHub's own digest, when it lists one.</summary>
public sealed record ReleaseAsset(string Name, string Url, long Size, string? Sha256);

public sealed record UpdateInfo(Version Version, string Tag, string Url, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);

/// <summary>The release ZIP to install and, when the release has one, its <c>.sha256</c> file.</summary>
public sealed record UpdatePackage(UpdateInfo Release, ReleaseAsset Zip, ReleaseAsset? Checksum);

/// <summary>
/// Looks up the newest published GitHub release. Only runs when the user asks for it
/// (or turned on the startup check); nothing else in TSCC_WASD uses the network.
/// </summary>
public static class UpdateChecker
{
    public const string Repository = "ynyrworks/TSCC_WASD";
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases";
    private const string Api = "https://api.github.com/repos/" + Repository + "/releases?per_page=20";

    /// <summary>Returns the newest release, newer or not, or null when none is published.</summary>
    public static async Task<UpdateInfo?> GetLatestAsync(HttpClient? client = null, CancellationToken token = default)
    {
        using var owned = client is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(10) } : null;
        var http = client ?? owned!;
        using var request = new HttpRequestMessage(HttpMethod.Get, Api);
        // GitHub's API rejects requests without a User-Agent.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppPaths.Name, CurrentVersion.ToString(3)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return FindLatest(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
    }

    /// <summary>Picks the highest-versioned published release; drafts and non-version tags are ignored.</summary>
    public static UpdateInfo? FindLatest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        UpdateInfo? best = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) continue;
            version = Normalize(version);
            if (best is not null && version <= best.Version) continue;
            best = new UpdateInfo(version, tag, release.GetProperty("html_url").GetString() ?? ReleasesPage,
                release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean(), ReadAssets(release));
        }
        return best;
    }

    private static List<ReleaseAsset> ReadAssets(JsonElement release)
    {
        var assets = new List<ReleaseAsset>();
        if (!release.TryGetProperty("assets", out var list) || list.ValueKind != JsonValueKind.Array) return assets;
        foreach (var asset in list.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) && name.GetString() is { } n
                && asset.TryGetProperty("browser_download_url", out var url) && url.GetString() is { } u)
            {
                long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
                string? sha256 = asset.TryGetProperty("digest", out var d) && d.GetString() is { } digest
                    && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest["sha256:".Length..] : null;
                assets.Add(new ReleaseAsset(n, u, size, sha256));
            }
        }
        return assets;
    }

    public static bool IsNewer(UpdateInfo latest, Version current) => latest.Version > Normalize(current);

    /// <summary>The release ZIP name scripts/package.ps1 produces for a version.</summary>
    public static string PackageFileName(Version version) => $"{AppPaths.Name}-{version.ToString(3)}-win-x64.zip";

    /// <summary>
    /// The win-x64 ZIP of this release and its checksum file, or null when the release has no
    /// ZIP served from this repository's own release downloads over HTTPS.
    /// </summary>
    public static UpdatePackage? SelectPackage(UpdateInfo release)
    {
        string zipName = PackageFileName(release.Version);
        ReleaseAsset? Find(string name) => release.Assets.FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && IsReleaseDownload(a.Url, release.Tag, a.Name));
        return Find(zipName) is { } zip ? new UpdatePackage(release, zip, Find(zipName + ".sha256")) : null;
    }

    /// <summary>True only for https://github.com/{Repository}/releases/download/{tag}/{name}.</summary>
    public static bool IsReleaseDownload(string url, string tag, string name)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        string expected = $"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}";
        return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The running version as major.minor.patch.</summary>
    public static Version CurrentVersion => Normalize(typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0));

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
