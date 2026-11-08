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
            default: return Describe(reader.GetType(), reader.Status);
        }
    }

    /// <summary>The mapping itself, by reader type and status text, so it can be tested without opening hardware.</summary>
    internal static ControllerInfo? Describe(Type readerType, string status)
    {
        if (readerType == typeof(Switch2InputReader)) return new("NS2 Pro", ControllerConnection.Usb);
        if (readerType == typeof(Switch2BleInputReader)) return new("NS2 Pro", ControllerConnection.Bluetooth);
        if (readerType == typeof(XInputReader)) return new(L.T("Xbox 手把", "Xbox controller"), ControllerConnection.Unknown);
        if (readerType == typeof(SwitchProInputReader)) return new("Switch Pro", ConnectionFromStatus(status));
        if (readerType == typeof(PlayStationInputReader))
        {
            bool sense = status.Contains("DualSense", StringComparison.OrdinalIgnoreCase);
            bool shock = status.Contains("DualShock", StringComparison.OrdinalIgnoreCase);
            string name = sense == shock ? "DualShock 4／DualSense" : sense ? "DualSense" : "DualShock 4";
            return new(name, ConnectionFromStatus(status));
        }
        return null;
    }

    /// <summary>Unknown when the text names neither or both (the waiting text mentions both).</summary>
    internal static ControllerConnection ConnectionFromStatus(string status)
    {
        bool usb = status.Contains("USB", StringComparison.OrdinalIgnoreCase);
        bool bluetooth = status.Contains("藍牙") || status.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
        return usb == bluetooth ? ControllerConnection.Unknown : usb ? ControllerConnection.Usb : ControllerConnection.Bluetooth;
    }
}
