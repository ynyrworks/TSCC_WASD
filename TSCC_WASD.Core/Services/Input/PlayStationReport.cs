using System.Buffers.Binary;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Parses DualShock 4 and DualSense input reports and builds their rumble output reports.
/// Layouts follow SDL's hidapi PS4/PS5 drivers (zlib) and the public DS4 / DualSense documentation.
/// Buttons map by position: Cross→A, Circle→B, Square→X, Triangle→Y, Share/Create→Back,
/// Options→Start, PS→Guide, touchpad click→Touchpad.
/// </summary>
public static class PlayStationReport
{
    /// <summary>DualSense (and Edge) product IDs; every other supported Sony ID is a DualShock 4.</summary>
    public static bool IsDualSense(ushort productId) => productId is 0x0CE6 or 0x0DF2;

    /// <summary>
    /// Accepts the reports a controller sends over USB and Bluetooth:
    /// DS4 USB 0x01 (64 bytes), DS4 Bluetooth 0x11 (full) or 0x01 (basic), DualSense USB 0x01 (64 bytes),
    /// DualSense Bluetooth 0x31 (full) or 0x01 (basic). Basic Bluetooth reports carry no motion.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> report, bool dualSense, out State state, out MotionSample? motion)
    {
        state = default;
        motion = null;
        if (report.Length < 10) return false;
        switch (report[0])
        {
            case 0x01 when report.Length < 30:
                // Basic Bluetooth report, same layout for DS4 and DualSense: sticks, buttons, triggers.
                state = BasicLayout(report[1..]);
                return true;
            case 0x01 when !dualSense:
                state = BasicLayout(report[1..]);
                motion = Motion(report[13..]);
                return true;
            case 0x11 when !dualSense && report.Length >= 25:
                state = BasicLayout(report[3..]);
                motion = Motion(report[15..]);
                return true;
            case 0x01 when dualSense && report.Length >= 28:
                state = DualSenseLayout(report[1..]);
                motion = Motion(report[16..]);
                return true;
            case 0x31 when dualSense && report.Length >= 29:
                state = DualSenseLayout(report[2..]);
                motion = Motion(report[17..]);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Battery from the full reports only (the basic Bluetooth report has none). The status byte sits at
    /// DS4 USB 30 / BT 0x11 32 and DualSense USB 53 / BT 0x31 54: the same shift the layouts above use.
    /// </summary>
    public static bool TryParseBattery(ReadOnlySpan<byte> report, bool dualSense, out BatteryStatus battery)
    {
        battery = default;
        if (report.Length == 0) return false;
        int offset = (report[0], dualSense) switch
        {
            (0x01, false) => 30,
            (0x11, false) => 32,
            (0x01, true) => 53,
            (0x31, true) => 54,
            _ => -1
        };
        if (offset < 0 || report.Length <= offset) return false;
        battery = dualSense ? BatteryParser.FromDualSense(report[offset]) : BatteryParser.FromDualShock4(report[offset]);
        return true;
    }

    /// <summary>DS4 layout (also used by both controllers' basic Bluetooth report), starting at the left stick X byte.</summary>
    private static State BasicLayout(ReadOnlySpan<byte> d)
    {
        var buttons = Hat(d[4] & 0x0F) | Face(d[4]);
        buttons |= Shoulders(d[5]);
        if ((d[6] & 0x01) != 0) buttons |= GamepadButtonFlags.Guide;
        if ((d[6] & 0x02) != 0) buttons |= GamepadButtonFlags.Touchpad;
        return Build(buttons, d[0], d[1], d[2], d[3], d[7], d[8]);
    }

    /// <summary>DualSense full layout, starting at the left stick X byte.</summary>
    private static State DualSenseLayout(ReadOnlySpan<byte> d)
    {
        var buttons = Hat(d[7] & 0x0F) | Face(d[7]);
        buttons |= Shoulders(d[8]);
        if ((d[9] & 0x01) != 0) buttons |= GamepadButtonFlags.Guide;
        if ((d[9] & 0x02) != 0) buttons |= GamepadButtonFlags.Touchpad;
        return Build(buttons, d[0], d[1], d[2], d[3], d[4], d[5]);
    }

    private static GamepadButtonFlags Face(byte value)
    {
        var buttons = GamepadButtonFlags.None;
        if ((value & 0x10) != 0) buttons |= GamepadButtonFlags.X; // Square
        if ((value & 0x20) != 0) buttons |= GamepadButtonFlags.A; // Cross
        if ((value & 0x40) != 0) buttons |= GamepadButtonFlags.B; // Circle
        if ((value & 0x80) != 0) buttons |= GamepadButtonFlags.Y; // Triangle
        return buttons;
    }

    private static GamepadButtonFlags Shoulders(byte value)
    {
        var buttons = GamepadButtonFlags.None;
        if ((value & 0x01) != 0) buttons |= GamepadButtonFlags.LeftShoulder;
        if ((value & 0x02) != 0) buttons |= GamepadButtonFlags.RightShoulder;
        if ((value & 0x10) != 0) buttons |= GamepadButtonFlags.Back;  // Share / Create
        if ((value & 0x20) != 0) buttons |= GamepadButtonFlags.Start; // Options
        if ((value & 0x40) != 0) buttons |= GamepadButtonFlags.LeftThumb;
        if ((value & 0x80) != 0) buttons |= GamepadButtonFlags.RightThumb;
        return buttons;
    }

    /// <summary>Hat switch: 0 = up, clockwise to 7 = up-left, 8 = released.</summary>
    private static GamepadButtonFlags Hat(int hat) => hat switch
    {
        0 => GamepadButtonFlags.DPadUp,
        1 => GamepadButtonFlags.DPadUp | GamepadButtonFlags.DPadRight,
        2 => GamepadButtonFlags.DPadRight,
        3 => GamepadButtonFlags.DPadDown | GamepadButtonFlags.DPadRight,
        4 => GamepadButtonFlags.DPadDown,
        5 => GamepadButtonFlags.DPadDown | GamepadButtonFlags.DPadLeft,
        6 => GamepadButtonFlags.DPadLeft,
        7 => GamepadButtonFlags.DPadUp | GamepadButtonFlags.DPadLeft,
        _ => GamepadButtonFlags.None
    };

    private static State Build(GamepadButtonFlags buttons, byte lx, byte ly, byte rx, byte ry, byte l2, byte r2) => new()
    {
        Gamepad = new Gamepad
        {
            Buttons = buttons,
            LeftThumbX = Axis(lx), LeftThumbY = Axis(ly, invert: true),
            RightThumbX = Axis(rx), RightThumbY = Axis(ry, invert: true),
            LeftTrigger = l2, RightTrigger = r2
        }
    };

    /// <summary>0–255 with 128 centred (down is 255 on Y) to XInput's signed range (up is positive).</summary>
    public static short Axis(byte value, bool invert = false)
    {
        int centred = invert ? Math.Min(127, 128 - value) : value - 128; // 128 is exactly centre either way.
        return (short)(centred < 0 ? centred * 256 : centred * short.MaxValue / 127);
    }

    /// <summary>Gyro then accelerometer, three little-endian int16 each, already in DS4 units.</summary>
    private static MotionSample Motion(ReadOnlySpan<byte> d) => new(
        BinaryPrimitives.ReadInt16LittleEndian(d), BinaryPrimitives.ReadInt16LittleEndian(d[2..]),
        BinaryPrimitives.ReadInt16LittleEndian(d[4..]), BinaryPrimitives.ReadInt16LittleEndian(d[6..]),
        BinaryPrimitives.ReadInt16LittleEndian(d[8..]), BinaryPrimitives.ReadInt16LittleEndian(d[10..]));

    /// <summary>
    /// Rumble-only output report. <paramref name="outputLength"/> is the interface's output report size;
    /// Bluetooth reports carry a CRC-32 over a 0xA2 header byte plus the report.
    /// </summary>
    public static byte[] BuildRumble(bool dualSense, bool bluetooth, byte large, byte small, int outputLength)
    {
        byte[] report;
        if (!dualSense && !bluetooth)
        {
            report = new byte[Math.Max(32, outputLength)];
            report[0] = 0x05;
            report[1] = 0x01;  // Rumble valid.
            report[4] = small; // Right (weak) motor.
            report[5] = large; // Left (strong) motor.
            return report;
        }
        if (dualSense && !bluetooth)
        {
            report = new byte[Math.Max(48, outputLength)];
            report[0] = 0x02;
            report[1] = 0x03;  // Compatible vibration + haptics select.
            report[3] = small;
            report[4] = large;
            return report;
        }
        report = new byte[78];
        if (!dualSense)
        {
            report[0] = 0x11;
            report[1] = 0xC4;  // HID + CRC, poll interval.
            report[3] = 0x01;  // Rumble valid.
            report[6] = small;
            report[7] = large;
        }
        else
        {
            report[0] = 0x31;
            report[1] = 0x02;  // Tag.
            report[2] = 0x03;  // Compatible vibration + haptics select.
            report[4] = small;
            report[5] = large;
        }
        uint crc = Crc32.Compute(Crc32.Compute(0, [0xA2]), report.AsSpan(0, 74));
        BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(74), crc);
        return report;
    }
}

/// <summary>Standard CRC-32 (IEEE 802.3), as used by DS4 and DualSense Bluetooth reports.</summary>
public static class Crc32
{
    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(i =>
    {
        uint c = (uint)i;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>Continues a CRC: pass the previous result (0 to start).</summary>
    public static uint Compute(uint previous, ReadOnlySpan<byte> data)
    {
        uint crc = ~previous;
        foreach (byte b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
