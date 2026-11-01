using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.Setup;

/// <summary>
/// Calls <paramref name="onArrival"/> when a HID or XUSB device interface arrives, using
/// CM_Register_Notification. Throws <see cref="Win32Exception"/> when registration fails.
/// Dispose unregisters and waits for a running callback, so do not dispose from inside it.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DeviceArrivalWatcher : IDisposable
{
    internal static readonly Guid Xusb = new("ec87f1e3-c13b-4100-b5f7-8b84d54260cb");

    private const int FilterTypeDeviceInterface = 0, ActionInterfaceArrival = 0;

    [StructLayout(LayoutKind.Explicit, Size = 416)]
    private struct NotifyFilter
    {
        [FieldOffset(0)] public int Size;
        [FieldOffset(4)] public int Flags;
        [FieldOffset(8)] public int FilterType;
        [FieldOffset(16)] public Guid ClassGuid;
    }

    private delegate int NotifyCallback(IntPtr notify, IntPtr context, int action, IntPtr eventData, int eventDataSize);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Register_Notification(ref NotifyFilter filter, IntPtr context, NotifyCallback callback, out IntPtr notify);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Unregister_Notification(IntPtr notify);

    private readonly NotifyCallback _callback;
    private readonly List<IntPtr> _handles = [];

    public DeviceArrivalWatcher(Action onArrival)
    {
        _callback = (_, _, action, _, _) =>
        {
            if (action == ActionInterfaceArrival)
            {
                try { onArrival(); } catch { /* Never throw into the system callback. */ }
            }
            return 0;
        };
        try
        {
            foreach (var guid in new[] { WindowsDevicePaths.Hid, Xusb })
            {
                var filter = new NotifyFilter { Size = 416, FilterType = FilterTypeDeviceInterface, ClassGuid = guid };
                uint result = CM_Register_Notification(ref filter, IntPtr.Zero, _callback, out var handle);
                if (result != 0) throw new Win32Exception((int)result, $"CM_Register_Notification failed: CONFIGRET 0x{result:X}.");
                _handles.Add(handle);
            }
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (var handle in _handles) CM_Unregister_Notification(handle);
        _handles.Clear();
    }
}
