using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TSCC_WASD.Core.Services.Setup;

/// <summary>
/// Runs HidHideCLI one call at a time, bounded by a timeout, and makes sure a hung call cannot outlive the app:
/// the child is killed on timeout and every child belongs to a job object that Windows closes with the app.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HidHideCli
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private const int AccessDeniedExitCode = 31;
    private static readonly object CallGate = new();
    private static readonly Lazy<nint> Job = new(CreateKillOnCloseJob);

    public static string Run(string cli, IReadOnlyList<string> args, TimeSpan? timeout = null)
    {
        lock (CallGate) return RunOne(cli, args, timeout ?? DefaultTimeout);
    }

    internal static bool IsAccessDenied(int exitCode, string error) =>
        exitCode == AccessDeniedExitCode || error.Contains("0x0005", StringComparison.OrdinalIgnoreCase);

    private static string RunOne(string cli, IReadOnlyList<string> args, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(cli)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException(L.T("無法啟動 HidHideCLI。", "Could not start HidHideCLI."));
        TryAssignToJob(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* Already exited. */ }
            try { process.WaitForExit(2_000); } catch { /* Best effort. */ }
            AppLog.Warn($"HidHideCLI did not finish within {timeout.TotalSeconds:0.#} s and was killed: {string.Join(' ', args)}");
            throw new TimeoutException(L.T("HidHideCLI 沒有回應。", "HidHideCLI did not respond."));
        }
        if (process.ExitCode != 0)
        {
            string raw = stderr.Result.Trim();
            if (IsAccessDenied(process.ExitCode, raw))
            {
                AppLog.Warn($"HidHideCLI access denied ({process.ExitCode}): {raw}");
                throw new IOException(L.T(
                    "HidHide 目前被其他程式占用（存取被拒）：可能是另一個 HidHide 工具，或卡住的 HidHideCLI 正在使用它。請關閉那些程式，或重新啟動電腦後再試。",
                    "HidHide is being held by something else (access denied): another HidHide tool or a stuck HidHideCLI is probably using it. Close it, or restart the PC, and try again."));
            }
            throw new IOException(L.T($"HidHideCLI 失敗（{process.ExitCode}）：{raw}", $"HidHideCLI failed ({process.ExitCode}): {raw}"));
        }
        return stdout.Result;
    }

    private static void TryAssignToJob(Process process)
    {
        try
        {
            if (Job.Value != 0) AssignProcessToJobObject(Job.Value, process.Handle);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private static nint CreateKillOnCloseJob()
    {
        try
        {
            nint job = CreateJobObject(0, null);
            if (job == 0) return 0;
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            nint ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)size)) return 0;
            }
            finally { Marshal.FreeHGlobal(ptr); }
            return job;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return 0; }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(nint job, int infoClass, nint info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);
}
