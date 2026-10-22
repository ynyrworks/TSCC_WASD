using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using TSCC_WASD.Core.Models;

namespace TSCC_WASD.Core.Services.Setup;

/// <summary>
/// Hides physical controllers from games while mapping so only the virtual DS4 is visible,
/// then restores the user's previous HidHide configuration exactly.
/// Every change is recorded on disk before it is applied, so a crash is undone on next launch.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class HidHideGuard
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Func<IReadOnlyList<string>, string> _run;
    private readonly Func<IEnumerable<string>> _extraDevices;
    private readonly Func<string, bool> _isVirtual;
    private readonly string _recordPath;
    private readonly object _gate = new();

    /// <param name="extraDevices">
    /// Controller device paths found by Windows enumeration. They are the primary source; the CLI's
    /// --dev-gaming list only supplements them because it can be truncated (see WindowsDevicePaths).
    /// </param>
    /// <param name="isVirtual">
    /// True for ViGEm pads. --dev-gaming also lists TSCC_WASD's own virtual DS4 (054C:05C4) and
    /// Xbox 360 (045E:028E) output, which must stay visible to games.
    /// </param>
    public HidHideGuard(Func<IReadOnlyList<string>, string> run, string? recordPath = null,
        Func<IEnumerable<string>>? extraDevices = null, Func<string, bool>? isVirtual = null)
    {
        _run = run;
        _extraDevices = extraDevices ?? (() => []);
        _isVirtual = isVirtual ?? (_ => false);
        _recordPath = recordPath ?? AppPaths.File("hidhide-restore.json");
    }

    /// <summary>Returns null when HidHide is not installed.</summary>
    public static HidHideGuard? TryCreate()
    {
        string? cli = FindCli();
        return cli is null ? null : new HidHideGuard(args => RunCli(cli, args), extraDevices: PhysicalControllers,
            isVirtual: Input.WindowsDevicePaths.IsVirtual);
    }

    public bool HasPendingRestore => File.Exists(_recordPath);

    /// <summary>
    /// Allowlists <paramref name="appPath"/>, hides matching controllers (present or remembered) and turns cloaking on.
    /// Safe to call repeatedly; later calls only add newly seen devices.
    /// </summary>
    public string Hide(string appPath, InputDeviceType inputType)
    {
        lock (_gate)
        {
            if (Contains(_run(["--inv-state"]), "--inv-on"))
                return L.T("HidHide 為反向清單模式，未自動隱藏實體手把。", "HidHide is in inverse-list mode; physical controllers were not hidden.");

            var record = Load() ?? new Record
            {
                AppPath = appPath,
                AppAdded = !ParseQuoted(_run(["--app-list"]), "--app-reg")
                    .Contains(appPath, StringComparer.OrdinalIgnoreCase),
                CloakWasOn = Contains(_run(["--cloak-state"]), "--cloak-on")
            };
            var alreadyHidden = ParseQuoted(_run(["--dev-list"]), "--dev-hide").ToHashSet(StringComparer.OrdinalIgnoreCase);
            alreadyHidden.UnionWith(record.Devices);
            var targets = GamingDevicesFromCli().Concat(_extraDevices())
                .Where(path => Matches(path, inputType) && !alreadyHidden.Contains(path) && !_isVirtual(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            bool firstCall = !HasPendingRestore;
            if (!firstCall && targets.Count == 0) return Summary(record);

            record.Devices.AddRange(targets);
            Save(record);

            var args = new List<string>();
            if (firstCall && record.AppAdded) args.AddRange(["--app-reg", appPath]);
            foreach (var path in targets) args.AddRange(["--dev-hide", path]);
            if (firstCall) args.Add("--cloak-on");
            if (args.Count > 0) _run(args);
            return Summary(record);
        }
    }

    /// <summary>Undoes only what <see cref="Hide"/> changed. No-op if nothing is pending.</summary>
    public void Restore()
    {
        lock (_gate)
        {
            var record = Load();
            if (record is null) return;
            var args = new List<string>();
            foreach (var path in record.Devices) args.AddRange(["--dev-unhide", path]);
            if (record.AppAdded) args.AddRange(["--app-unreg", record.AppPath]);
            if (!record.CloakWasOn) args.Add("--cloak-off");
            if (args.Count > 0) _run(args);
            File.Delete(_recordPath);
        }
    }

    internal static bool Matches(string instancePath, InputDeviceType inputType)
    {
        bool ns2 = Contains(instancePath, "VID_057E&PID_2069");
        // Microsoft gaming HID devices are Xbox controllers (Bluetooth / xinputhid).
        bool xbox = Contains(instancePath, "VID_045E");
        bool known = Input.HidDevice.TryParseIds(instancePath, out ushort vid, out ushort pid, out _);
        bool playStation = known && vid == Input.WindowsDevicePaths.SonyVendor && Input.WindowsDevicePaths.PlayStationProducts.Contains(pid);
        bool switchPro = known && vid == Input.WindowsDevicePaths.NintendoVendor && pid == Input.WindowsDevicePaths.SwitchProProduct;
        return inputType switch
        {
            InputDeviceType.Switch2ProUsb => ns2,
            InputDeviceType.XInput => xbox,
            InputDeviceType.PlayStation => playStation,
            InputDeviceType.SwitchPro => switchPro,
            _ => ns2 || xbox || playStation || switchPro
        };
    }

    internal static IEnumerable<string> ParseGamingDevices(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<string>();
        foreach (var group in doc.RootElement.EnumerateArray())
        {
            if (!group.TryGetProperty("devices", out var devices)) continue;
            foreach (var device in devices.EnumerateArray())
                if (device.TryGetProperty("deviceInstancePath", out var path) && path.GetString() is { Length: > 0 } value)
                    result.Add(value);
        }
        return result;
    }

    internal static IEnumerable<string> ParseQuoted(string output, string command)
    {
        foreach (Match match in QuotedArgument().Matches(output))
            if (match.Groups[1].Value == command) yield return match.Groups[2].Value;
    }

    private static string Summary(Record record) => record.Devices.Count == 0
        ? L.T("HidHide 已啟用；目前沒有需要隱藏的實體手把。", "HidHide is on; no physical controllers needed hiding.")
        : L.T($"HidHide 已隱藏 {record.Devices.Count} 個實體手把介面，遊戲只會看到虛擬 DS4。", $"HidHide hid {record.Devices.Count} physical controller interface(s); games only see the virtual DS4.");

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private Record? Load()
    {
        if (!File.Exists(_recordPath)) return null;
        return JsonSerializer.Deserialize<Record>(File.ReadAllText(_recordPath))
            ?? throw new InvalidDataException(L.T("HidHide 還原紀錄無效。", "The HidHide restore record is invalid."));
    }

    private void Save(Record record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_recordPath)!);
        File.WriteAllText(_recordPath, JsonSerializer.Serialize(record, JsonOptions));
    }

    private IEnumerable<string> GamingDevicesFromCli()
    {
        try { return ParseGamingDevices(_run(["--dev-gaming"])).ToList(); }
        catch (JsonException) { return []; } // Truncated output; Windows enumeration still covers the devices.
    }

    private static IEnumerable<string> PhysicalControllers()
    {
        try { return Input.WindowsDevicePaths.FindPhysicalControllers(); }
        catch { return []; } // Enumeration problems must not block hiding what the CLI reports.
    }

    private static string? FindCli()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Nefarius Software Solutions e.U.\HidHide");
            if (key?.GetValue("Path") is string root)
            {
                string path = Path.Combine(root, "x64", "HidHideCLI.exe");
                if (File.Exists(path)) return path;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { }
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe");
        return File.Exists(fallback) ? fallback : null;
    }

    private static string RunCli(string cli, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(cli)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException(L.T("無法啟動 HidHideCLI。", "Could not start HidHideCLI."));
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            try { process.Kill(); } catch { /* Already exited. */ }
            throw new TimeoutException(L.T("HidHideCLI 沒有回應。", "HidHideCLI did not respond."));
        }
        if (process.ExitCode != 0)
            throw new IOException(L.T($"HidHideCLI 失敗（{process.ExitCode}）：{stderr.Result.Trim()}", $"HidHideCLI failed ({process.ExitCode}): {stderr.Result.Trim()}"));
        return stdout.Result;
    }

    [GeneratedRegex("(--[a-z-]+)\\s+\"([^\"]+)\"")]
    private static partial Regex QuotedArgument();

    private sealed class Record
    {
        public string AppPath { get; set; } = "";
        public bool AppAdded { get; set; }
        public bool CloakWasOn { get; set; }
        public List<string> Devices { get; set; } = new();
    }
}
