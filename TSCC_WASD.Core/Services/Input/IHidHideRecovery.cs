using Microsoft.Win32.SafeHandles;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// What a reader may ask of HidHide when it refuses access to a controller TSCC_WASD hid itself.
/// </summary>
public interface IHidHideRecovery
{
    /// <summary>True when TSCC_WASD hid the device behind this HID interface path (it is in the restore record).</summary>
    bool IsHiddenByUs(string hidInterfacePath);

    /// <summary>Registers this app with HidHide again.</summary>
    void ReRegisterApp();

    /// <summary>
    /// Unhides that one device, runs <paramref name="open"/>, and hides it again before returning (also if it throws).
    /// Throws <see cref="InvalidOperationException"/> without any CLI call when TSCC_WASD did not hide the device.
    /// </summary>
    SafeFileHandle OpenWhileUnhidden(string hidInterfacePath, Func<SafeFileHandle> open);
}
