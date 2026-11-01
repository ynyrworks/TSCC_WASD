using System.Buffers.Binary;
using Xunit;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.VirtualControllers;

namespace TSCC_WASD.Tests;

public class DualShock4ReportTests
{
    private static byte[] Build(GamepadButtonFlags buttons = GamepadButtonFlags.None, MotionSample? motion = null,
        byte lt = 0, byte rt = 0)
        => DualShock4Report.Build(new State { Gamepad = new Gamepad { Buttons = buttons, LeftTrigger = lt, RightTrigger = rt } },
            0.08, motion, counter: 5, timestamp: 0x1234);

    [Fact]
    public void SticksAreRadialAndIndependent()
    {
        var gp = new Gamepad { LeftThumbX = 3000, LeftThumbY = 3000, RightThumbX = short.MaxValue, RightThumbY = short.MinValue };
        var r = DualShock4Report.Build(new State { Gamepad = gp }, 0.15, null, 0, 0);
        Assert.Equal([128, 128], r[0..2]); // Left combined distance is inside the dead zone.
        Assert.True(r[2] > 128 && r[3] > 128); // Right pushed right and down (Y inverted).
    }

    [Fact]
    public void NeutralReportHasCenteredSticksNoDPadAndNoTouches()
    {
        var r = Build();
        Assert.Equal(DualShock4Report.Length, r.Length);
        Assert.Equal([128, 128, 128, 128], r[0..4]);
        Assert.Equal(0x0008, BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(4)));
        Assert.Equal(5 << 2, r[6]);
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(9)));
        // Bit 7 set on every contact = finger lifted; otherwise games see phantom touches.
        foreach (int offset in new[] { 34, 38, 43, 47, 52, 56 }) Assert.Equal(0x80, r[offset]);
    }

    [Theory]
    [InlineData(GamepadButtonFlags.X, 0x0018)]
    [InlineData(GamepadButtonFlags.A, 0x0028)]
    [InlineData(GamepadButtonFlags.B, 0x0048)]
    [InlineData(GamepadButtonFlags.Y, 0x0088)]
    [InlineData(GamepadButtonFlags.LeftShoulder, 0x0108)]
    [InlineData(GamepadButtonFlags.RightShoulder, 0x0208)]
    [InlineData(GamepadButtonFlags.Back, 0x1008)]
    [InlineData(GamepadButtonFlags.Start, 0x2008)]
    [InlineData(GamepadButtonFlags.LeftThumb, 0x4008)]
    [InlineData(GamepadButtonFlags.RightThumb, 0x8008)]
    [InlineData(GamepadButtonFlags.DPadUp, 0x0000)]
    [InlineData(GamepadButtonFlags.DPadUp | GamepadButtonFlags.DPadRight, 0x0001)]
    [InlineData(GamepadButtonFlags.DPadDown | GamepadButtonFlags.DPadLeft, 0x0005)]
    public void MapsButtonsAndHat(GamepadButtonFlags buttons, int expected)
        => Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(Build(buttons).AsSpan(4)));

    [Fact]
    public void MapsGuideToPsAndTouchpadClickAndTriggers()
    {
        var r = Build(GamepadButtonFlags.Guide | GamepadButtonFlags.Touchpad, lt: 200, rt: 5);
        Assert.Equal(0x03, r[6] & 0x03);
        Assert.Equal(200, r[7]);
        Assert.Equal(5, r[8]);
        ushort buttons = BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(4));
        Assert.True((buttons & 0x0400) != 0);  // L2 digital
        Assert.False((buttons & 0x0800) != 0); // R2 below threshold
    }

    [Fact]
    public void WritesMotionGyroBeforeAccel()
    {
        var r = Build(motion: new MotionSample(1, -2, 3, 8192, -4, 5));
        short At(int o) => BinaryPrimitives.ReadInt16LittleEndian(r.AsSpan(o));
        Assert.Equal([1, -2, 3, 8192, -4, 5], new[] { At(12), At(14), At(16), At(18), At(20), At(22) });
    }

    [Fact]
    public void ParsesRumbleOnlyWhenFlagged()
    {
        // Layout confirmed against ViGEm's AwaitRawOutputReport on a live virtual DS4.
        Assert.True(DualShock4Report.TryParseRumble([0x05, 0x07, 0, 0, 0x22, 0x11, 0xAA, 0xBB, 0xCC], out var large, out var small));
        Assert.Equal(0x11, large);
        Assert.Equal(0x22, small);
        Assert.False(DualShock4Report.TryParseRumble([0x05, 0x00, 0, 0, 0, 0, 0x40], out _, out _));
        Assert.False(DualShock4Report.TryParseRumble([0x11, 0x07, 0, 0, 1, 1], out _, out _));
    }
}
