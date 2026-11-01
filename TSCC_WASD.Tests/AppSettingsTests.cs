using System.IO;
using Xunit;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

public class AppSettingsTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void HideThirdPartyXInputDefaultsOnWhenKeyIsMissing()
    {
        string path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"language\":\"en\"}");
        Assert.True(new AppSettingsStore(path).Load().HideThirdPartyXInput);
    }

    [Fact]
    public void MissingOrCorruptSettingsFallBackToDefaults()
    {
        string path = TempPath();
        var defaults = new AppSettingsStore(path).Load();
        Assert.False(defaults.StartWithWindows);
        Assert.True(defaults.MinimizeToTray);
        Assert.True(defaults.ManageSteam);
        Assert.Equal("auto", defaults.Language);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");
        Assert.Equal("auto", new AppSettingsStore(path).Load().Language);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        string path = TempPath();
        var store = new AppSettingsStore(path);
        store.Save(new AppSettings { StartWithWindows = true, LaunchSteamAfterAutoStart = true, Language = "en", LastProfileId = "abc" });
        var loaded = store.Load();
        Assert.True(loaded.StartWithWindows);
        Assert.True(loaded.LaunchSteamAfterAutoStart);
        Assert.Equal("en", loaded.Language);
        Assert.Equal("abc", loaded.LastProfileId);
        Assert.False(File.Exists(path + ".tmp"));
    }
}
