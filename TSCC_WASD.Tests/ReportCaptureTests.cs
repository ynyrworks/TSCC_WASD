using System.Diagnostics;
using Xunit;
using TSCC_WASD.App;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

public class ReportCaptureTests
{
    [Fact]
    public void OffByDefaultAndRecordsNothing()
    {
        var capture = new ReportCapture();
        Assert.False(capture.IsRecording);
        capture.Record("NS2ProUsb", new byte[] { 1, 2 });
        capture.RecordText("XInput", "x");
        Assert.Empty(capture.Snapshot());
    }

    [Fact]
    public void RecordsSourceTimeAndACopyOfTheReport()
    {
        var capture = new ReportCapture();
        capture.Start();
        var report = new byte[] { 0xAB, 0x01 };
        capture.Record("PlayStation", report);
        report[0] = 0;
        var entry = Assert.Single(capture.Snapshot());
        Assert.Equal("PlayStation", entry.Source);
        Assert.Equal(new byte[] { 0xAB, 0x01 }, entry.Data);
        Assert.InRange(entry.Milliseconds, 0, 5000);
    }

    [Fact]
    public void StopsAtTheEntryLimit()
    {
        var capture = new ReportCapture();
        capture.Start(TimeSpan.FromMinutes(1), maxEntries: 3);
        for (int i = 0; i < 10; i++) capture.Record("s", new byte[] { (byte)i });
        Assert.Equal(3, capture.Snapshot().Count);
        Assert.False(capture.IsRecording);
    }

    [Fact]
    public void StopsWhenTimeIsUp()
    {
        var capture = new ReportCapture();
        capture.Start(TimeSpan.FromMilliseconds(50));
        capture.Record("s", new byte[] { 1 });
        var wait = Stopwatch.StartNew();
        while (capture.IsRecording && wait.Elapsed < TimeSpan.FromSeconds(5)) Thread.Sleep(10);
        Assert.False(capture.IsRecording);
        capture.Record("s", new byte[] { 2 });
        Assert.Single(capture.Snapshot());
    }

    [Fact]
    public void ConcurrentWritersKeepEveryEntryUpToTheLimit()
    {
        var capture = new ReportCapture();
        capture.Start(TimeSpan.FromMinutes(1), maxEntries: 800);
        Parallel.For(0, 4, _ => { for (int i = 0; i < 500; i++) capture.Record("s", new byte[] { 7 }); });
        Assert.Equal(800, capture.Snapshot().Count);
        Assert.False(capture.IsRecording);
    }

    [Fact]
    public void ExportPutsDiagnosticsFirstThenOneLinePerEntry()
    {
        var capture = new ReportCapture();
        capture.Start();
        capture.Record("SwitchPro", new byte[] { 0x30, 0xFF });
        capture.RecordText("XInput", "parsed buttons=A");
        var lines = capture.Export("TSCC_WASD 1.0\nInput: x\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("TSCC_WASD 1.0", lines[0]);
        Assert.Contains(lines, l => l.Contains("XInput has no raw report"));
        Assert.Matches(@"^\d+ SwitchPro 30FF$", lines[^2]);
        Assert.Matches(@"^\d+ XInput parsed buttons=A$", lines[^1]);
    }

    [Fact]
    public async Task ControllerTestWindowShowsStartMappingWithoutBindingErrors()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ControllerTestWindow();
                Assert.Contains(TSCC_WASD.Core.L.T("請先開始映射", "Start mapping first"), ((System.Windows.Controls.TextBlock)window.FindName("SourceText")).Text);
                window.Close();
                done.SetResult();
            }
            catch (Exception ex) { done.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
