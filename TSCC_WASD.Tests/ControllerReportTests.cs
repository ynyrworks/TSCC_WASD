using System.Buffers.Binary;
using System.Text;
using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

/// <summary>DualShock 4, DualSense and Switch Pro reports built byte by byte from the published layouts.</summary>
public class ControllerReportTests
{
    private const GamepadButtonFlags Expected =
        GamepadButtonFlags.DPadRight | GamepadButtonFlags.A | GamepadButtonFlags.LeftShoulder |
        GamepadButtonFlags.Back | GamepadButtonFlags.Guide;

    [Theory]
    [InlineData(@"\\?\hid#vid_054c&pid_09cc&mi_03#7&1a2b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x054C, 0x09CC, false)]
    [InlineData(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&05c4#9&2c3d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x054C, 0x05C4, true)]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805f9b34fb}&Dev&VID_045e&PID_0b13&REV_0509\d&23b3&0&0000", 0x045E, 0x0B13, false)]
    [InlineData(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&2009#9&1&0&0000", 0x057E, 0x2009, true)]
    public void ReadsVendorAndProductFromUsbAndBluetoothPaths(string path, int vid, int pid, bool bluetooth)
    {
        Assert.True(HidDevice.TryParseIds(path, out ushort v, out ushort p, out bool bt));
        Assert.Equal((vid, pid, bluetooth), (v, p, bt));
        Assert.False(HidDevice.TryParseIds(@"\\?\root#system#0000", out _, out _, out _));
    }

    [Fact]
    public void InterfacePathBecomesTheDeviceInstanceId()
    {
        Assert.Equal(@"hid\vid_054c&pid_05c4\7&1a2b&0&0000",
            HidDevice.InstanceId(@"\\?\hid#vid_054c&pid_05c4#7&1a2b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
        Assert.Null(HidDevice.InstanceId("not a device path"));
    }

    /// <summary>DS4 layout from the left stick X byte: hat right + Cross, L1 + Share, PS, L2 200.</summary>
    private static byte[] Ds4Body()
    {
        var body = new byte[23];
        body[0] = 0;    // LX full left
        body[1] = 255;  // LY full down
        body[2] = 128;  // RX centre
        body[3] = 128;  // RY centre
        body[4] = 0x22; // Hat 2 (right) + Cross
        body[5] = 0x11; // L1 + Share
        body[6] = 0x01 | (5 << 2); // PS + counter
        body[7] = 200;  // L2
        body[8] = 0;    // R2
        BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(12), 100);   // Gyro X
        BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(18), 8192);  // Accel X (1 g)
        return body;
    }

    private static void AssertDs4Body(State state)
    {
        Assert.Equal(Expected, state.Gamepad.Buttons);
        Assert.Equal(short.MinValue, state.Gamepad.LeftThumbX);
        Assert.Equal(-127 * 256, state.Gamepad.LeftThumbY); // Down is negative in XInput.
        Assert.Equal(0, state.Gamepad.RightThumbX);
        Assert.Equal(0, state.Gamepad.RightThumbY);
        Assert.Equal(200, state.Gamepad.LeftTrigger);
    }

    [Fact]
    public void DualShock4UsbAndBluetoothReports()
    {
        var usb = new byte[64];
        usb[0] = 0x01;
        Ds4Body().CopyTo(usb, 1);
        Assert.True(PlayStationReport.TryParse(usb, dualSense: false, out var state, out var motion));
        AssertDs4Body(state);
        Assert.Equal((short)100, motion!.Value.GyroX);
        Assert.Equal((short)8192, motion.Value.AccelX);

        var bluetooth = new byte[78];
        bluetooth[0] = 0x11;
        Ds4Body().CopyTo(bluetooth, 3);
        Assert.True(PlayStationReport.TryParse(bluetooth, dualSense: false, out state, out motion));
        AssertDs4Body(state);
        Assert.Equal((short)100, motion!.Value.GyroX);

        // Basic Bluetooth report before full reports are enabled: no motion.
        var basic = new byte[10];
        basic[0] = 0x01;
        Ds4Body().AsSpan(0, 9).CopyTo(basic.AsSpan(1));
        Assert.True(PlayStationReport.TryParse(basic, dualSense: false, out state, out motion));
        AssertDs4Body(state);
        Assert.Null(motion);
    }

    [Fact]
    public void DualSenseUsbAndBluetoothReports()
    {
        var body = new byte[28];
        body[0] = 0; body[1] = 255; body[2] = 128; body[3] = 128; // Sticks
        body[4] = 200; body[5] = 0;                               // L2, R2
        body[7] = 0x22; body[8] = 0x11; body[9] = 0x01;           // Hat right + Cross, L1 + Create, PS
        BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(15), -50); // Gyro X

        var usb = new byte[64];
        usb[0] = 0x01;
        body.CopyTo(usb, 1);
        Assert.True(PlayStationReport.TryParse(usb, dualSense: true, out var state, out var motion));
        AssertDs4Body(state);
        Assert.Equal((short)-50, motion!.Value.GyroX);

        var bluetooth = new byte[78];
        bluetooth[0] = 0x31;
        body.CopyTo(bluetooth, 2);
        Assert.True(PlayStationReport.TryParse(bluetooth, dualSense: true, out state, out motion));
        AssertDs4Body(state);
        Assert.Equal((short)-50, motion!.Value.GyroX);
    }

    [Fact]
    public void ReleasedHatAndUnknownReportsAreHandled()
    {
        var usb = new byte[64];
        usb[0] = 0x01;
        usb[5] = 0x08; // Hat released
        usb[1] = usb[2] = usb[3] = usb[4] = 128;
        Assert.True(PlayStationReport.TryParse(usb, dualSense: false, out var state, out _));
        Assert.Equal(GamepadButtonFlags.None, state.Gamepad.Buttons);
        Assert.False(PlayStationReport.TryParse(new byte[64] { 0x05, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false, out _, out _));
        Assert.Equal(0, PlayStationReport.Axis(128));
        Assert.Equal(0, PlayStationReport.Axis(128, invert: true));
        Assert.Equal(short.MaxValue, PlayStationReport.Axis(255));
        Assert.Equal(short.MaxValue, PlayStationReport.Axis(0, invert: true));
    }

    [Fact]
    public void RumbleReportsUseTheRightIdsMotorsAndBluetoothCrc()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(0, Encoding.ASCII.GetBytes("123456789")));

        var ds4Usb = PlayStationReport.BuildRumble(dualSense: false, bluetooth: false, large: 200, small: 50, outputLength: 32);
        Assert.Equal((byte)0x05, ds4Usb[0]);
        Assert.Equal((50, 200), (ds4Usb[4], ds4Usb[5]));

        var dsUsb = PlayStationReport.BuildRumble(dualSense: true, bluetooth: false, large: 200, small: 50, outputLength: 48);
        Assert.Equal((byte)0x02, dsUsb[0]);
        Assert.Equal((50, 200), (dsUsb[3], dsUsb[4]));

        foreach (bool dualSense in new[] { false, true })
        {
            var bt = PlayStationReport.BuildRumble(dualSense, bluetooth: true, large: 200, small: 50, outputLength: 547);
            Assert.Equal(78, bt.Length);
            Assert.Equal(dualSense ? (byte)0x31 : (byte)0x11, bt[0]);
            uint expected = Crc32.Compute(Crc32.Compute(0, [0xA2]), bt.AsSpan(0, 74));
            Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(bt.AsSpan(74)));
            Assert.Contains((byte)200, bt[..74]);
        }
    }

    [Fact]
    public void SwitchProFullReportMapsByPosition()
    {
        var report = new byte[64];
        report[0] = 0x30;
        report[3] = 0x08 | 0x80;        // A (right) + ZR
        report[4] = 0x10 | 0x01;        // Home + Minus
        report[5] = 0x02 | 0x40;        // Up + L
        WriteSticks(report, 6, 2048, 2048);
        WriteSticks(report, 9, 2048 + 2047, 2048);
        Assert.True(SwitchProReport.TryParse(report, Switch2StickCalibration.Nominal, Switch2StickCalibration.Nominal, out var state, out var raw));
        Assert.Equal(GamepadButtonFlags.B | GamepadButtonFlags.Guide | GamepadButtonFlags.Back |
                     GamepadButtonFlags.DPadUp | GamepadButtonFlags.LeftShoulder, state.Gamepad.Buttons);
        Assert.Equal((byte)255, state.Gamepad.RightTrigger);
        Assert.Equal((byte)0, state.Gamepad.LeftTrigger);
        Assert.Equal(0, state.Gamepad.LeftThumbX);
        Assert.Equal(short.MaxValue, state.Gamepad.RightThumbX);
        Assert.Equal(2048 + 2047, raw.RightX);
    }

    [Fact]
    public void SwitchProSimpleBluetoothReport()
    {
        var report = new byte[12];
        report[0] = 0x3F;
        report[1] = 0x01 | 0x40;  // B (bottom) + ZL
        report[2] = 0x20;         // Capture
        report[3] = 4;            // Hat down
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(4), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(6), 0xFFFF); // Full down
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(8), 0x0000); // Full left
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(10), 0x8000);
        Assert.True(SwitchProReport.TryParse(report, default, default, out var state, out _));
        Assert.Equal(GamepadButtonFlags.A | GamepadButtonFlags.Touchpad | GamepadButtonFlags.DPadDown, state.Gamepad.Buttons);
        Assert.Equal((byte)255, state.Gamepad.LeftTrigger);
        Assert.Equal(0, state.Gamepad.LeftThumbX);
        Assert.Equal(-32767, state.Gamepad.LeftThumbY);
        Assert.Equal(short.MinValue, state.Gamepad.RightThumbX);
        Assert.Equal(0, state.Gamepad.RightThumbY);
    }

    [Fact]
    public void SwitchProSetupCommands()
    {
        var command = SwitchProReport.SetFullReportMode(17);
        Assert.Equal((byte)0x01, command[0]);
        Assert.Equal((byte)1, command[1]); // Counter wraps at 16.
        Assert.Equal((byte)0x03, command[10]);
        Assert.Equal((byte)0x30, command[11]);
        Assert.All(SwitchProReport.UsbHandshake, c => Assert.Equal((byte)0x80, c[0]));
    }

    [Fact]
    public void SwitchProSpiReadAndReply()
    {
        var command = SwitchProReport.SpiRead(2, SwitchProReport.FactoryStickCalibration, 18);
        Assert.Equal("10" + "3D600000" + "12", Convert.ToHexString(command.AsSpan(10, 6)));

        var reply = new byte[49];
        reply[0] = 0x21;
        reply[13] = 0x90;
        reply[14] = 0x10;
        new byte[] { 0x3D, 0x60, 0, 0, 3, 0xAA, 0xBB, 0xCC }.CopyTo(reply, 15);
        Assert.True(SwitchProReport.TryParseSpiReply(reply, out uint address, out var data));
        Assert.Equal(0x603Du, address);
        Assert.Equal("AABBCC", Convert.ToHexString(data));

        reply[13] = 0x00; // NACK
        Assert.False(SwitchProReport.TryParseSpiReply(reply, out _, out _));
    }

    [Fact]
    public void SwitchProCalibrationOrderDiffersPerStick()
    {
        // Values from a real controller (2026-10-23): left centre 1932/1845, right centre 2054/1917.
        var factory = new byte[18];
        WriteSticks(factory, 0, 1459, 1461);  // left: above centre
        WriteSticks(factory, 3, 1932, 1845);  // left: centre
        WriteSticks(factory, 6, 1497, 1609);  // left: below centre
        WriteSticks(factory, 9, 2054, 1917);  // right: centre
        WriteSticks(factory, 12, 1560, 1448); // right: below centre
        WriteSticks(factory, 15, 1427, 1562); // right: above centre
        var unsetUser = Enumerable.Repeat((byte)0xFF, 22).ToArray();

        Assert.True(SwitchProReport.TryDecodeCalibration(factory, unsetUser, out var left, out var right));
        Assert.Equal(new Switch2AxisCalibration(1932, 1459, 1497), left.X);
        Assert.Equal(new Switch2AxisCalibration(1845, 1461, 1609), left.Y);
        Assert.Equal(new Switch2AxisCalibration(2054, 1427, 1560), right.X);
        Assert.Equal(new Switch2AxisCalibration(1917, 1562, 1448), right.Y);

        // A user calibration (B2 A1 magic) wins for that stick only.
        var user = (byte[])unsetUser.Clone();
        user[0] = 0xB2; user[1] = 0xA1;
        WriteSticks(user, 2, 1400, 1400);
        WriteSticks(user, 5, 2000, 2000);
        WriteSticks(user, 8, 1400, 1400);
        Assert.True(SwitchProReport.TryDecodeCalibration(factory, user, out left, out right));
        Assert.Equal(2000, left.X.Center);
        Assert.Equal(2054, right.X.Center);

        Assert.False(SwitchProReport.TryDecodeCalibration(new byte[18], unsetUser, out _, out _));
    }

    private static void WriteSticks(byte[] report, int offset, int x, int y)
    {
        report[offset] = (byte)(x & 0xFF);
        report[offset + 1] = (byte)(((x >> 8) & 0x0F) | ((y & 0x0F) << 4));
        report[offset + 2] = (byte)(y >> 4);
    }
}
