using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

public class BatteryTests
{
    private static BatteryStatus Known(BatteryLevel level, bool charging) => BatteryStatus.Of(level, charging);

    // Switch Pro: 0x91 and 0x61 are real captures from the maintainer.
    [Theory]
    [InlineData(0x91, BatteryLevel.Full, true)]
    [InlineData(0x61, BatteryLevel.Medium, false)]
    [InlineData(0x00, BatteryLevel.Empty, false)]
    [InlineData(0x20, BatteryLevel.Critical, false)]
    [InlineData(0x41, BatteryLevel.Low, false)]
    [InlineData(0xE0, BatteryLevel.Full, false)] // out-of-range level clamps
    public void SwitchProByteTwo(byte value, BatteryLevel level, bool charging) =>
        Assert.Equal(Known(level, charging), BatteryParser.FromSwitchPro(value));

    // NS2 Pro: real captures. USB report 0x05 and the same fields without the ID over Bluetooth.
    [Fact]
    public void Ns2BluetoothCapture()
    {
        var report = new byte[63];
        report[0x1F] = 0xE1; report[0x20] = 0x0D; report[0x21] = 0x00;
        Assert.True(BatteryParser.TryParseNs2(report, bluetooth: true, out var battery));
        Assert.Equal(Known(BatteryLevel.Low, false), battery); // 3553 mV
    }

    [Fact]
    public void Ns2UsbCapture()
    {
        var report = new byte[64];
        report[0] = 0x05;
        report[0x20] = 0x1B; report[0x21] = 0x0E; report[0x22] = 0x34;
        Assert.True(BatteryParser.TryParseNs2(report, bluetooth: false, out var battery));
        Assert.Equal(Known(BatteryLevel.Low, true), battery); // 3611 mV
    }

    [Fact]
    public void Ns2RejectsWrongReportsAndZeroVoltage()
    {
        var usb = new byte[64];
        Assert.False(BatteryParser.TryParseNs2(usb, false, out _)); // no report ID
        usb[0] = 0x05;
        Assert.False(BatteryParser.TryParseNs2(usb, false, out _)); // 0 mV
        Assert.False(BatteryParser.TryParseNs2(new byte[10], true, out _));
    }

    [Theory]
    [InlineData(3900, BatteryLevel.Full)]
    [InlineData(3899, BatteryLevel.Medium)]
    [InlineData(3700, BatteryLevel.Medium)]
    [InlineData(3699, BatteryLevel.Low)]
    [InlineData(3500, BatteryLevel.Low)]
    [InlineData(3499, BatteryLevel.Critical)]
    [InlineData(3400, BatteryLevel.Critical)]
    [InlineData(3399, BatteryLevel.Empty)]
    public void Ns2VoltageThresholds(int millivolts, BatteryLevel level) =>
        Assert.Equal(level, BatteryParser.FromNs2Millivolts(millivolts));

    [Theory]
    [InlineData(0, BatteryLevel.Empty)]
    [InlineData(1, BatteryLevel.Critical)]
    [InlineData(2, BatteryLevel.Critical)]
    [InlineData(3, BatteryLevel.Low)]
    [InlineData(4, BatteryLevel.Low)]
    [InlineData(5, BatteryLevel.Medium)]
    [InlineData(7, BatteryLevel.Medium)]
    [InlineData(8, BatteryLevel.Full)]
    [InlineData(10, BatteryLevel.Full)]
    public void TenStepMapping(int step, BatteryLevel level) => Assert.Equal(level, BatteryParser.FromTenSteps(step));

    [Fact]
    public void DualShock4StatusByte()
    {
        Assert.Equal(Known(BatteryLevel.Medium, false), BatteryParser.FromDualShock4(0x06));
        Assert.Equal(Known(BatteryLevel.Low, true), BatteryParser.FromDualShock4(0x13)); // cable, step 3
        Assert.Equal(Known(BatteryLevel.Full, false), BatteryParser.FromDualShock4(0x1B)); // cable, 11 = full
        Assert.Equal(Known(BatteryLevel.Empty, false), BatteryParser.FromDualShock4(0x00));
    }

    [Fact]
    public void DualSenseStatusByte()
    {
        Assert.Equal(Known(BatteryLevel.Full, false), BatteryParser.FromDualSense(0x08)); // discharging, step 8
        Assert.Equal(Known(BatteryLevel.Medium, true), BatteryParser.FromDualSense(0x15));
        Assert.Equal(Known(BatteryLevel.Full, false), BatteryParser.FromDualSense(0x2A)); // full
        Assert.Equal(BatteryStatus.Unknown, BatteryParser.FromDualSense(0xA5));           // error status
    }

    [Fact]
    public void PlayStationReportsUseTheTransportOffsets()
    {
        var ds4Usb = new byte[64]; ds4Usb[0] = 0x01; ds4Usb[30] = 0x14;
        var ds4Bt = new byte[78]; ds4Bt[0] = 0x11; ds4Bt[32] = 0x14;
        foreach (var report in new[] { ds4Usb, ds4Bt })
        {
            Assert.True(PlayStationReport.TryParseBattery(report, dualSense: false, out var b));
            Assert.Equal(Known(BatteryLevel.Low, true), b);
        }
        var dsUsb = new byte[64]; dsUsb[0] = 0x01; dsUsb[53] = 0x07;
        var dsBt = new byte[78]; dsBt[0] = 0x31; dsBt[54] = 0x07;
        foreach (var report in new[] { dsUsb, dsBt })
        {
            Assert.True(PlayStationReport.TryParseBattery(report, dualSense: true, out var b));
            Assert.Equal(Known(BatteryLevel.Medium, false), b);
        }
    }

    [Fact]
    public void PlayStationBasicOrShortReportsHaveNoBattery()
    {
        var basic = new byte[10]; basic[0] = 0x01;
        Assert.False(PlayStationReport.TryParseBattery(basic, false, out _));
        var shortDs = new byte[40]; shortDs[0] = 0x01;
        Assert.False(PlayStationReport.TryParseBattery(shortDs, true, out _));
        Assert.False(PlayStationReport.TryParseBattery([], false, out _));
    }

    [Theory]
    [InlineData(1, 3, BatteryAvailability.Unsupported, BatteryLevel.Empty)] // wired
    [InlineData(0, 3, BatteryAvailability.Unknown, BatteryLevel.Empty)]     // disconnected
    [InlineData(0xFF, 2, BatteryAvailability.Unknown, BatteryLevel.Empty)]
    [InlineData(2, 0, BatteryAvailability.Known, BatteryLevel.Empty)]
    [InlineData(3, 1, BatteryAvailability.Known, BatteryLevel.Low)]
    [InlineData(2, 2, BatteryAvailability.Known, BatteryLevel.Medium)]
    [InlineData(3, 3, BatteryAvailability.Known, BatteryLevel.Full)]
    public void XInputMapping(byte type, byte level, BatteryAvailability availability, BatteryLevel expected)
    {
        var status = XInputReader.MapBattery(0, new XInputReader.BatteryInformation { BatteryType = type, BatteryLevel = level });
        Assert.Equal(availability, status.Availability);
        if (availability == BatteryAvailability.Known)
        {
            Assert.Equal(expected, status.Level);
            Assert.False(status.Charging);
        }
    }

    [Fact]
    public void XInputFailedCallIsUnknown() =>
        Assert.Equal(BatteryStatus.Unknown, XInputReader.MapBattery(1167, new XInputReader.BatteryInformation { BatteryType = 2, BatteryLevel = 3 }));

    private sealed class XRig
    {
        public long Now = 10_000;
        public int BatteryCalls;
        public bool Throw;
        public byte Level = 3;
        public readonly XInputReader Reader;
        public XRig()
        {
            Reader = new XInputReader(
                (uint index, out State state) => { state = default; return index == 1 ? 0u : 1167u; },
                (_, _, _) => { }, () => Now,
                (uint index, out XInputReader.BatteryInformation info) =>
                {
                    BatteryCalls++;
                    if (Throw) throw new InvalidOperationException();
                    info = new XInputReader.BatteryInformation { BatteryType = 3, BatteryLevel = Level };
                    return 0;
                });
        }
    }

    [Fact]
    public void XInputBatteryIsQueriedAboutEveryFiveSeconds()
    {
        var rig = new XRig();
        Assert.Equal(BatteryStatus.Unknown, rig.Reader.Battery);
        for (int i = 0; i < 100; i++) { rig.Now += 40; rig.Reader.TryGetState(out _); } // 4 s
        Assert.Equal(1, rig.BatteryCalls);
        Assert.Equal(Known(BatteryLevel.Full, false), rig.Reader.Battery);
        rig.Level = 1;
        rig.Now += 1100; // 5.1 s after the first read
        rig.Reader.TryGetState(out _);
        Assert.Equal(2, rig.BatteryCalls);
        Assert.Equal(Known(BatteryLevel.Low, false), rig.Reader.Battery);
    }

    [Fact]
    public void XInputBatteryFailureIsUnknownAndDoesNotBreakReading()
    {
        var rig = new XRig { Throw = true };
        Assert.True(rig.Reader.TryGetState(out _));
        Assert.Equal(BatteryStatus.Unknown, rig.Reader.Battery);
    }

    private sealed class BatterySource(BatteryStatus battery, bool available) : IInputReader, IBatterySource
    {
        public bool ThrowOnBattery;
        public BatteryStatus Battery => ThrowOnBattery ? throw new InvalidOperationException() : battery;
        public bool TryGetState(out State state) { state = default; return available; }
        public void Dispose() { }
    }

    [Fact]
    public void AutoReaderReportsTheBatteryOfTheActiveSource()
    {
        var a = new BatterySource(Known(BatteryLevel.Low, true), available: false);
        var b = new BatterySource(Known(BatteryLevel.Full, false), available: true);
        using var reader = new AutoInputReader([a, b], ["A", "B"]);
        Assert.Equal(BatteryStatus.Unknown, reader.Battery); // nothing active yet
        Assert.True(reader.TryGetState(out _));
        Assert.Equal(Known(BatteryLevel.Full, false), reader.Battery);
        b.ThrowOnBattery = true;
        Assert.Equal(BatteryStatus.Unknown, reader.Battery);
    }
}
