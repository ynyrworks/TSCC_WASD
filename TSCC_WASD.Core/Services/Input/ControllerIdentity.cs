namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Stable identity of a physical controller. <see cref="Model"/> is one of the constants below;
/// <see cref="DeviceId"/> tells two controllers of the same model apart when the reader can (HID instance id,
/// Bluetooth address), and is null otherwise (XInput has no serial and its slot is not stable).
/// </summary>
public sealed record ControllerId(string Model, string? DeviceId = null)
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

    /// <summary>The key a new calibration is saved under: the device when it can be told apart, else the model.</summary>
    public string PreferredKey => DeviceKey ?? ModelKey;

    /// <summary>Lookup order: the per-device key, then the model key.</summary>
    public IEnumerable<string> LookupKeys()
    {
        if (DeviceKey is { } device) yield return device;
        yield return ModelKey;
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
