using TSCC_WASD.Core.Services;

namespace TSCC_WASD.App.ViewModels;

/// <summary>What happens when the app opens (the first drop-down of the settings page).</summary>
public enum StartupChoice
{
    /// <summary>No sign-in launch, no automatic mapping.</summary>
    Off,
    /// <summary>No sign-in launch; mapping starts whenever the app is opened by hand.</summary>
    OnOpen,
    /// <summary>Run at Windows sign-in (tray) and start mapping; opening by hand does not.</summary>
    AtSignIn,
    /// <summary>Sign-in launch, and also start mapping whenever the app is opened by hand.</summary>
    AtSignInAndOnOpen
}

/// <summary>How Steam is handled when mapping starts automatically (the second drop-down).</summary>
public enum SteamChoice
{
    /// <summary>Leave Steam alone.</summary>
    Leave,
    /// <summary>Restart Steam if it started before the controllers were hidden.</summary>
    RestartIfRunning,
    /// <summary>Start Steam after hiding the controllers if it is not running.</summary>
    StartIfNotRunning,
    /// <summary>Both of the above.</summary>
    Both
}

/// <summary>
/// The one place that maps the settings page drop-downs onto the existing <see cref="AppSettings"/> booleans.
/// Each drop-down covers every combination of its two booleans, so an older settings file never shows a wrong
/// choice and no "custom" entry is needed.
/// </summary>
public static class SettingChoices
{
    public static StartupChoice GetStartup(bool startWithWindows, bool startMappingOnLaunch) => (startWithWindows, startMappingOnLaunch) switch
    {
        (false, false) => StartupChoice.Off,
        (false, true) => StartupChoice.OnOpen,
        (true, false) => StartupChoice.AtSignIn,
        (true, true) => StartupChoice.AtSignInAndOnOpen
    };

    public static (bool StartWithWindows, bool StartMappingOnLaunch) Apply(StartupChoice choice) => choice switch
    {
        StartupChoice.Off => (false, false),
        StartupChoice.OnOpen => (false, true),
        StartupChoice.AtSignIn => (true, false),
        StartupChoice.AtSignInAndOnOpen => (true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    public static SteamChoice GetSteam(bool manageSteam, bool launchSteamAfterAutoStart) => (manageSteam, launchSteamAfterAutoStart) switch
    {
        (false, false) => SteamChoice.Leave,
        (true, false) => SteamChoice.RestartIfRunning,
        (false, true) => SteamChoice.StartIfNotRunning,
        (true, true) => SteamChoice.Both
    };

    public static (bool ManageSteam, bool LaunchSteamAfterAutoStart) Apply(SteamChoice choice) => choice switch
    {
        SteamChoice.Leave => (false, false),
        SteamChoice.RestartIfRunning => (true, false),
        SteamChoice.StartIfNotRunning => (false, true),
        SteamChoice.Both => (true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    public static string Name(StartupChoice choice, bool zh) => choice switch
    {
        StartupChoice.Off => zh ? "不自動啟動" : "Do nothing",
        StartupChoice.OnOpen => zh ? "只在手動開啟程式時開始映射" : "Map when I open the app",
        StartupChoice.AtSignIn => zh ? "登入時啟動，縮在系統匣並開始映射" : "Start at sign-in, in the tray, mapping",
        _ => zh ? "登入時啟動並開始映射，手動開啟也開始映射" : "Start at sign-in mapping, and map when opened by hand"
    };

    public static string Description(StartupChoice choice, bool zh) => choice switch
    {
        StartupChoice.Off => zh ? "不隨 Windows 啟動，開啟程式時也不會自動開始映射" : "Does not start with Windows and does not map automatically",
        StartupChoice.OnOpen => zh ? "不隨 Windows 啟動；手動開啟程式時自動開始映射" : "Does not start with Windows; mapping begins when you open the app",
        StartupChoice.AtSignIn => zh ? "登入後縮在系統匣並開始映射；手動開啟程式不會自動映射" : "Starts in the tray and maps at sign-in; opening the app by hand does not map",
        _ => zh ? "登入後縮在系統匣並開始映射；手動開啟程式時也會自動映射" : "Starts in the tray and maps at sign-in; opening the app by hand maps too"
    };

    public static string Name(SteamChoice choice, bool zh) => choice switch
    {
        SteamChoice.Leave => zh ? "不處理 Steam" : "Leave Steam alone",
        SteamChoice.RestartIfRunning => zh ? "Steam 已開啟就重啟" : "Restart Steam if running",
        SteamChoice.StartIfNotRunning => zh ? "Steam 沒開就啟動" : "Start Steam if not running",
        _ => zh ? "已開啟就重啟，沒開就啟動 Steam" : "Restart if running, start if not"
    };
}
