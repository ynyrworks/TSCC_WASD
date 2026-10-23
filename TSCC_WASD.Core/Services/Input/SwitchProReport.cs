namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Original Nintendo Switch Pro Controller (057E:2009) input reports and setup commands.
/// Layouts follow SDL's SDL_hidapi_switch.c (zlib) and dekuNukem's Nintendo_Switch_Reverse_Engineering
/// notes. Buttons map by position like the NS2 Pro: B (bottom)→A, A (right)→B, Y (left)→X, X (top)→Y,
/// Minus→Back, Plus→Start, Home→Guide, Capture→Touchpad.
/// </summary>
public static class SwitchProReport
{
    /// <summary>
    /// Full report 0x30 (12-bit sticks; sent once Steam or TSCC_WASD set it up) or the simple Bluetooth
    /// report 0x3F the controller sends by default. Full reports use <paramref name="left"/>/<paramref name="right"/>
    /// calibration; simple reports carry 16-bit sticks that need none.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> report, Switch2StickCalibration left, Switch2StickCalibration right,
        out State state, out RawSticks raw)
    {
        state = default;
        raw = default;
        if (report.Length >= 12 && report[0] == 0x30)
        {
            raw = new RawSticks(
                Switch2StickCalibration.UnpackX(report[6..]), Switch2StickCalibration.UnpackY(report[6..]),
                Switch2StickCalibration.UnpackX(report[9..]), Switch2StickCalibration.UnpackY(report[9..]));
            var buttons = GamepadButtonFlags.None;
            byte r = report[3], shared = report[4], l = report[5];
            if ((r & 0x01) != 0) buttons |= GamepadButtonFlags.X;             // Y (left)
            if ((r & 0x02) != 0) buttons |= GamepadButtonFlags.Y;             // X (top)
            if ((r & 0x04) != 0) buttons |= GamepadButtonFlags.A;             // B (bottom)
            if ((r & 0x08) != 0) buttons |= GamepadButtonFlags.B;             // A (right)
            if ((r & 0x40) != 0) buttons |= GamepadButtonFlags.RightShoulder; // R
            if ((shared & 0x01) != 0) buttons |= GamepadButtonFlags.Back;
            if ((shared & 0x02) != 0) buttons |= GamepadButtonFlags.Start;
            if ((shared & 0x04) != 0) buttons |= GamepadButtonFlags.RightThumb;
            if ((shared & 0x08) != 0) buttons |= GamepadButtonFlags.LeftThumb;
            if ((shared & 0x10) != 0) buttons |= GamepadButtonFlags.Guide;
            if ((shared & 0x20) != 0) buttons |= GamepadButtonFlags.Touchpad;
            if ((l & 0x01) != 0) buttons |= GamepadButtonFlags.DPadDown;
            if ((l & 0x02) != 0) buttons |= GamepadButtonFlags.DPadUp;
            if ((l & 0x04) != 0) buttons |= GamepadButtonFlags.DPadRight;
            if ((l & 0x08) != 0) buttons |= GamepadButtonFlags.DPadLeft;
            if ((l & 0x40) != 0) buttons |= GamepadButtonFlags.LeftShoulder;  // L
            state = new State
            {
                Gamepad = new Gamepad
                {
                    Buttons = buttons,
                    LeftTrigger = (byte)((l & 0x80) != 0 ? 255 : 0),  // ZL
                    RightTrigger = (byte)((r & 0x80) != 0 ? 255 : 0), // ZR
                    LeftThumbX = left.X.Normalize(raw.LeftX), LeftThumbY = left.Y.Normalize(raw.LeftY),
                    RightThumbX = right.X.Normalize(raw.RightX), RightThumbY = right.Y.Normalize(raw.RightY)
                }
            };
            return true;
        }
        if (report.Length >= 12 && report[0] == 0x3F)
        {
            var buttons = GamepadButtonFlags.None;
            byte b1 = report[1], b2 = report[2];
            if ((b1 & 0x01) != 0) buttons |= GamepadButtonFlags.A; // B (bottom)
            if ((b1 & 0x02) != 0) buttons |= GamepadButtonFlags.B; // A (right)
            if ((b1 & 0x04) != 0) buttons |= GamepadButtonFlags.X; // Y (left)
            if ((b1 & 0x08) != 0) buttons |= GamepadButtonFlags.Y; // X (top)
            if ((b1 & 0x10) != 0) buttons |= GamepadButtonFlags.LeftShoulder;
            if ((b1 & 0x20) != 0) buttons |= GamepadButtonFlags.RightShoulder;
            if ((b2 & 0x01) != 0) buttons |= GamepadButtonFlags.Back;
            if ((b2 & 0x02) != 0) buttons |= GamepadButtonFlags.Start;
            if ((b2 & 0x04) != 0) buttons |= GamepadButtonFlags.LeftThumb;
            if ((b2 & 0x08) != 0) buttons |= GamepadButtonFlags.RightThumb;
            if ((b2 & 0x10) != 0) buttons |= GamepadButtonFlags.Guide;
            if ((b2 & 0x20) != 0) buttons |= GamepadButtonFlags.Touchpad;
            buttons |= report[3] switch
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
            state = new State
            {
                Gamepad = new Gamepad
                {
                    Buttons = buttons,
                    LeftTrigger = (byte)((b1 & 0x40) != 0 ? 255 : 0),
                    RightTrigger = (byte)((b1 & 0x80) != 0 ? 255 : 0),
                    LeftThumbX = Simple(report, 4), LeftThumbY = Simple(report, 6, invert: true),
                    RightThumbX = Simple(report, 8), RightThumbY = Simple(report, 10, invert: true)
                }
            };
            return true;
        }
        return false;
    }

    /// <summary>Simple-report axis: unsigned 16-bit, centre 0x8000, Y grows downwards.</summary>
    private static short Simple(ReadOnlySpan<byte> report, int offset, bool invert = false)
    {
        int value = report[offset] | (report[offset + 1] << 8);
        int centred = invert ? Math.Min(32767, 32768 - value) : value - 32768;
        return (short)Math.Clamp(centred, short.MinValue, short.MaxValue);
    }

    /// <summary>USB-only commands (report 0x80) that make the controller talk HID over USB.</summary>
    public static readonly byte[][] UsbHandshake = [[0x80, 0x02], [0x80, 0x04]];

    /// <summary>Subcommand 0x03 0x30: switch to full reports. Rumble data is left neutral.</summary>
    public static byte[] SetFullReportMode(byte counter) => Subcommand(counter, 0x03, 0x30);

    /// <summary>Output report 0x01: counter, neutral rumble for both motors, then the subcommand.</summary>
    public static byte[] Subcommand(byte counter, byte id, byte argument) => Subcommand(counter, id, [argument]);

    public static byte[] Subcommand(byte counter, byte id, ReadOnlySpan<byte> arguments)
    {
        var report = new byte[49];
        report[0] = 0x01;
        report[1] = (byte)(counter & 0x0F);
        byte[] neutralRumble = [0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40];
        neutralRumble.CopyTo(report, 2);
        report[10] = id;
        arguments.CopyTo(report.AsSpan(11));
        return report;
    }

    /// <summary>Factory stick calibration (left 9 bytes, then right 9 bytes) in SPI flash.</summary>
    public const uint FactoryStickCalibration = 0x603D;
    /// <summary>User stick calibration: B2 A1 + left 9 bytes, then B2 A1 + right 9 bytes.</summary>
    public const uint UserStickCalibration = 0x8010;

    /// <summary>Subcommand 0x10: read <paramref name="length"/> bytes of SPI flash (read-only).</summary>
    public static byte[] SpiRead(byte counter, uint address, byte length)
    {
        Span<byte> arguments = stackalloc byte[5];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(arguments, address);
        arguments[4] = length;
        return Subcommand(counter, 0x10, arguments);
    }

    /// <summary>
    /// Reply report 0x21 to an SPI read: ACK with data at 13, subcommand 0x10 at 14, then the address,
    /// the length and the data.
    /// </summary>
    public static bool TryParseSpiReply(ReadOnlySpan<byte> report, out uint address, out ReadOnlySpan<byte> data)
    {
        address = 0;
        data = default;
        if (report.Length < 20 || report[0] != 0x21 || (report[13] & 0x80) == 0 || report[14] != 0x10) return false;
        address = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(report[15..]);
        int length = report[19];
        if (report.Length < 20 + length) return false;
        data = report.Slice(20, length);
        return true;
    }

    /// <summary>
    /// Decodes one stick's 9 calibration bytes. The left stick stores max-above-center, center,
    /// min-below-center; the right stick stores center, min-below-center, max-above-center.
    /// </summary>
    public static Switch2StickCalibration DecodeStick(ReadOnlySpan<byte> data, bool left)
    {
        if (data.Length < 9) return default;
        ReadOnlySpan<byte> above = left ? data[..3] : data[6..9];
        ReadOnlySpan<byte> center = left ? data[3..6] : data[..3];
        ReadOnlySpan<byte> below = left ? data[6..9] : data[3..6];
        return new(
            new(Switch2StickCalibration.UnpackX(center), Switch2StickCalibration.UnpackX(above), Switch2StickCalibration.UnpackX(below)),
            new(Switch2StickCalibration.UnpackY(center), Switch2StickCalibration.UnpackY(above), Switch2StickCalibration.UnpackY(below)));
    }

    /// <summary>
    /// Picks the user calibration where it is set (B2 A1 magic) and valid, otherwise the factory one.
    /// Returns false when neither stick has usable data (for example a third-party controller).
    /// </summary>
    public static bool TryDecodeCalibration(ReadOnlySpan<byte> factory, ReadOnlySpan<byte> user,
        out Switch2StickCalibration left, out Switch2StickCalibration right)
    {
        left = right = default;
        if (factory.Length < 18) return false;
        left = DecodeStick(factory[..9], left: true);
        right = DecodeStick(factory[9..18], left: false);
        if (user.Length >= 22)
        {
            if (user[0] == 0xB2 && user[1] == 0xA1 && DecodeStick(user[2..11], left: true) is { IsValid: true } userLeft) left = userLeft;
            if (user[11] == 0xB2 && user[12] == 0xA1 && DecodeStick(user[13..22], left: false) is { IsValid: true } userRight) right = userRight;
        }
        return left.IsValid && right.IsValid;
    }
}
