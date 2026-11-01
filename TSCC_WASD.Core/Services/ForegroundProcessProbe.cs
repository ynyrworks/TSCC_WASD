using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.Core.Services;

/// <summary>Reads the foreground window's process. Failures count as "no match" and are logged once.</summary>
public sealed class ForegroundProcessProbe
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private bool _logged;

    public ForegroundProcess? GetForeground()
    {
        try
        {
            nint hwnd = GetForegroundWindow();
            if (hwnd == 0) return null;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;
            if (pid == (uint)Environment.ProcessId) return new ForegroundProcess(AutoProfileSwitchRule.SelfProcessName, (int)pid);
            nint process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == 0) { LogOnce($"OpenProcess failed ({Marshal.GetLastWin32Error()})"); return null; }
            try
            {
                var buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                if (!QueryFullProcessImageNameW(process, 0, buffer, ref size))
                {
                    LogOnce($"QueryFullProcessImageName failed ({Marshal.GetLastWin32Error()})");
                    return null;
                }
                return new ForegroundProcess(buffer.ToString(0, (int)size), (int)pid);
            }
            finally { CloseHandle(process); }
        }
        catch (Exception ex)
        {
            LogOnce(ex.Message);
            return null;
        }
    }

    public static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch { return true; }
    }

    private void LogOnce(string detail)
    {
        if (_logged) return;
        _logged = true;
        AppLog.Warn($"Auto profile switch: cannot read the foreground process, treated as no match ({detail}).");
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(nint process, uint flags, StringBuilder name, ref uint size);
}
