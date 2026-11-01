using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace TSCC_WASD.Core.Services;

/// <summary>Program-wide preferences (not per profile). Saved immediately when changed.</summary>
public sealed class AppSettings
{
    /// <summary>Run at Windows sign-in, minimized to the tray, and start mapping right away.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Start mapping whenever the program opens, not only at sign-in.</summary>
    public bool StartMappingOnLaunch { get; set; }

    /// <summary>Minimizing hides the window to the notification area.</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// When mapping starts automatically, restart a Steam that started before the controllers were
    /// hidden, so Steam only sees the virtual DS4. Manual starts still ask first.
    /// </summary>
    public bool ManageSteam { get; set; } = true;

    /// <summary>
    /// After an automatic start, launch Steam if it is not running. Turn off Steam's own
    /// "run at startup" so Steam always starts after the controllers are hidden.
    /// </summary>
    public bool LaunchSteamAfterAutoStart { get; set; }

    /// <summary>"auto", "zh-TW" or "en". Takes effect on the next launch.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Check GitHub for a newer release when the app opens. Off by default: no network unless asked.</summary>
    public bool CheckUpdatesOnStartup { get; set; }

    /// <summary>Offer to install HidHide at launch while it is missing. ViGEmBus is always offered: mapping needs it.</summary>
    public bool OfferHidHideInstall { get; set; } = true;

    /// <summary>
    /// Experimental: while mapping, also hide third-party XInput controllers (8BitDo, PowerA, ...), not only
    /// Microsoft's. May include wheels or large joysticks. A settings.json without this key means on.
    /// </summary>
    public bool HideThirdPartyXInput { get; set; } = true;

    public string? LastProfileId { get; set; }
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private readonly string _path;

    public AppSettingsStore(string? path = null) => _path = path ?? AppPaths.File("settings.json");

    /// <summary>Missing or unreadable settings fall back to defaults; they hold no user data worth failing over.</summary>
    public AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);
    }
}

/// <summary>Per-user sign-in launch via HKCU\...\Run. No admin rights, scheduled tasks or services.</summary>
[SupportedOSPlatform("windows")]
public static class StartupRegistration
{
    public const string StartupArgument = "--startup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppPaths.Name;

    public static bool IsEnabled(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Replaces the pre-rename "YnyrWASD" Run value with one pointing at this executable.</summary>
    public static void MigrateLegacy(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(AppPaths.LegacyName) is null) return;
        key.DeleteValue(AppPaths.LegacyName, throwOnMissingValue: false);
        if (enabled) Set(true, exePath);
    }

    public static void Set(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\" {StartupArgument}");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
