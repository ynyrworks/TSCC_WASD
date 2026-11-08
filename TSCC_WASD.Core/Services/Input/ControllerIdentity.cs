namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Stable identity of a physical controller. <see cref="Model"/> is one of the constants below;
/// <see cref="DeviceId"/> tells two controllers of the same model apart when the reader can (HID instance id,
/// Bluetooth address), and is null otherwise (XInput has no serial and its slot is not stable).
/// </summary>
public sealed record ControllerId(string Model, string? DeviceId = null, string? LegacyDeviceId = null)
{
    public const string Ns2Pro = "ns2pro";
    public const string DualShock4 = "dualshock4";
    public const string DualSense = "dualsense";
    public const string SwitchPro = "switchpro";
    public const string XInput = "xinput";

    /// <summary>Key for every controller of this model.</summary>
    public string ModelKey => Model;

    /// <summary>Key for this exact device, or null when the reader has no per-device id.</summary>
    public string? DeviceKey => string.IsNullOrWhiteSpace(DeviceId) ? null : $"{Model}#{DeviceId.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Key an older version used for this device (the HID instance id, which changes when a Bluetooth controller is
    /// re-paired), or null when it is the same as <see cref="DeviceKey"/> or unknown.
    /// </summary>
    public string? LegacyDeviceKey =>
        string.IsNullOrWhiteSpace(LegacyDeviceId) || LegacyDeviceKey_Same() ? null : $"{Model}#{LegacyDeviceId.Trim().ToLowerInvariant()}";

    private bool LegacyDeviceKey_Same() =>
        string.Equals(LegacyDeviceId?.Trim(), DeviceId?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The key a new calibration is saved under: the device when it can be told apart, else the model.</summary>
    public string PreferredKey => DeviceKey ?? ModelKey;

    /// <summary>Lookup order: the per-device key, the legacy per-device key, then the model key.</summary>
    public IEnumerable<string> LookupKeys()
    {
        if (DeviceKey is { } device) yield return device;
        if (LegacyDeviceKey is { } legacy) yield return legacy;
        yield return ModelKey;
    }

    /// <summary>
    /// Identity for a HID interface: over Bluetooth the device id is the Bluetooth address (stable across re-pairing)
    /// when it can be found, with the HID instance id kept as the legacy id; over USB it is the instance id.
    /// Does Windows calls, so call it once when the controller connects, never per input report.
    /// </summary>
    public static ControllerId ForHid(string model, string devicePath, bool bluetooth) =>
        ForHid(model, HidDevice.InstanceId(devicePath), bluetooth, WindowsDevicePaths.ParentOf);

    internal static ControllerId ForHid(string model, string? instanceId, bool bluetooth, Func<string, string?> parentOf)
    {
        string? address = bluetooth && instanceId is not null ? HidDevice.BluetoothAddress(instanceId, parentOf) : null;
        return address is null ? new ControllerId(model, instanceId) : new ControllerId(model, address, instanceId);
    }

    /// <summary>Model part of a key made by <see cref="DeviceKey"/> or <see cref="ModelKey"/>.</summary>
    public static string ModelOfKey(string key)
    {
        int cut = key.IndexOf('#');
        return cut < 0 ? key : key[..cut];
    }
}

/// <summary>Input that knows which controller it currently reads. Never throws or blocks; null while none is connected.</summary>
public interface IControllerIdentity
{
    ControllerId? Identity { get; }
}
