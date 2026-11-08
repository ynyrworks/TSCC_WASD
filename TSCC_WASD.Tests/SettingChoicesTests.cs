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

    private static (MainViewModel vm, AppSettingsStore store, Action cleanup) NewVm(Action<bool, string> writer)
    {
        string dir = Path.Combine(Path.GetTempPath(), "wasd-choices-" + Guid.NewGuid().ToString("N"));
        var store = new AppSettingsStore(Path.Combine(dir, "settings.json"));
        return (new MainViewModel(settingsStore: store, startupWriter: writer, hidHideFactory: () => null), store,
            () => { try { Directory.Delete(dir, true); } catch (IOException) { } });
    }

    [Fact]
    public void ChoiceSettersWriteTheSettingsAndReadThemBack()
    {
        var written = new List<bool>();
        var (vm, store, cleanup) = NewVm((on, _) => written.Add(on));
        try
        {
            foreach (var choice in Enum.GetValues<StartupChoice>())
            {
                vm.StartupChoice = choice;
                Assert.Equal(choice, vm.StartupChoice);
                var saved = store.Load();
                Assert.Equal(SettingChoices.Apply(choice), (saved.StartWithWindows, saved.StartMappingOnLaunch));
            }
            foreach (var choice in Enum.GetValues<SteamChoice>())
            {
                vm.SteamChoice = choice;
                Assert.Equal(choice, vm.SteamChoice);
                var saved = store.Load();
                Assert.Equal(SettingChoices.Apply(choice), (saved.ManageSteam, saved.LaunchSteamAfterAutoStart));
            }
            Assert.NotEmpty(written); // the injected writer, never the real Run key
        }
        finally { cleanup(); }
    }

    [Fact]
    public void StartupDescriptionFollowsTheChoice()
    {
        var (vm, _, cleanup) = NewVm((_, _) => { });
        try
        {
            var seen = new HashSet<string>();
            foreach (var choice in Enum.GetValues<StartupChoice>())
            {
                vm.StartupChoice = choice;
                Assert.Equal(SettingChoices.Description(choice, TSCC_WASD.Core.L.Chinese), vm.StartupDescription);
                seen.Add(vm.StartupDescription);
            }
            Assert.Equal(4, seen.Count);
        }
        finally { cleanup(); }
    }

    [Fact]
    public void FailedRunKeyWriteChangesNeitherSettingAndShowsTheError()
    {
        var (vm, store, cleanup) = NewVm((_, _) => throw new UnauthorizedAccessException("denied"));
        try
        {
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            vm.StartupChoice = StartupChoice.AtSignInAndOnOpen;
            Assert.Equal(StartupChoice.Off, vm.StartupChoice);
            Assert.False(vm.Settings.StartWithWindows);
            Assert.False(vm.Settings.StartMappingOnLaunch);
            Assert.Contains("denied", vm.StatusMessage);
            Assert.Contains(nameof(MainViewModel.StartupChoice), raised); // the drop-down re-reads the real state
            Assert.False(store.Load().StartMappingOnLaunch);
        }
        finally { cleanup(); }
    }
}
