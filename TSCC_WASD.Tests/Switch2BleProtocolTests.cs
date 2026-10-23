using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

/// <summary>Sample bytes are from a real Pro Controller 2 over Bluetooth LE (2026-10-23).</summary>
public class Switch2BleProtocolTests
{
    private const string Neutral = "620D0000000000000000F747824C8880000000000000000000000000000000EC0D000000000000000001000000000000000000000000000000000000000000";
    private static readonly Switch2StickCalibration Left = Switch2StickCalibration.Parse(Convert.FromHexString("C927821E465D2BB660FFFF"));
    private static readonly Switch2StickCalibration Right = Switch2StickCalibration.Parse(Convert.FromHexString("38287F0CB664277663FFFF"));

    [Fact]
    public void ReadsSyncModeAndPairedAdvertisements()
    {
        Assert.True(Switch2BleProtocol.TryParseAdvertisement(Convert.FromHexString("0100037E0569200001000000000000000F00000000000000"),
            out ushort vid, out ushort pid, out ulong host));
        Assert.Equal((0x057E, 0x2069, 0UL), (vid, pid, host));

        Assert.True(Switch2BleProtocol.TryParseAdvertisement(Convert.FromHexString("0100037E056920000100E50135BFEFE00F00000000000000"),
            out _, out _, out host));
        Assert.Equal(0xE0EFBF3501E5UL, host);
        Assert.False(Switch2BleProtocol.TryParseAdvertisement(new byte[15], out _, out _, out _));
    }

    [Fact]
    public void FramesCommandsAndMemoryReads()
    {
        Assert.Equal("0C9101020004000027000000", Convert.ToHexString(Switch2BleProtocol.FeatureInit(0x27)));
        Assert.Equal("02910104000800000B7E000042C01F00", Convert.ToHexString(Switch2BleProtocol.BuildReadMemory(0x0B, 0x1FC042)));

        var reply = Convert.FromHexString("0201000000000000" + "0B7E000042C01F00" + "C927821E465D2BB660FFFF");
        Assert.True(Switch2BleProtocol.TryParseReply(reply, 0x02, out var payload));
        Assert.True(Switch2BleProtocol.TryParseMemory(payload, 0x0B, 0x1FC042, out var data));
        Assert.Equal("C927821E465D2BB660FFFF", Convert.ToHexString(data));
        Assert.False(Switch2BleProtocol.TryParseMemory(payload, 0x0B, 0x1FC062, out _));
        Assert.False(Switch2BleProtocol.TryParseReply(reply, 0x0C, out _));
    }

    [Fact]
    public void FactoryCalibrationMatchesTheController()
    {
        Assert.Equal(new Switch2AxisCalibration(1993, 1566, 1579), Left.X);
        Assert.Equal(new Switch2AxisCalibration(2082, 1492, 1547), Left.Y);
        Assert.True(Right.IsValid);
        Assert.True(Switch2BleProtocol.IsUnset(Convert.FromHexString("FFFFFF0000")));
        Assert.False(Switch2BleProtocol.IsUnset(Convert.FromHexString("C92782")));
    }

    [Fact]
    public void NeutralReportParsesThroughTheUsbLayout()
    {
        var state = Parse(Convert.FromHexString(Neutral));
        Assert.Equal(GamepadButtonFlags.None, state.Gamepad.Buttons);
        Assert.InRange(state.Gamepad.LeftThumbX, -1500, 1500);
        Assert.InRange(state.Gamepad.RightThumbY, -1500, 1500);
        Assert.Equal(0, state.Gamepad.LeftTrigger);
    }

    [Theory]
    // Bytes 4–6 of the BLE report, as pressed on the controller.
    [InlineData(4, 0x04, GamepadButtonFlags.A)]            // Nintendo B (south)
    [InlineData(4, 0x08, GamepadButtonFlags.B)]            // Nintendo A (east)
    [InlineData(4, 0x40, GamepadButtonFlags.RightShoulder)]
    [InlineData(5, 0x01, GamepadButtonFlags.Back)]         // Minus
    [InlineData(5, 0x02, GamepadButtonFlags.Start)]        // Plus
    [InlineData(5, 0x08, GamepadButtonFlags.LeftThumb)]
    [InlineData(5, 0x10, GamepadButtonFlags.Guide)]        // Home
    [InlineData(5, 0x20, GamepadButtonFlags.Touchpad)]     // Capture
    [InlineData(6, 0x02, GamepadButtonFlags.DPadUp)]
    [InlineData(6, 0x40, GamepadButtonFlags.LeftShoulder)]
    public void ButtonsSitWhereTheUsbReportHasThem(int index, byte bit, GamepadButtonFlags expected)
    {
        var report = Convert.FromHexString(Neutral);
        report[index] = bit;
        Assert.Equal(expected, Parse(report).Gamepad.Buttons);
    }

    [Fact]
    public void TriggersAreDigital()
    {
        var report = Convert.FromHexString(Neutral);
        report[4] = 0x80; // ZR
        report[6] = 0x80; // ZL
        var state = Parse(report);
        Assert.Equal((255, 255), (state.Gamepad.LeftTrigger, state.Gamepad.RightTrigger));
    }

    [Fact]
    public void RejectsShortReports()
    {
        Assert.False(Switch2BleProtocol.TryConvertReport(new byte[62], new byte[64]));
    }

    [Fact]
    public void RumbleUsesTheUsbMotorBlocksWithoutAReportId()
    {
        var packet = Switch2BleProtocol.BuildRumble(160, 160, 3);
        var usb = Switch2Rumble.BuildReport(160, 160, 3);
        Assert.Equal(0x21, packet.Length);
        Assert.Equal(0x00, packet[0]);
        Assert.Equal(usb.AsSpan(1, 0x20).ToArray(), packet.AsSpan(1).ToArray());
        Assert.Equal(0x53, packet[1]);
    }

    private static State Parse(byte[] notification)
    {
        var report = new byte[64];
        Assert.True(Switch2BleProtocol.TryConvertReport(notification, report));
        Assert.True(Switch2ReportParser.TryParse(report, Left, Right, out var state));
        return state;
    }
}
