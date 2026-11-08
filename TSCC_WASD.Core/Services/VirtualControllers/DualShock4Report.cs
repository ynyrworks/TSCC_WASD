using System.Buffers.Binary;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.Core.Services.VirtualControllers;

/// <summary>
/// Builds the 63-byte ViGEm DS4_REPORT_EX buffer (DualShock 4 USB input report 0x01 without its ID),
/// which unlike the simple target API can carry the PS button, touchpad click and motion data.
/// </summary>
public static class DualShock4Report
{
    public const int Length = 63;
    private const int ButtonsOffset = 4, SpecialOffset = 6, TriggerOffset = 7, TimestampOffset = 9;
    private const int BatteryOffset = 11, GyroOffset = 12, AccelOffset = 18, BatterySpecialOffset = 29;
    private const int TouchPacketsOffset = 32, CurrentTouchOffset = 33, PreviousTouchOffset = 42, TouchSize = 9;
    private const int DigitalTriggerThreshold = 10;

    /// <param name="counter">Increments per report; DS4 stores it in the top six bits of the special byte.</param>
    /// <param name="timestamp">DS4 sensor clock in 5.33 µs units.</param>
    public static byte[] Build(State state, double deadZone, MotionSample? motion, byte counter, ushort timestamp)
        => Build(state, deadZone, motion, counter, timestamp, BatteryStatus.Unknown);

    /// <param name="battery">Physical controller battery; Unknown or Unsupported keeps the old "cable, full" bytes.</param>
    public static byte[] Build(State state, double deadZone, MotionSample? motion, byte counter, ushort timestamp, BatteryStatus battery)
    {
        var report = new byte[Length];
        var gp = state.Gamepad;
        var b = gp.Buttons;

        var left = AxisConverter.ApplyRadial(gp.LeftThumbX, gp.LeftThumbY, deadZone);
        var right = AxisConverter.ApplyRadial(gp.RightThumbX, gp.RightThumbY, deadZone);
        report[0] = AxisConverter.ToByte(left.X);
        report[1] = AxisConverter.ToByte(left.Y, invert: true);
        report[2] = AxisConverter.ToByte(right.X);
        report[3] = AxisConverter.ToByte(right.Y, invert: true);

        ushort buttons = DPad(b);
        if (b.HasFlag(GamepadButtonFlags.X)) buttons |= 0x0010; // Square
        if (b.HasFlag(GamepadButtonFlags.A)) buttons |= 0x0020; // Cross
        if (b.HasFlag(GamepadButtonFlags.B)) buttons |= 0x0040; // Circle
        if (b.HasFlag(GamepadButtonFlags.Y)) buttons |= 0x0080; // Triangle
        if (b.HasFlag(GamepadButtonFlags.LeftShoulder)) buttons |= 0x0100;
        if (b.HasFlag(GamepadButtonFlags.RightShoulder)) buttons |= 0x0200;
        if (gp.LeftTrigger > DigitalTriggerThreshold) buttons |= 0x0400;
        if (gp.RightTrigger > DigitalTriggerThreshold) buttons |= 0x0800;
        if (b.HasFlag(GamepadButtonFlags.Back)) buttons |= 0x1000;  // Share
        if (b.HasFlag(GamepadButtonFlags.Start)) buttons |= 0x2000; // Options
        if (b.HasFlag(GamepadButtonFlags.LeftThumb)) buttons |= 0x4000;
        if (b.HasFlag(GamepadButtonFlags.RightThumb)) buttons |= 0x8000;
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(ButtonsOffset), buttons);

        byte special = (byte)(counter << 2);
        if (b.HasFlag(GamepadButtonFlags.Guide)) special |= 0x01;    // PS
        if (b.HasFlag(GamepadButtonFlags.Touchpad)) special |= 0x02; // Touchpad click
        report[SpecialOffset] = special;

        report[TriggerOffset] = gp.LeftTrigger;
        report[TriggerOffset + 1] = gp.RightTrigger;
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(TimestampOffset), timestamp);
        report[BatteryOffset] = 0xFF; // Not read by games or BatteryParser; kept as before.
        report[BatterySpecialOffset] = StatusByte(battery);

        if (motion is { } m)
        {
            var span = report.AsSpan();
            BinaryPrimitives.WriteInt16LittleEndian(span[GyroOffset..], m.GyroX);
            BinaryPrimitives.WriteInt16LittleEndian(span[(GyroOffset + 2)..], m.GyroY);
            BinaryPrimitives.WriteInt16LittleEndian(span[(GyroOffset + 4)..], m.GyroZ);
            BinaryPrimitives.WriteInt16LittleEndian(span[AccelOffset..], m.AccelX);
            BinaryPrimitives.WriteInt16LittleEndian(span[(AccelOffset + 2)..], m.AccelY);
            BinaryPrimitives.WriteInt16LittleEndian(span[(AccelOffset + 4)..], m.AccelZ);
        }

        // No finger on the touchpad: bit 7 of each contact's tracking byte means "not touching".
        // Left at zero, games would see two phantom touches.
        report[TouchPacketsOffset] = 1;
        for (int touch = 0; touch < 3; touch++)
        {
            int start = touch == 0 ? CurrentTouchOffset : PreviousTouchOffset + (touch - 1) * TouchSize;
            report[start + 1] = 0x80;
            report[start + 5] = 0x80;
        }
        return report;
    }

    /// <summary>
    /// DS4 status byte: low nibble 0-10 level, bit 4 cable/charging. Empty 0, Critical 1, Low 3, Medium 6, Full 10.
    /// Charging sets bit 4 and keeps the level (nibble 11 would read back as "not charging").
    /// Unknown or Unsupported gives the old 0x1B (cable, full).
    /// </summary>
    public static byte StatusByte(BatteryStatus battery)
    {
        if (!battery.IsKnown) return 0x1B;
        int level = battery.Level switch
        {
            BatteryLevel.Empty => 0,
            BatteryLevel.Critical => 1,
            BatteryLevel.Low => 3,
            BatteryLevel.Medium => 6,
            _ => 10
        };
        return (byte)(level | (battery.Charging ? 0x10 : 0));
    }

    /// <summary>
    /// Reads rumble from a DS4 USB output report (ID 0x05) as returned by ViGEm.
    /// Byte 1 bit 0 marks the motor values as valid; byte 4 is the small motor, byte 5 the large one.
    /// </summary>
    public static bool TryParseRumble(ReadOnlySpan<byte> output, out byte large, out byte small)
    {
        large = small = 0;
        if (output.Length < 6 || output[0] != 0x05 || (output[1] & 0x01) == 0) return false;
        small = output[4];
        large = output[5];
        return true;
    }

    private static ushort DPad(GamepadButtonFlags b)
    {
        bool up = b.HasFlag(GamepadButtonFlags.DPadUp), down = b.HasFlag(GamepadButtonFlags.DPadDown);
        bool left = b.HasFlag(GamepadButtonFlags.DPadLeft), right = b.HasFlag(GamepadButtonFlags.DPadRight);
        return (up, down, left, right) switch
        {
            (true, false, false, false) => 0,
            (true, false, false, true) => 1,
            (false, false, false, true) => 2,
            (false, true, false, true) => 3,
            (false, true, false, false) => 4,
            (false, true, true, false) => 5,
            (false, false, true, false) => 6,
            (true, false, true, false) => 7,
            _ => 8
        };
    }
}
