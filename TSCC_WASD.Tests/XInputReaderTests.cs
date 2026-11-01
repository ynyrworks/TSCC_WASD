using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

public class XInputReaderTests
{
    private sealed class Rig
    {
        public readonly bool[] Connected = new bool[4];
        public readonly int[] Calls = new int[4];
        public readonly List<(int Slot, byte Large, byte Small)> Vibrations = new();
        public long Now = 10_000;
        public XInputReader Reader;

        public Rig()
        {
            Reader = new XInputReader(Get, (s, l, m) => Vibrations.Add((s, l, m)), () => Now);
        }

        private uint Get(uint index, out State state)
        {
            Calls[index]++;
            state = new State { Gamepad = new Gamepad { LeftThumbX = (short)(index + 1) } };
            return Connected[index] ? 0u : 1167u;
        }

        public int Total => Calls.Sum();
        public void Reset() => Array.Clear(Calls);
    }

    [Fact]
    public void ConnectedPadIsTheOnlySlotQueriedBetweenScans()
    {
        var rig = new Rig();
        rig.Connected[2] = true;
        Assert.True(rig.Reader.TryGetState(out var s));
        Assert.Equal(3, s.Gamepad.LeftThumbX);
        rig.Reset();
        for (int i = 0; i < 10; i++)
        {
            rig.Now += 50;
            Assert.True(rig.Reader.TryGetState(out _));
        }
        Assert.Equal(10, rig.Total);
        Assert.Equal(10, rig.Calls[2]);
    }

    [Fact]
    public void EmptyLowerSlotsAreScannedAtMostOncePerSecondAndLowerPadWins()
    {
        var rig = new Rig();
        rig.Connected[2] = true;
        rig.Reader.TryGetState(out _);
        rig.Reset();
        rig.Now += 500;
        rig.Reader.TryGetState(out _);
        Assert.Equal(0, rig.Calls[0] + rig.Calls[1]);
        rig.Connected[0] = true;
        rig.Now += 600; // 1.1 s after the last scan
        Assert.True(rig.Reader.TryGetState(out var s));
        Assert.Equal(1, s.Gamepad.LeftThumbX);
        rig.Reset();
        rig.Now += 10;
        rig.Reader.TryGetState(out _);
        Assert.Equal(1, rig.Total);
        Assert.Equal(1, rig.Calls[0]);
    }

    [Fact]
    public void NoPadBacksOffToOneFullScanPerSecondAndFindsNewPadWithinASecond()
    {
        var rig = new Rig();
        Assert.False(rig.Reader.TryGetState(out _));
        Assert.Equal(4, rig.Total);
        for (int i = 0; i < 10; i++)
        {
            rig.Now += 50;
            Assert.False(rig.Reader.TryGetState(out _));
        }
        Assert.Equal(4, rig.Total);
        rig.Connected[1] = true;
        rig.Now += 600;
        Assert.True(rig.Reader.TryGetState(out var s));
        Assert.Equal(2, s.Gamepad.LeftThumbX);
    }

    [Fact]
    public void LosingTheActivePadReportsNoInputImmediatelyAndRecovers()
    {
        var rig = new Rig();
        rig.Connected[0] = true;
        rig.Reader.TryGetState(out _);
        rig.Connected[0] = false;
        rig.Now += 10;
        Assert.False(rig.Reader.TryGetState(out var s));
        Assert.Equal(default, s);
        rig.Connected[3] = true;
        rig.Now += 10;
        Assert.True(rig.Reader.TryGetState(out _));
    }

    [Fact]
    public void ExcludedSlotIsNeverReadAndChangingItRescansImmediately()
    {
        var rig = new Rig();
        rig.Connected[0] = true;
        rig.Connected[1] = true;
        rig.Reader.ExcludedSlot = 0;
        Assert.True(rig.Reader.TryGetState(out var s));
        Assert.Equal(2, s.Gamepad.LeftThumbX);
        Assert.Equal(0, rig.Calls[0]);
        rig.Now += 10;
        rig.Reader.ExcludedSlot = 1;
        Assert.True(rig.Reader.TryGetState(out s));
        Assert.Equal(1, s.Gamepad.LeftThumbX);
        rig.Reset();
        rig.Now += 10;
        rig.Reader.ExcludedSlot = 0;
        Assert.True(rig.Reader.TryGetState(out s));
        Assert.Equal(2, s.Gamepad.LeftThumbX);
        Assert.Equal(0, rig.Calls[0]);
    }

    [Fact]
    public void RumbleFollowsTheActiveSlot()
    {
        var rig = new Rig();
        rig.Connected[2] = true;
        rig.Reader.SetRumble(10, 20);
        rig.Reader.TryGetState(out _);
        Assert.Contains((2, (byte)10, (byte)20), rig.Vibrations);
        rig.Connected[0] = true;
        rig.Now += 1100;
        rig.Reader.TryGetState(out _);
        Assert.Contains((2, (byte)0, (byte)0), rig.Vibrations);
        Assert.Equal((0, (byte)10, (byte)20), rig.Vibrations[^1]);
    }
}
