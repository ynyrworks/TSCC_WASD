using Xunit;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.Tests;

public class AxisConverterTests
{
    [Theory]
    [InlineData(short.MinValue, false, 0)]
    [InlineData(short.MaxValue, false, 255)]
    [InlineData(short.MinValue, true, 255)]
    [InlineData(short.MaxValue, true, 0)]
    [InlineData(0, false, 128)]
    [InlineData(0, true, 128)]
    [InlineData(1000, false, 128)]
    [InlineData(-1000, true, 128)]
    public void EndpointsAndDeadZone(short input, bool invert, byte expected)
        => Assert.Equal(expected, AxisConverter.Convert(input, 0.08, invert));

    [Fact]
    public void EveryShortInputIsMonotonicAndSafe()
    {
        byte previous = 0;
        for (int value = short.MinValue; value <= short.MaxValue; value++)
        {
            byte actual = AxisConverter.Convert((short)value, 0.08);
            Assert.True(actual >= previous, $"Not monotonic at {value}");
            previous = actual;
        }
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDeadZoneRejected(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => AxisConverter.Convert(0, value));

    [Theory]
    [InlineData(3000, 3000)]   // each axis alone is past half the dead zone
    [InlineData(-2000, 2000)]
    public void InsideRadiusBothAxesCentre(short x, short y)
        => Assert.Equal(((short)0, (short)0), AxisConverter.ApplyRadial(x, y, 0.15));

    [Fact]
    public void OutsideRadiusKeepsDirection()
    {
        var (x, y) = AxisConverter.ApplyRadial(20000, 10000, 0.1);
        Assert.True(x > 0 && y > 0);
        Assert.Equal(2.0, (double)x / y, 2);
    }

    [Fact]
    public void ContinuousFromDeadZoneEdgeToFull()
    {
        const double dz = 0.2;
        int previous = 0;
        for (int v = 0; v <= short.MaxValue; v++)
        {
            int x = AxisConverter.ApplyRadial((short)v, 0, dz).X;
            Assert.True(x >= previous && x - previous <= 2, $"Jump at {v}");
            previous = x;
        }
        Assert.Equal(short.MaxValue, previous);
    }

    [Theory]
    [InlineData(short.MaxValue, short.MaxValue)]
    [InlineData(short.MinValue, short.MinValue)]
    [InlineData(short.MinValue, short.MaxValue)]
    public void DiagonalFullPushStaysInRange(short x, short y)
    {
        var (ox, oy) = AxisConverter.ApplyRadial(x, y, 0.08);
        Assert.True(Math.Sqrt((double)ox * ox + (double)oy * oy) <= 32768.5);
        Assert.Equal(Math.Sign(x), Math.Sign(ox));
        Assert.Equal(Math.Sign(y), Math.Sign(oy));
    }

    [Theory]
    [InlineData(0.08)]
    [InlineData(0.5)]
    [InlineData(0)]
    public void SingleAxisMatchesPerAxisFormula(double dz)
    {
        for (int v = short.MinValue; v <= short.MaxValue; v += 7)
        {
            byte old = AxisConverter.Convert((short)v, dz);
            var (x, y) = AxisConverter.ApplyRadial((short)v, 0, dz);
            // Two roundings (short, then byte) can differ from the old single rounding by one step.
            Assert.InRange(AxisConverter.ToByte(x), old - 1, old + 1);
            Assert.Equal((short)0, y);
            Assert.InRange(AxisConverter.ToByte(AxisConverter.ApplyRadial(0, (short)v, dz).Y, invert: true),
                AxisConverter.Convert((short)v, dz, invert: true) - 1, AxisConverter.Convert((short)v, dz, invert: true) + 1);
        }
    }

    [Fact]
    public void ZeroDeadZonePassesThrough()
    {
        Assert.Equal(((short)0, (short)0), AxisConverter.ApplyRadial(0, 0, 0));
        Assert.Equal(((short)1000, (short)0), AxisConverter.ApplyRadial(1000, 0, 0));
        Assert.Equal((short.MinValue, (short)0), AxisConverter.ApplyRadial(short.MinValue, 0, 0));
    }

    [Fact]
    public void InvalidDeadZoneRejectedByApplyRadial()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AxisConverter.ApplyRadial(0, 0, 1.0));
}
