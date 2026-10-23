using System.Runtime.InteropServices;

namespace TSCC_WASD.Core.Services.Input;

internal static class WindowsDevicePaths
{
    internal static readonly Guid Hid = new("4d1e55b2-f16f-11cf-88cb-001111000030");
    // DeviceInterfaceGUID advertised by Switch 2 Pro's Microsoft OS descriptors.
    internal static readonly Guid Switch2Usb = new("6f13725e-ef0e-4fd3-ae5f-b2de989ec825");

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_Interface_List_SizeW(out uint length, ref Guid guid, string? device, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_Interface_ListW(ref Guid guid, string? device, [Out] char[] buffer, uint length, uint flags);

    internal static string[] Find(Guid guid) =>
        [.. FindAll(guid).Where(p => p.Contains("vid_057e&pid_2069", StringComparison.OrdinalIgnoreCase))];

    /// <summary>Every present interface of the class <paramref name="guid"/>.</summary>
    internal static string[] FindAll(Guid guid)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            uint result = CM_Get_Device_Interface_List_SizeW(out uint length, ref guid, null, 0);
            if (result != 0) throw new IOException($"Device enumeration failed: CONFIGRET 0x{result:X}.");
            var buffer = new char[length];
            result = CM_Get_Device_Interface_ListW(ref guid, null, buffer, length, 0);
            if (result == 0) return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (result != 0x1A) throw new IOException($"Device enumeration failed: CONFIGRET 0x{result:X}.");
        }
        return [];
    }

    /// <summary>Sony DualShock 4 (v1, v2, wireless adapter) and DualSense (and Edge) product IDs.</summary>
    internal static readonly ushort[] PlayStationProducts = [0x05C4, 0x09CC, 0x0BA0, 0x0CE6, 0x0DF2];
    internal const ushort SonyVendor = 0x054C, NintendoVendor = 0x057E, SwitchProProduct = 0x2009;

    /// <summary>True for device or interface IDs of the controllers TSCC_WASD reads and may hide.</summary>
    internal static bool IsSupportedController(string id)
    {
        if (id.Contains("VID_057E&PID_2069", StringComparison.OrdinalIgnoreCase)) return true;
        // Microsoft gaming devices are Xbox controllers (USB, XUSB/GIP, Bluetooth via xinputhid).
        if (id.Contains("VID_045E", StringComparison.OrdinalIgnoreCase)) return true;
        if (!HidDevice.TryParseIds(id, out ushort vid, out ushort pid, out _)) return false;
        return (vid == SonyVendor && PlayStationProducts.Contains(pid)) || (vid == NintendoVendor && pid == SwitchProProduct);
    }

    // Device setup classes HidHide filters: HID, and those used by wired Xbox controllers (XUSB / GIP).
    private const string HidClass = "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}";
    private const string XnaCompositeClass = "{d61ca365-5af4-4486-998b-9db4734c6ca3}";
    private const string XboxCompositeClass = "{05f5cfe2-4733-4950-a6bb-07aad01a3a84}";
    private const uint FilterClass = 0x200, LocatePhantom = 1;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_IDW(uint devInst, [Out] char[] buffer, uint length, uint flags);

    /// <summary>
    /// Physical NS2 Pro and Microsoft (Xbox) controller devices, present or remembered, over USB,
    /// Bluetooth or wired XUSB/GIP. Virtual pads created by ViGEm (for example DS4Windows' Xbox 360
    /// output) hang off a root-enumerated bus and are excluded. Callers filter by controller type.
    /// </summary>
    /// <remarks>
    /// Enumerated here rather than parsed from <c>HidHideCLI --dev-gaming</c>: the CLI stops writing
    /// mid-JSON when a device description is not ASCII (for example a localized Windows), which once
    /// left every controller visible.
    /// </remarks>
    internal static string[] FindPhysicalControllers() =>
        // HID: only the HID interfaces themselves (as HidHide lists them), not their USB function nodes.
        [.. ListClass(HidClass).Where(id => id.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase))
            .Concat(ListClass(XnaCompositeClass)).Concat(ListClass(XboxCompositeClass))
            .Where(IsSupportedController)
            .Where(id => !IsVirtual(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Walks up the device tree until it meets the bus the device hangs off. ViGEm pads (ours and
    /// DS4Windows') sit on a root-enumerated bus (ROOT\SYSTEM\xxxx), the Xbox 360 pad's HID child
    /// three levels below it; a leftover pad that is no longer present can also report HTREE\ROOT\0.
    /// Physical controllers reach a USB root hub, PCI or the Bluetooth stack. The last known parent
    /// is used, so remembered (non-present) devices are classified too. Unknown parents count as
    /// virtual so nothing questionable is hidden.
    /// </summary>
    internal static bool IsVirtual(string deviceId) => IsVirtual(deviceId, ParentOf);

    internal static bool IsVirtual(string deviceId, Func<string, string?> parentOf)
    {
        string? current = deviceId;
        for (int level = 0; level < 8; level++)
        {
            current = parentOf(current);
            if (current is null || StartsWithAny(current, VirtualBuses)) return true;
            if (StartsWithAny(current, PhysicalBuses)) return false;
        }
        return true;
    }

    private static readonly string[] VirtualBuses = [@"ROOT\", @"HTREE\ROOT"];
    private static readonly string[] PhysicalBuses = [@"USB\ROOT_HUB", @"PCI\", @"ACPI\", @"BTH\", @"BTHENUM\", @"BTHLE\", @"BTHLEDEVICE\"];

    private static bool StartsWithAny(string value, string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ListClass(string classGuid)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            if (CM_Get_Device_ID_List_SizeW(out uint length, classGuid, FilterClass) != 0) return [];
            var buffer = new char[length];
            uint result = CM_Get_Device_ID_ListW(classGuid, buffer, length, FilterClass);
            if (result == 0) return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (result != 0x1A) return [];
        }
        return [];
    }

    /// <summary>DEVPKEY_Device_Parent: unlike CM_Get_Parent, it keeps the last parent of a device that is not present.</summary>
    private static string? ParentOf(string deviceId)
    {
        if (CM_Locate_DevNodeW(out uint node, deviceId, LocatePhantom) != 0) return null;
        var key = new DevPropKey { FormatId = new Guid("4340a6c5-93fa-4706-972c-7b648008a5a7"), PropertyId = 8 };
        var property = new byte[800];
        uint size = (uint)property.Length;
        if (CM_Get_DevNode_PropertyW(node, ref key, out uint type, property, ref size, 0) == 0 && type == 0x12 /* DEVPROP_TYPE_STRING */)
            return System.Text.Encoding.Unicode.GetString(property, 0, (int)size).TrimEnd('\0');
        if (CM_Get_Parent(out uint parent, node, 0) != 0) return null;
        var buffer = new char[400];
        return CM_Get_Device_IDW(parent, buffer, (uint)buffer.Length, 0) == 0
            ? new string(buffer).TrimEnd('\0') : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_PropertyW(uint devInst, ref DevPropKey key, out uint type, [Out] byte[] buffer, ref uint size, uint flags);
}
