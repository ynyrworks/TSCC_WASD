using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>A HID controller interface found by vendor and product ID, over USB or Bluetooth.</summary>
public sealed record HidControllerPath(string Path, ushort VendorId, ushort ProductId, bool Bluetooth);

/// <summary>
/// Finds and opens HID game controllers. Interfaces are opened shared, so Steam or another tool can
/// keep using the controller; TSCC_WASD only needs the input reports (and best-effort rumble writes).
/// </summary>
public static partial class HidDevice
{
    /// <summary>
    /// Present physical HID interfaces whose vendor/product pair is in <paramref name="products"/>.
    /// Virtual pads are skipped: TSCC_WASD's own DualShock 4 output (or DS4Windows') reports as a real
    /// 054C:05C4 controller and would otherwise be read back as input.
    /// </summary>
    public static IReadOnlyList<HidControllerPath> Find(ushort vendorId, IReadOnlyCollection<ushort> products)
    {
        var result = new List<HidControllerPath>();
        foreach (var path in WindowsDevicePaths.FindAll(WindowsDevicePaths.Hid))
            if (TryParseIds(path, out ushort vid, out ushort pid, out bool bluetooth) && vid == vendorId && products.Contains(pid)
                && InstanceId(path) is { } id && !WindowsDevicePaths.IsVirtual(id))
                result.Add(new HidControllerPath(path, vid, pid, bluetooth));
        return result;
    }

    /// <summary>
    /// Interface path to device instance ID: <c>\\?\hid#vid_054c&amp;pid_05c4#7&amp;1&amp;0&amp;0000#{guid}</c>
    /// becomes <c>hid\vid_054c&amp;pid_05c4\7&amp;1&amp;0&amp;0000</c>.
    /// </summary>
    public static string? InstanceId(string interfacePath)
    {
        string path = interfacePath.StartsWith(@"\\?\", StringComparison.Ordinal) ? interfacePath[4..] : interfacePath;
        int guid = path.LastIndexOf("#{", StringComparison.Ordinal);
        if (guid <= 0) return null;
        return path[..guid].Replace('#', '\\');
    }

    /// <summary>
    /// Bluetooth address (12 lowercase hex digits) of a classic Bluetooth HID device: walks up from the HID instance id
    /// to its <c>BTHENUM\</c> parent, whose id ends in <c>…&amp;BCCE25C5FF8E_C00000000</c>. Null for USB, Bluetooth LE,
    /// unknown parents or malformed ids.
    /// </summary>
    internal static string? BluetoothAddress(string instanceId, Func<string, string?> parentOf)
    {
        string? current = instanceId;
        for (int level = 0; level < 4; level++)
        {
            try { current = parentOf(current); }
            catch { return null; }
            if (current is null) return null;
            if (current.StartsWith(@"BTHENUM\", StringComparison.OrdinalIgnoreCase))
            {
                var match = BthEnumAddress().Match(current);
                return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
            }
        }
        return null;
    }

    [System.Text.RegularExpressions.GeneratedRegex("[&_]([0-9a-f]{12})_c[0-9a-f]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex BthEnumAddress();

    /// <summary>
    /// Reads VID/PID from a HID interface path. USB: <c>hid#vid_054c&amp;pid_05c4…</c>.
    /// Bluetooth: <c>hid#{00001124-…}_vid&amp;0002054c_pid&amp;05c4…</c> (the 0002 prefix is the Bluetooth vendor source).
    /// </summary>
    public static bool TryParseIds(string path, out ushort vendorId, out ushort productId, out bool bluetooth)
    {
        vendorId = productId = 0;
        bluetooth = path.Contains("{00001124-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase);
        var match = IdPattern().Match(path);
        if (!match.Success) return false;
        string vid = match.Groups[1].Value;
        return ushort.TryParse(vid.AsSpan(vid.Length - 4), System.Globalization.NumberStyles.HexNumber, null, out vendorId)
            && ushort.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out productId);
    }

    // USB "vid_054c&pid_05c4", Bluetooth "vid&0002054c_pid&05c4", Bluetooth LE "vid&02045e_pid&0b13".
    [System.Text.RegularExpressions.GeneratedRegex("vid[_&]([0-9a-f]{4,8})[_&]pid[_&]([0-9a-f]{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex IdPattern();

    /// <summary>Opens the interface for overlapped reading, shared with other programs.</summary>
    public static FileStream OpenRead(string path, out int inputReportLength)
    {
        var handle = CreateFileW(path, 0x80000000 /* GENERIC_READ */, 3, IntPtr.Zero, 3, 0x40000000 /* OVERLAPPED */, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        inputReportLength = Capabilities(handle).InputReportByteLength;
        return new FileStream(handle, FileAccess.Read, Math.Max(1, inputReportLength), isAsync: true);
    }

    /// <summary>Opens the interface for writing output reports; null when the device refuses (rumble is optional).</summary>
    public static FileStream? TryOpenWrite(string path, out int outputReportLength)
    {
        outputReportLength = 0;
        var handle = CreateFileW(path, 0x40000000 /* GENERIC_WRITE */, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        outputReportLength = Capabilities(handle).OutputReportByteLength;
        return new FileStream(handle, FileAccess.Write, 0, isAsync: false);
    }

    private static HidpCaps Capabilities(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out IntPtr data)) return default;
        try { return HidP_GetCaps(data, out var caps) == 0x00110000 /* HIDP_STATUS_SUCCESS */ ? caps : default; }
        finally { HidD_FreePreparsedData(data); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
    [DllImport("hid.dll")]
    private static extern uint HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);
}
