using System.Net.Http.Headers;
using System.Text.Json;

namespace TSCC_WASD.Core.Services;

public sealed record UpdateInfo(Version Version, string Tag, string Url, bool Prerelease);

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
            if (best is not null && version <= best.Version) continue;
            best = new UpdateInfo(version, tag, release.GetProperty("html_url").GetString() ?? ReleasesPage,
                release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean());
        }
        return best;
    }

    public static bool IsNewer(UpdateInfo latest, Version current) => latest.Version > Normalize(current);

    /// <summary>The running version as major.minor.patch.</summary>
    public static Version CurrentVersion => Normalize(typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0));

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
