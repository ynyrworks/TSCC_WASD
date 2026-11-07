namespace TSCC_WASD.Core.Services.Input;

public enum BatteryLevel { Empty, Critical, Low, Medium, Full }

/// <summary>Whether a battery reading is available at all.</summary>
public enum BatteryAvailability
{
    /// <summary>Nothing read yet, or the controller is not connected / the read failed.</summary>
    Unknown,
    /// <summary>The connection cannot report a battery (for example a wired Xbox pad).</summary>
    Unsupported,
    Known
}

/// <summary>Battery of the physical controller. <see cref="Level"/> and <see cref="Charging"/> only mean something when <see cref="Availability"/> is Known.</summary>
public readonly record struct BatteryStatus(BatteryAvailability Availability, BatteryLevel Level, bool Charging)
{
    public static BatteryStatus Unknown => default;
    public static BatteryStatus Unsupported => new(BatteryAvailability.Unsupported, default, false);
    public static BatteryStatus Of(BatteryLevel level, bool charging) => new(BatteryAvailability.Known, level, charging);
    public bool IsKnown => Availability == BatteryAvailability.Known;
}

/// <summary>Input that reports the battery of the controller it reads. Never throws or blocks.</summary>
public interface IBatterySource
{
    BatteryStatus Battery { get; }
}

/// <summary>Battery decoding shared by the readers; every parser is pure so it can be tested with sample bytes.</summary>
public static class BatteryParser
{
    /// <summary>
    /// Maps a 0–10 charge step (DS4 / DualSense) onto five levels. Tentative thresholds:
    /// 0 Empty, 1–2 Critical, 3–4 Low, 5–7 Medium, 8–10 Full.
    /// </summary>
    public static BatteryLevel FromTenSteps(int step) => step switch
    {
        <= 0 => BatteryLevel.Empty,
        <= 2 => BatteryLevel.Critical,
        <= 4 => BatteryLevel.Low,
        <= 7 => BatteryLevel.Medium,
        _ => BatteryLevel.Full
    };

    /// <summary>
    /// DS4 status byte (USB report offset 30; Bluetooth 0x11 report offset 32): low nibble level 0–10
    /// (11 = full while on the cable), bit 4 = cable connected. On the cable it is charging until 11.
    /// </summary>
    public static BatteryStatus FromDualShock4(byte status)
    {
        int nibble = status & 0x0F;
        bool cable = (status & 0x10) != 0;
        return BatteryStatus.Of(FromTenSteps(Math.Min(nibble, 10)), cable && nibble < 11);
    }

    /// <summary>
    /// DualSense status byte (USB report offset 53; Bluetooth 0x31 report offset 54): low nibble level 0–10,
    /// high nibble 0 discharging, 1 charging, 2 full. Other high nibbles (errors) are unknown.
    /// </summary>
    public static BatteryStatus FromDualSense(byte status)
    {
        int nibble = status & 0x0F;
        return (status >> 4) switch
        {
            0 => BatteryStatus.Of(FromTenSteps(Math.Min(nibble, 10)), false),
            1 => BatteryStatus.Of(FromTenSteps(Math.Min(nibble, 10)), true),
            2 => BatteryStatus.Of(BatteryLevel.Full, false),
            _ => BatteryStatus.Unknown
        };
    }

    /// <summary>Switch Pro report 0x30 byte 2: bits 7–5 level 0–4, bit 4 charging. The low bit is ignored.</summary>
    public static BatteryStatus FromSwitchPro(byte status) =>
        BatteryStatus.Of((BatteryLevel)Math.Min(status >> 5, (int)BatteryLevel.Full), (status & 0x10) != 0);

    // TENTATIVE: derived from a few NS2 Pro captures (3553 mV = low), not from a published table.
    private const int Ns2FullMv = 3900, Ns2MediumMv = 3700, Ns2LowMv = 3500, Ns2CriticalMv = 3400;

    public static BatteryLevel FromNs2Millivolts(int millivolts) =>
        millivolts >= Ns2FullMv ? BatteryLevel.Full
        : millivolts >= Ns2MediumMv ? BatteryLevel.Medium
        : millivolts >= Ns2LowMv ? BatteryLevel.Low
        : millivolts >= Ns2CriticalMv ? BatteryLevel.Critical
        : BatteryLevel.Empty;

    /// <summary>
    /// NS2 Pro USB report 0x05 (64 bytes): voltage uint16 LE at 0x20 (mV), charging flag at 0x22 (tentative).
    /// A Bluetooth report has no report ID, so the same fields sit one byte earlier (0x1F, 0x21).
    /// </summary>
    public static bool TryParseNs2(ReadOnlySpan<byte> report, bool bluetooth, out BatteryStatus battery)
    {
        battery = default;
        int shift = bluetooth ? 1 : 0;
        if (bluetooth ? report.Length < 63 : (report.Length != 64 || report[0] != 0x05)) return false;
        int millivolts = report[0x20 - shift] | (report[0x21 - shift] << 8);
        if (millivolts == 0) return false;
        battery = BatteryStatus.Of(FromNs2Millivolts(millivolts), report[0x22 - shift] != 0);
        return true;
    }
}
