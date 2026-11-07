using System.IO;
using TSCC_WASD.App;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services;
using Xunit;

namespace TSCC_WASD.Tests;

public class ProfileAutoSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
    private string ProfilePath => Path.Combine(_dir, "profiles.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private MainViewModel NewViewModel(int delayMs = 150) =>
        new(new ProfileStore(ProfilePath), new AppSettingsStore(Path.Combine(_dir, "settings.json")), TimeSpan.FromMilliseconds(delayMs));

    private static async Task<bool> WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        for (int waited = 0; waited < timeoutMs; waited += 20)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    // ---- ProfileAutoSaver on its own ----

    private sealed class Probe
    {
        public List<string> Writes = new();
        public List<(ProfileSaveState State, string? Message)> Reports = new();
        public bool CanSave = true;
        public bool Hold;
        public bool FailWrite;
        public MappingProfile Profile = new();
        public ProfileAutoSaver Create(int delayMs) => new(
            () => new ProfileCapture(new[] { Profile.Snapshot() }, null),
            list =>
            {
                if (FailWrite) throw new IOException("disk full");
                lock (Writes) Writes.Add(list[0].Name);
            },
            () => CanSave, (state, message) => { lock (Reports) Reports.Add((state, message)); }, TimeSpan.FromMilliseconds(delayMs),
            context: null, holdOff: () => Hold);
        public int WriteCount { get { lock (Writes) return Writes.Count; } }
    }

    [Fact]
    public async Task SeveralEditsInOneQuietPeriodAreOneWrite()
    {
        var probe = new Probe();
        using var saver = probe.Create(200);
        for (int i = 0; i < 5; i++)
        {
            probe.Profile.Name = "Edit " + i;
            saver.Touch();
            await Task.Delay(30);
        }
        Assert.Equal(0, probe.WriteCount);
        Assert.True(await WaitFor(() => probe.WriteCount > 0));
        await Task.Delay(500);
        Assert.Equal(new[] { "Edit 4" }, probe.Writes);
        Assert.Equal(ProfileSaveState.Saved, saver.State);
    }

    [Fact]
    public async Task WriteWaitsWhileAMouseButtonIsHeldAndHappensOnceAfterRelease()
    {
        var probe = new Probe { Hold = true };
        using var saver = probe.Create(60);
        probe.Profile.Name = "Dragged";
        saver.Touch();
        await Task.Delay(400);
        Assert.Equal(0, probe.WriteCount);
        probe.Hold = false;
        Assert.True(await WaitFor(() => probe.WriteCount == 1));
        await Task.Delay(300);
        Assert.Equal(1, probe.WriteCount);
    }

    [Fact]
    public void FlushWritesPendingEditsImmediately()
    {
        var probe = new Probe();
        using var saver = probe.Create(60_000);
        probe.Profile.Name = "Before exit";
        saver.Touch();
        Assert.Equal(0, probe.WriteCount);
        Assert.True(saver.Flush());
        Assert.Equal(new[] { "Before exit" }, probe.Writes);
        Assert.True(saver.Flush()); // nothing pending: no second write
        Assert.Equal(1, probe.WriteCount);
    }

    [Fact]
    public void NothingIsWrittenWhenSavingIsNotAllowed()
    {
        var probe = new Probe { CanSave = false };
        using var saver = probe.Create(10);
        probe.Profile.Name = "Edited";
        saver.Touch();
        Assert.True(saver.Flush());
        Assert.Equal(0, probe.WriteCount);
    }

    [Fact]
    public void FailedWriteKeepsTheEditsAndIsRetriedByTheNextFlush()
    {
        var probe = new Probe { FailWrite = true };
        using var saver = probe.Create(60_000);
        probe.Profile.Name = "Keep me";
        saver.Touch();
        Assert.False(saver.Flush());
        Assert.Equal(ProfileSaveState.Failed, saver.State);
        Assert.Contains(probe.Reports, r => r.State == ProfileSaveState.Failed && r.Message == "disk full");
        probe.FailWrite = false;
        Assert.True(saver.Flush());
        Assert.Equal(new[] { "Keep me" }, probe.Writes);
        Assert.Equal(ProfileSaveState.Saved, saver.State);
    }

    // ---- Through the view model and a real ProfileStore in a temp folder ----

    [Fact]
    public async Task ViewModelCoalescesEditsIntoOneSaveAndKeepsTheFileValid()
    {
        var vm = NewViewModel();
        try
        {
            var before = File.GetLastWriteTimeUtc(ProfilePath);
            vm.SelectedProfile!.Name = "A";
            vm.SelectedProfile.Name = "AB";
            vm.SelectedProfile.PollingRateHz = 200;
            Assert.True(await WaitFor(() => new ProfileStore(ProfilePath).LoadProfiles()[0].Name == "AB"));
            Assert.Equal(200, new ProfileStore(ProfilePath).LoadProfiles()[0].PollingRateHz);
            Assert.True(await WaitFor(() => vm.SaveStatusIsSaved));
            Assert.True(File.Exists(ProfilePath + ".bak"));
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task ExitFlushWritesPendingEdits()
    {
        var vm = NewViewModel(delayMs: 60_000);
        vm.SelectedProfile!.Name = "Edited at exit";
        Assert.NotEqual("Edited at exit", new ProfileStore(ProfilePath).LoadProfiles()[0].Name);
        await vm.DisposeAsync();
        Assert.Equal("Edited at exit", new ProfileStore(ProfilePath).LoadProfiles()[0].Name);
    }

    [Fact]
    public async Task InvalidDeadZoneIsNotSavedAndTheStatusSaysWhy()
    {
        var vm = NewViewModel(delayMs: 60_000);
        try
        {
            double original = new ProfileStore(ProfilePath).LoadProfiles()[0].DeadZone;
            vm.SelectedProfile!.DeadZone = 2;
            Assert.False(vm.FlushProfiles());
            Assert.Equal(original, new ProfileStore(ProfilePath).LoadProfiles()[0].DeadZone);
            Assert.True(vm.SaveStatusIsError);
            Assert.Contains(TSCC_WASD.Core.L.T("死區必須介於 0–0.95。", "Dead zone must be 0–0.95."), vm.SaveStatusText);
            vm.SelectedProfile.DeadZone = 0.3;
            Assert.True(vm.FlushProfiles());
            Assert.Equal(0.3, new ProfileStore(ProfilePath).LoadProfiles()[0].DeadZone);
            Assert.True(vm.SaveStatusIsSaved);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task ValidEditsSurviveAFlushWhileAnotherProfileIsInvalid()
    {
        var vm = NewViewModel(delayMs: 60_000);
        try
        {
            vm.NewCommand.Execute(null);
            vm.SelectedProfile!.Name = "Second";
            Assert.True(vm.FlushProfiles());
            var first = vm.Profiles[0];
            double savedDeadZone = first.DeadZone;
            first.DeadZone = 2;           // invalid
            first.Name = "Renamed first"; // valid on its own, but the profile as a whole is not
            vm.SelectedProfile.Description = "Edited second";
            vm.SelectedProfile.PollingRateHz = 240;
            Assert.False(vm.FlushProfiles());
            var stored = new ProfileStore(ProfilePath).LoadProfiles();
            Assert.Equal(2, stored.Count);
            Assert.Equal("Edited second", stored[1].Description);
            Assert.Equal(240, stored[1].PollingRateHz);
            Assert.Equal(savedDeadZone, stored[0].DeadZone); // last valid version of the invalid profile
            Assert.NotEqual("Renamed first", stored[0].Name);
            Assert.True(vm.SaveStatusIsError);
            Assert.Contains("Renamed first", vm.SaveStatusText);
            Assert.Contains(TSCC_WASD.Core.L.T("死區必須介於 0–0.95。", "Dead zone must be 0–0.95."), vm.SaveStatusText);
            first.DeadZone = 0.2;
            Assert.True(vm.FlushProfiles());
            Assert.Equal("Renamed first", new ProfileStore(ProfilePath).LoadProfiles()[0].Name);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task AddRemoveAndDuplicateEachTriggerASave()
    {
        var vm = NewViewModel(delayMs: 60_000);
        try
        {
            vm.NewCommand.Execute(null);
            Assert.True(vm.FlushProfiles());
            Assert.Equal(2, new ProfileStore(ProfilePath).LoadProfiles().Count);

            vm.DuplicateCommand.Execute(null);
            Assert.Equal(3, vm.Profiles.Count);
            Assert.True(vm.FlushProfiles());
            var stored = new ProfileStore(ProfilePath).LoadProfiles();
            Assert.Equal(3, stored.Count);
            Assert.Equal(3, stored.Select(p => p.Id).Distinct().Count());

            vm.DeleteCommand.Execute(null);
            Assert.True(vm.FlushProfiles());
            Assert.Equal(2, new ProfileStore(ProfilePath).LoadProfiles().Count);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task ABrokenProfileFileIsNeverOverwritten()
    {
        Directory.CreateDirectory(_dir);
        const string broken = "{ this is not json";
        File.WriteAllText(ProfilePath, broken);
        var vm = NewViewModel(delayMs: 30);
        try
        {
            Assert.False(vm.CanEdit);
            Assert.True(vm.SaveStatusIsError);
            Assert.False(vm.NewCommand.CanExecute(null));
            Assert.False(vm.DuplicateCommand.CanExecute(null));
            vm.Profiles.Add(new MappingProfile { Name = "Sneaky" }); // even a direct change must not reach the disk
            Assert.True(vm.FlushProfiles());
            await Task.Delay(300);
        }
        finally { await vm.DisposeAsync(); }
        Assert.Equal(broken, File.ReadAllText(ProfilePath));
        Assert.False(File.Exists(ProfilePath + ".bak"));
    }

    [Fact]
    public async Task ReloadFlushesPendingEditsFirstAndThenReadsTheDisk()
    {
        var vm = NewViewModel(delayMs: 60_000);
        try
        {
            vm.SelectedProfile!.Name = "Survives reload";
            vm.ReloadCommand.Execute(null);
            Assert.Equal("Survives reload", vm.SelectedProfile!.Name);
            Assert.Equal("Survives reload", new ProfileStore(ProfilePath).LoadProfiles()[0].Name);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public async Task ReloadAsksBeforeDiscardingEditsThatCannotBeSaved()
    {
        var vm = NewViewModel(delayMs: 60_000);
        try
        {
            int asked = 0;
            vm.ConfirmDiscardUnsaved = () => { asked++; return false; };
            vm.SelectedProfile!.DeadZone = 5;
            vm.ReloadCommand.Execute(null);
            Assert.Equal(1, asked);
            Assert.Equal(5, vm.SelectedProfile!.DeadZone); // declined: nothing was reloaded
            vm.ConfirmDiscardUnsaved = () => { asked++; return true; };
            vm.ReloadCommand.Execute(null);
            Assert.Equal(2, asked);
            Assert.NotEqual(5, vm.SelectedProfile!.DeadZone);
        }
        finally { await vm.DisposeAsync(); }
    }

    // ---- Page text ----

    [Theory]
    [InlineData("eldenring.exe", false, true, "eldenring.exe")]
    [InlineData("eldenring.exe", true, true, "eldenring.exe · 使用中")]
    [InlineData(null, false, true, "沒有對應的遊戲時使用")]
    [InlineData("  ", true, true, "沒有對應的遊戲時使用 · 使用中")]
    [InlineData("game.exe", true, false, "game.exe · In use")]
    public void ListSubtitleShowsTheProcessOrTheDefaultNoteAndTheActiveMark(string? process, bool active, bool zh, string expected) =>
        Assert.Equal(expected, ProfileSubtitleConverter.Text(process, active, zh));
}
