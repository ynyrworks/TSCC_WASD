using System.Reflection;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.App;

/// <summary>Version channel and link rules for the About page.</summary>
public static class AboutInfo
{
    public static readonly string RepoUrl = $"https://github.com/{UpdateChecker.Repository}";

    /// <summary>The full version text, such as 1.0.0 or 1.0.0-rc.1 (build metadata removed).</summary>
    public static string CurrentVersionText
    {
        get
        {
            string? info = typeof(AboutInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info)) return UpdateChecker.CurrentVersion.ToString(3);
            int plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
    }

    /// <summary>True when the version is below 1.0.0 or carries a pre-release suffix such as -rc.</summary>
    public static bool IsPreview(string version)
    {
        string v = version.Trim().TrimStart('v', 'V');
        int plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus];
        int dash = v.IndexOf('-');
        if (dash >= 0) return true;
        return !Version.TryParse(v, out var parsed) || parsed.Major < 1;
    }

    public static string ChannelText(string version, bool chinese) =>
        IsPreview(version) ? (chinese ? "預覽版" : "Preview") : (chinese ? "正式版" : "Stable");

    public static string ManualUrl(bool chinese) =>
        $"{RepoUrl}/blob/main/docs/{(chinese ? "MANUAL.zh-TW.md" : "MANUAL.md")}";

    /// <summary>The main window's Help link target; always the same manual the About page opens.</summary>
    public static string HelpUrl(bool chinese) => ManualUrl(chinese);

    public static string IssuesUrl => $"{RepoUrl}/issues";

    public static string ThirdPartyNoticesUrl => $"{RepoUrl}/blob/main/THIRD-PARTY-NOTICES.md";

    /// <summary>The header line of the About page, such as "版本 1.0.0 · 正式版".</summary>
    public static string VersionLine(string version, bool chinese) => chinese
        ? $"版本 {version} · {ChannelText(version, true)}"
        : $"Version {version} · {ChannelText(version, false)}";

    public static string SigningPolicyUrl => $"{RepoUrl}/blob/main/docs/CODE-SIGNING-POLICY.md";
}
