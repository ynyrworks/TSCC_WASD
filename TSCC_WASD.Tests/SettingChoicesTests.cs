using System.IO;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Services;
using Xunit;

namespace TSCC_WASD.Tests;

public class SettingChoicesTests
{
    public static IEnumerable<object[]> Bools() =>
        from a in new[] { false, true } from b in new[] { false, true } select new object[] { a, b };

    [Theory]
    [InlineData(StartupChoice.Off, false, false)]
    [InlineData(StartupChoice.OnOpen, false, true)]
    [InlineData(StartupChoice.AtSignIn, true, false)]
    [InlineData(StartupChoice.AtSignInAndOnOpen, true, true)]
    public void StartupChoiceSetsTheExistingBooleans(StartupChoice choice, bool startWithWindows, bool onLaunch)
        => Assert.Equal((startWithWindows, onLaunch), SettingChoices.Apply(choice));

    [Theory]
    [InlineData(SteamChoice.Leave, false, false)]
    [InlineData(SteamChoice.RestartIfRunning, true, false)]
    [InlineData(SteamChoice.StartIfNotRunning, false, true)]
    [InlineData(SteamChoice.Both, true, true)]
    public void SteamChoiceSetsTheExistingBooleans(SteamChoice choice, bool manage, bool launch)
        => Assert.Equal((manage, launch), SettingChoices.Apply(choice));

    [Theory, MemberData(nameof(Bools))]
    public void EveryOldCheckboxCombinationRoundTripsThroughItsChoice(bool first, bool second)
    {
        Assert.Equal((first, second), SettingChoices.Apply(SettingChoices.GetStartup(first, second)));
        Assert.Equal((first, second), SettingChoices.Apply(SettingChoices.GetSteam(first, second)));
    }

    [Fact]
    public void EveryChoiceRoundTripsAndHasADistinctName()
    {
        foreach (var c in Enum.GetValues<StartupChoice>())
        {
            var (a, b) = SettingChoices.Apply(c);
            Assert.Equal(c, SettingChoices.GetStartup(a, b));
        }
        foreach (var c in Enum.GetValues<SteamChoice>())
        {
            var (a, b) = SettingChoices.Apply(c);
            Assert.Equal(c, SettingChoices.GetSteam(a, b));
        }
        foreach (bool zh in new[] { true, false })
        {
            Assert.Equal(4, Enum.GetValues<StartupChoice>().Select(c => SettingChoices.Name(c, zh)).Distinct().Count());
            Assert.Equal(4, Enum.GetValues<SteamChoice>().Select(c => SettingChoices.Name(c, zh)).Distinct().Count());
        }
    }

    [Fact]
    public void SteamChoiceOnTheViewModelWritesTheSettingsAndReadsThemBack()
    {
        string dir = Path.Combine(Path.GetTempPath(), "wasd-choices-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AppSettingsStore(Path.Combine(dir, "settings.json"));
            var vm = new MainViewModel(settingsStore: store);
            foreach (var choice in Enum.GetValues<SteamChoice>())
            {
                vm.SteamChoice = choice;
                Assert.Equal(choice, vm.SteamChoice);
                var saved = store.Load();
                Assert.Equal(SettingChoices.Apply(choice), (saved.ManageSteam, saved.LaunchSteamAfterAutoStart));
            }
            // The start-up drop-down is not driven here: it writes the real HKCU Run entry.
            Assert.Equal(StartupChoice.Off, SettingChoices.GetStartup(vm.Settings.StartWithWindows, vm.Settings.StartMappingOnLaunch));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
