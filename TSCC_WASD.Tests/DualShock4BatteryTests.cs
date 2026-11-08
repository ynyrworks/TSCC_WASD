using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.VirtualControllers;
using Xunit;

namespace TSCC_WASD.Tests;

public class DualShock4BatteryTests
{
    private static byte[] Build(BatteryStatus battery)
        => DualShock4Report.Build(new State(), 0.08, null, 1, 2, battery);

    [Theory]
    [InlineData(BatteryLevel.Empty, 0)]
    [InlineData(BatteryLevel.Critical, 1)]
    [InlineData(BatteryLevel.Low, 3)]
    [InlineData(BatteryLevel.Medium, 6)]
    [InlineData(BatteryLevel.Full, 10)]
    public void LevelAndChargingMapToStatusByte(BatteryLevel level, int nibble)
    {
        Assert.Equal(nibble, Build(BatteryStatus.Of(level, false))[29]);
        Assert.Equal(0x10 | nibble, Build(BatteryStatus.Of(level, true))[29]);
    }

    [Fact]
    public void UnknownAndUnsupportedKeepOldBytes()
    {
        foreach (var battery in new[] { BatteryStatus.Unknown, BatteryStatus.Unsupported })
        {
            var r = Build(battery);
            Assert.Equal(0x1B, r[29]);
            Assert.Equal(0xFF, r[11]);
        }
    }

    [Fact]
    public void OnlyBatteryByteDiffersFromOldBuild()
    {
        var old = DualShock4Report.Build(new State(), 0.08, null, 1, 2);
        var now = Build(BatteryStatus.Of(BatteryLevel.Low, false));
        for (int i = 0; i < old.Length; i++)
            if (i != 29) Assert.Equal(old[i], now[i]);
    }

    [Theory]
    [InlineData(BatteryLevel.Empty, false)]
    [InlineData(BatteryLevel.Critical, true)]
    [InlineData(BatteryLevel.Low, false)]
    [InlineData(BatteryLevel.Medium, true)]
    [InlineData(BatteryLevel.Full, false)]
    [InlineData(BatteryLevel.Full, true)]
    public void RoundTripsThroughParser(BatteryLevel level, bool charging)
    {
        var back = BatteryParser.FromDualShock4(Build(BatteryStatus.Of(level, charging))[29]);
        Assert.Equal(BatteryStatus.Of(level, charging), back);
    }
}
