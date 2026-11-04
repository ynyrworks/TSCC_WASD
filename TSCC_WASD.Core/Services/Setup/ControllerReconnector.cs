using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace TSCC_WASD.Core.Services.Setup;

/// <summary>What one reconnect run did, written by the elevated helper for the app to read.</summary>
public sealed record ReconnectResult(DateTime StartedUtc, int UsbPorts, int Bluetooth, bool WirelessAdapter, List<string> Errors);

/// <summary>
/// Reconnects controllers without restarting Steam: an elevated helper power-cycles their USB ports
/// (<see cref="UsbPortCycler"/>). The helper is set up once with a single administrator prompt and then
/// runs on demand through a Task Scheduler task, so later reconnects need no prompt.
/// </summary>
/// <remarks>
/// Security: the task runs a copy of TSCC_WASD in %ProgramFiles%\TSCC_WASD\Helper, which only
/// administrators can change, never the user-writable extracted folder. The task's arguments are fixed;
/// the helper only cycles ports of the controllers TSCC_WASD supports. Its result file lives in the same
/// protected folder.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class ControllerReconnector
{
    public const string ReconnectArgument = "--reconnect-controllers";
    public const string InstallArgument = "--install-reconnect-helper";
    public const string UninstallArgument = "--uninstall-reconnect-helper";
    private const string TaskName = @"\TSCC_WASD\ReconnectControllers";
    private const int ErrorCancelled = 1223;
    private static readonly string[] SkippedFolders = ["drivers", "docs", "examples", "licenses", "scripts"];

    public static string HelperDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppPaths.Name, "Helper");

    private static string HelperExe => Path.Combine(HelperDirectory, "TSCC_WASD.exe");
    private static string ResultPath => Path.Combine(HelperDirectory, "last-reconnect.json");

    /// <summary>Set up and matching this version of TSCC_WASD.</summary>
    public static bool IsInstalled => File.Exists(HelperExe) && SameBuild(HelperExe, Environment.ProcessPath) && TaskExists();

    /// <summary>Set up for an older or newer TSCC_WASD; needs one more administrator prompt to update.</summary>
    public static bool NeedsUpdate =>
        File.Exists(HelperExe) && IsTsccWasd(Environment.ProcessPath) && !IsInstalled;

    /// <summary>Only TSCC_WASD itself can be compared with the helper copy (not a test host).</summary>
    private static bool IsTsccWasd(string? path) =>
        string.Equals(Path.GetFileName(path), "TSCC_WASD.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Cheap check (no Task Scheduler query), used to enable the Remove button.</summary>
    public static bool IsPresent => Directory.Exists(HelperDirectory);

    // ---- Called from the normal (non-elevated) app ----

    /// <summary>Sets up or updates the helper with one administrator prompt. False when declined or failed.</summary>
    public static Task<bool> InstallAsync() => RunElevatedSelfAsync(InstallArgument, WindowsIdentity.GetCurrent().Name);

    public static Task<bool> UninstallAsync() => RunElevatedSelfAsync(UninstallArgument);

    /// <summary>Starts the task and waits for its result; null if it did not report back in time.</summary>
    public static async Task<ReconnectResult?> ReconnectAsync(CancellationToken token = default)
    {
        var requested = DateTime.UtcNow.AddSeconds(-1);
        using (var run = Process.Start(SchtasksInfo("/Run", "/TN", TaskName)))
        {
            if (run is null) return null;
            await run.WaitForExitAsync(token);
            if (run.ExitCode != 0) return null;
        }
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(500, token);
            if (ReadResult() is { } result && result.StartedUtc >= requested) return result;
        }
        return null;
    }

    // ---- Entry points of the elevated process (handled before any window opens) ----

    /// <summary>Runs a helper command line if <paramref name="args"/> holds one; returns its exit code, or null.</summary>
    public static int? TryRunCommand(string[] args)
    {
        if (args.Length == 0) return null;
        try
        {
            return args[0] switch
            {
                ReconnectArgument => Reconnect(),
                InstallArgument when args.Length == 2 => Install(args[1]),
                UninstallArgument => Uninstall(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            AppLog.Error($"Helper command {args[0]} failed", ex);
            return 1;
        }
    }

    private static int Reconnect()
    {
        var started = DateTime.UtcNow;
        var plan = UsbPortCycler.PlanConnected();
        var errors = new List<string>();
        foreach (var port in plan.Ports)
        {
            try { UsbPortCycler.Cycle(port); }
            catch (Exception ex) when (ex is IOException or Win32Exception) { errors.Add($"{port.DeviceId}: {ex.Message}"); }
        }
        // Let Windows finish re-enumerating before the app checks on the controllers.
        if (plan.Ports.Count > 0) Thread.Sleep(2500);
        var result = new ReconnectResult(started, plan.Ports.Count - errors.Count, plan.Bluetooth, plan.WirelessAdapter, errors);
        File.WriteAllText(ResultPath, JsonSerializer.Serialize(result));
        return errors.Count == 0 ? 0 : 2;
    }

    private static int Install(string user)
    {
        if (string.IsNullOrWhiteSpace(user) || user.IndexOfAny(['"', '<', '>', '&']) >= 0) return 1;
        Directory.CreateDirectory(HelperDirectory);
        CopyApp(AppContext.BaseDirectory, HelperDirectory);
        var xml = Path.Combine(Path.GetTempPath(), $"TSCC_WASD-task-{Guid.NewGuid():N}.xml");
        File.WriteAllText(xml, TaskXml(HelperExe, user), Encoding.Unicode);
        try
        {
            using var create = Process.Start(SchtasksInfo("/Create", "/TN", TaskName, "/XML", xml, "/F"))!;
            create.WaitForExit();
            return create.ExitCode;
        }
        finally { File.Delete(xml); }
    }

    private static int Uninstall()
    {
        using (var delete = Process.Start(SchtasksInfo("/Delete", "/TN", TaskName, "/F"))!) delete.WaitForExit();
        if (Directory.Exists(HelperDirectory)) Directory.Delete(HelperDirectory, recursive: true);
        var parent = Path.GetDirectoryName(HelperDirectory)!;
        if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        return 0;
    }

    // ---- Helpers ----

    /// <summary>Task Scheduler definition: on demand only, as the signed-in user, with highest privileges.</summary>
    public static string TaskXml(string exePath, string user) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>TSCC_WASD: reconnects USB controllers so Steam releases them without a restart. Remove it in TSCC_WASD (App settings) or by deleting this task.</Description>
          </RegistrationInfo>
          <Principals>
            <Principal id="Author">
              <UserId>{SecurityElement.Escape(user)}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <AllowStartOnDemand>true</AllowStartOnDemand>
            <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>
            <Enabled>true</Enabled>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(exePath)}</Command>
              <Arguments>{ReconnectArgument}</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;

    private static async Task<bool> RunElevatedSelfAsync(params string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(info);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled) { return false; }
    }

    /// <summary>Copies the program (single file in releases, several files in builds) without docs or installers.</summary>
    internal static void CopyApp(string source, string target)
    {
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(Path.Combine(target, "last-reconnect.json")) };
        CopyAppInto(source, target, kept);
        RemoveStale(target, kept);
    }

    private static void CopyAppInto(string source, string target, HashSet<string> kept)
    {
        foreach (var file in Directory.GetFiles(source))
        {
            string destination = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
            kept.Add(Path.GetFullPath(destination));
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(dir);
            if (SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            string destination = Path.Combine(target, name);
            Directory.CreateDirectory(destination);
            kept.Add(Path.GetFullPath(destination));
            CopyAppInto(dir, destination, kept);
        }
    }

    /// <summary>Deletes everything under <paramref name="directory"/> that setup did not just copy (leftovers of older builds).
    /// Links are removed without following them.</summary>
    private static void RemoveStale(string directory, HashSet<string> kept)
    {
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            bool isLink = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
            if (entry is DirectoryInfo dir)
            {
                if (isLink) { if (!kept.Contains(dir.FullName)) dir.Delete(false); }
                else if (!kept.Contains(dir.FullName)) dir.Delete(true);
                else RemoveStale(dir.FullName, kept);
            }
            else if (!kept.Contains(entry.FullName)) entry.Delete();
        }
    }

    private static bool SameBuild(string helper, string? current)
    {
        if (current is null || !File.Exists(current)) return false;
        var a = new FileInfo(helper);
        var b = new FileInfo(current);
        return a.Length == b.Length
            && FileVersionInfo.GetVersionInfo(helper).FileVersion == FileVersionInfo.GetVersionInfo(current).FileVersion;
    }

    private static bool TaskExists()
    {
        try
        {
            using var query = Process.Start(SchtasksInfo("/Query", "/TN", TaskName));
            if (query is null) return false;
            query.WaitForExit(10_000);
            return query.HasExited && query.ExitCode == 0;
        }
        catch (Win32Exception) { return false; }
    }

    private static ReconnectResult? ReadResult()
    {
        try { return File.Exists(ResultPath) ? JsonSerializer.Deserialize<ReconnectResult>(File.ReadAllText(ResultPath)) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static ProcessStartInfo SchtasksInfo(params string[] args)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
}
