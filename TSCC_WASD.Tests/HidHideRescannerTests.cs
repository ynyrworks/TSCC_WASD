using System.ComponentModel;
using System.IO;
using Xunit;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Mapping;
using TSCC_WASD.Core.Services.Setup;

namespace TSCC_WASD.Tests;

public class HidHideRescannerTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Slow = TimeSpan.FromSeconds(30);

    private sealed class FakeWatcher : IDisposable
    {
        public Action Arrive = () => { };
        public int Disposed;
        public void Dispose() => Interlocked.Increment(ref Disposed);
    }

    private static async Task WaitFor(Func<bool> condition, int ms = 3000)
    {
        for (int i = 0; i < ms / 10 && !condition(); i++) await Task.Delay(10);
    }

    [Fact]
    public async Task BurstOfArrivalsCallsHideOnceAndIdleCallsNothing()
    {
        int calls = 0;
        var watcher = new FakeWatcher();
        using var rescanner = new HidHideRescanner(() => Interlocked.Increment(ref calls),
            onArrival => { watcher.Arrive = onArrival; return watcher; }, Debounce, Slow, Slow);
        Assert.True(rescanner.UsesNotifications);

        await Task.Delay(300);
        Assert.Equal(0, calls);

        for (int i = 0; i < 5; i++) watcher.Arrive();
        await WaitFor(() => calls > 0);
        await Task.Delay(300);
        Assert.Equal(1, calls);

        watcher.Arrive();
        await WaitFor(() => calls > 1);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task FallbackTimerStillScansWhenNotificationsAreQuiet()
    {
        int calls = 0;
        using var rescanner = new HidHideRescanner(() => Interlocked.Increment(ref calls),
            _ => new FakeWatcher(), Debounce, TimeSpan.FromMilliseconds(80), Slow);
        await WaitFor(() => calls >= 2);
        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task RegistrationFailureFallsBackToFastScanAndLogs()
    {
        string old = AppLog.Directory;
        AppLog.Directory = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        try
        {
            int calls = 0;
            using var rescanner = new HidHideRescanner(() => Interlocked.Increment(ref calls),
                _ => throw new Win32Exception(5, "denied"), Debounce, Slow, TimeSpan.FromMilliseconds(80));
            Assert.False(rescanner.UsesNotifications);
            await WaitFor(() => calls >= 2);
            Assert.True(calls >= 2);
            Assert.Contains("unavailable", File.ReadAllText(AppLog.CurrentFile));
        }
        finally { AppLog.Directory = old; }
    }

    [Fact]
    public async Task DisposeUnregistersAndStopsEveryHideCall()
    {
        int calls = 0;
        var watcher = new FakeWatcher();
        var rescanner = new HidHideRescanner(() => Interlocked.Increment(ref calls),
            onArrival => { watcher.Arrive = onArrival; return watcher; }, Debounce, TimeSpan.FromMilliseconds(50), Slow);
        await WaitFor(() => calls >= 1);
        watcher.Arrive();
        rescanner.Dispose();
        int atStop = calls;
        Assert.Equal(1, watcher.Disposed);

        watcher.Arrive();
        await Task.Delay(400);
        Assert.Equal(atStop, calls);
        rescanner.Dispose();
        Assert.Equal(1, watcher.Disposed);
    }

    [Fact]
    public void RecoverHiddenControllersReturnsTheRecoveryNoticeForADamagedRecord()
    {
        string dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string record = Path.Combine(dir, "hidhide-restore.json");
        File.WriteAllText(record, "{broken");
        string old = AppLog.Directory;
        AppLog.Directory = dir;
        try
        {
            var guard = new HidHideGuard(_ => "", record);
            var coordinator = new MappingCoordinator(hidHideFactory: () => guard);
            string? message = coordinator.RecoverHiddenControllers();
            Assert.NotNull(guard.RecoveryNotice);
            Assert.Equal(guard.RecoveryNotice, message);
        }
        finally { AppLog.Directory = old; }
    }

    [Fact]
    public void DamagedRecordIsKeptEvenWhenHidHideCliCannotStart()
    {
        string dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string record = Path.Combine(dir, "hidhide-restore.json");
        File.WriteAllText(record, "{broken");
        string old = AppLog.Directory;
        AppLog.Directory = dir;
        try
        {
            var guard = new HidHideGuard(_ => throw new Win32Exception(2, "not found"), record);
            guard.Restore();
            Assert.Equal("{broken", File.ReadAllText(record + ".corrupt"));
            Assert.False(guard.HasPendingRestore);
            Assert.False(string.IsNullOrEmpty(guard.RecoveryNotice));
        }
        finally { AppLog.Directory = old; }
    }
}
