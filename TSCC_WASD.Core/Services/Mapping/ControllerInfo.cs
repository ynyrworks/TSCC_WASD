using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.Mapping;

public enum ControllerConnection { Unknown, Usb, Bluetooth }

/// <summary>A reader that describes its own controller; takes precedence over the type-based description.</summary>
public interface IControllerInfoSource
{
    ControllerInfo? Controller { get; }
}

/// <summary>Display name (Xbox controller, DualShock 4, DualSense, Switch Pro, NS2 Pro) and connection of the controller being read.</summary>
public sealed record ControllerInfo(string Name, ControllerConnection Connection)
{
    /// <summary>
    /// Describes a reader. The type decides the name where it is fixed; the PlayStation and Switch Pro readers
    /// cover USB and Bluetooth, so their status text (which names the connection once connected) decides there.
    /// Null for a null or unknown reader.
    /// </summary>
    public static ControllerInfo? Describe(IInputReader? reader)
    {
        switch (reader)
        {
            case null: return null;
            case IControllerInfoSource own: return own.Controller;
            case Switch2InputReader: return new("NS2 Pro", ControllerConnection.Usb);
            case Switch2BleInputReader: return new("NS2 Pro", ControllerConnection.Bluetooth);
            case XInputReader: return new(L.T("Xbox 手把", "Xbox controller"), ControllerConnection.Unknown);
            case PlayStationInputReader:
                string status = reader.Status;
                bool sense = status.Contains("DualSense", StringComparison.OrdinalIgnoreCase);
                bool shock = status.Contains("DualShock", StringComparison.OrdinalIgnoreCase);
                string name = sense == shock ? "DualShock 4／DualSense" : sense ? "DualSense" : "DualShock 4";
                return new(name, ConnectionFromStatus(status));
            case SwitchProInputReader: return new("Switch Pro", ConnectionFromStatus(reader.Status));
            default: return null;
        }
    }

    /// <summary>Unknown when the text names neither or both (the waiting text mentions both).</summary>
    internal static ControllerConnection ConnectionFromStatus(string status)
    {
        bool usb = status.Contains("USB", StringComparison.OrdinalIgnoreCase);
        bool bluetooth = status.Contains("藍牙") || status.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
        return usb == bluetooth ? ControllerConnection.Unknown : usb ? ControllerConnection.Usb : ControllerConnection.Bluetooth;
    }
}
