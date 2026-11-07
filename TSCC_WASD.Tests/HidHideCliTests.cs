using System.Diagnostics;
using System.IO;
using TSCC_WASD.Core.Services.Setup;
using Xunit;

namespace TSCC_WASD.Tests;

/// <summary>Runs a stand-in for HidHideCLI (Windows PowerShell scripts); never the real tool.</summary>
[Collection(AppLogCollection.Name)]
public class HidHideCliTests
{
    private static readonly string PowerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private static string[] Script(string command) => ["-NoProfile", "-NonInteractive", "-Command", command];

    [Fact]
    public void KillsAHungCliAfterTheTimeoutAndLogsIt()
    {
        string pidFile = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", "pid-" + Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
        string old = TSCC_WASD.Core.AppLog.Directory;
        string logDir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        TSCC_WASD.Core.AppLog.Directory = logDir;
        try
        {
            var watch = Stopwatch.StartNew();
            Assert.Throws<TimeoutException>(() => HidHideCli.Run(PowerShell, Script($"Set-Content -LiteralPath '{pidFile}' $PID; Start-Sleep -Seconds 60"), TimeSpan.FromSeconds(2)));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20));
            Assert.Contains("was killed", LogFile.ReadAllText(TSCC_WASD.Core.AppLog.CurrentFile));
            int pid = int.Parse(File.ReadAllText(pidFile).Trim());
            Assert.True(!ProcessExists(pid, WaitMs: 3000), "the hung CLI process was still running");
        }
        finally
        {
            TSCC_WASD.Core.AppLog.Directory = old;
            TempDir.Delete(logDir);
            try { File.Delete(pidFile); } catch (IOException) { }
        }
    }

    private static bool ProcessExists(int pid, int WaitMs)
    {
        var until = DateTime.UtcNow.AddMilliseconds(WaitMs);
        do
        {
            try { using var p = Process.GetProcessById(pid); if (p.HasExited) return false; }
            catch (ArgumentException) { return false; }
            Thread.Sleep(100);
        } while (DateTime.UtcNow < until);
        return true;
    }

    [Fact]
    public void AccessDeniedGivesPlainAdviceAndKeepsTheRawTextInTheLog()
    {
        string old = TSCC_WASD.Core.AppLog.Directory;
        string logDir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        TSCC_WASD.Core.AppLog.Directory = logDir;
        try
        {
            var ex = Assert.Throws<IOException>(() => HidHideCli.Run(PowerShell,
                Script("[Console]::Error.WriteLine('Error code 0x0005 Device: denied'); exit 31")));
            Assert.DoesNotContain("0x0005", ex.Message);
            Assert.True(ex.Message.Contains("HidHideCLI") && (ex.Message.Contains("重新啟動") || ex.Message.Contains("restart")));
            Assert.Contains("0x0005", LogFile.ReadAllText(TSCC_WASD.Core.AppLog.CurrentFile));
        }
        finally
        {
            TSCC_WASD.Core.AppLog.Directory = old;
            TempDir.Delete(logDir);
        }
    }

    [Theory]
    [InlineData(31, "", true)]
    [InlineData(1, "Error code 0x0005", true)]
    [InlineData(1, "something else", false)]
    public void RecognisesAccessDenied(int exit, string error, bool expected) =>
        Assert.Equal(expected, HidHideCli.IsAccessDenied(exit, error));

    [Fact]
    public async Task ConcurrentCallsRunOneAtATime()
    {
        string dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        string log = Path.Combine(dir, "calls.txt");
        Directory.CreateDirectory(dir);
        try
        {
            string command = $"Add-Content -LiteralPath '{log}' 'start'; Start-Sleep -Milliseconds 700; Add-Content -LiteralPath '{log}' 'end'";
            var calls = Enumerable.Range(0, 3).Select(_ => Task.Run(() => HidHideCli.Run(PowerShell, Script(command), TimeSpan.FromSeconds(30)))).ToArray();
            await Task.WhenAll(calls);
            Assert.Equal(["start", "end", "start", "end", "start", "end"], File.ReadAllLines(log));
        }
        finally { TempDir.Delete(dir); }
    }
}
