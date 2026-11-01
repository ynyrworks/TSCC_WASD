using Xunit;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

public class AutoInputReaderTests
{
    private static State Pressed(GamepadButtonFlags buttons) => new() { Gamepad = new Gamepad { Buttons = buttons } };

    [Fact]
    public void IdleControllerDoesNotStealFromTheOneInUse()
    {
        var ns2 = new FakeSource { Available = true };
        var xbox = new FakeSource { Available = true };
        using var reader = new AutoInputReader(ns2, xbox);

        Assert.True(reader.TryGetState(out _));
        Assert.Equal("NS2 Pro", reader.ActiveSource);

        xbox.State = Pressed(GamepadButtonFlags.A);
        Assert.True(reader.TryGetState(out var state));
        Assert.Equal("Xbox／XInput", reader.ActiveSource);
        Assert.Equal(GamepadButtonFlags.A, state.Gamepad.Buttons);

        // Releasing the button keeps Xbox active although NS2 still streams neutral reports.
        xbox.State = default;
        reader.TryGetState(out _);
        Assert.Equal("Xbox／XInput", reader.ActiveSource);

        ns2.State = Pressed(GamepadButtonFlags.B);
        reader.TryGetState(out state);
        Assert.Equal("NS2 Pro", reader.ActiveSource);
        Assert.Equal(GamepadButtonFlags.B, state.Gamepad.Buttons);
    }

    [Theory]
    [InlineData(false, "NS2 Pro: a; Xbox／XInput: a")]
    [InlineData(true, "NS2 Pro：a；Xbox／XInput：a")]
    public void SearchingStatusUsesSeparatorsOfTheLanguage(bool chinese, string expectedList)
    {
        var ns2 = new FakeSource { Status = "a" };
        var xbox = new FakeSource { Status = "a" };
        string list = AutoInputReader.SearchingList([ns2, xbox], ["NS2 Pro", "Xbox／XInput"], chinese);
        Assert.Equal(expectedList, list);
    }

    [Fact]
    public void ActiveSourceDoesNotSwitchWhileHeld()
    {
        var ns2 = new FakeSource { Available = true, State = Pressed(GamepadButtonFlags.A) };
        var xbox = new FakeSource { Available = true, State = Pressed(GamepadButtonFlags.B) };
        using var reader = new AutoInputReader(ns2, xbox);
        reader.TryGetState(out var state);
        Assert.Equal("NS2 Pro", reader.ActiveSource);
        Assert.Equal(GamepadButtonFlags.A, state.Gamepad.Buttons);
    }

    [Fact]
    public void FallsBackWhenActiveDisconnectsOrThrows()
    {
        var ns2 = new FakeSource { Available = true };
        var xbox = new FakeSource { Available = true };
        using var reader = new AutoInputReader(ns2, xbox);
        reader.TryGetState(out _);
        ns2.Throw = true;
        Assert.True(reader.TryGetState(out _));
        Assert.Equal("Xbox／XInput", reader.ActiveSource);

        xbox.Available = false;
        Assert.False(reader.TryGetState(out var state));
        Assert.Null(reader.ActiveSource);
        Assert.Equal(default, state);
    }

    [Fact]
    public void SmallStickDriftIsNotTreatedAsUse()
    {
        Assert.False(AutoInputReader.IsInUse(new State { Gamepad = new Gamepad { LeftThumbX = 3000, RightTrigger = 10 } }));
        Assert.True(AutoInputReader.IsInUse(new State { Gamepad = new Gamepad { LeftThumbY = short.MinValue } }));
        Assert.True(AutoInputReader.IsInUse(new State { Gamepad = new Gamepad { LeftTrigger = 200 } }));
    }

    [Fact]
    public void RumbleAndMotionFollowTheActiveSource()
    {
        var ns2 = new FakeSource { Available = true };
        var xbox = new FakeSource { Available = true };
        using var reader = new AutoInputReader(ns2, xbox);
        reader.TryGetState(out _);
        reader.SetRumble(100, 20);
        Assert.Equal((100, 20), ns2.Rumble);
        Assert.True(reader.TryGetMotion(out var motion));
        Assert.Equal(ns2.Motion, motion);

        xbox.State = Pressed(GamepadButtonFlags.A);
        reader.TryGetState(out _);
        Assert.Equal((0, 0), ns2.Rumble);    // Stopped on the old controller...
        Assert.Equal((100, 20), xbox.Rumble); // ...and replayed on the new one.
    }

    [Fact]
    public void SearchingStatusListsEverySourceInOrder()
    {
        var a = new FakeSource { Status = "status-a" };
        var b = new FakeSource { Status = "status-b" };
        var c = new FakeSource { Status = "status-c" };
        using var reader = new AutoInputReader([a, b, c], ["Alpha", "Beta", "Gamma"]);

        string status = reader.Status;
        int ia = status.IndexOf("Alpha", StringComparison.Ordinal);
        int ib = status.IndexOf("Beta", StringComparison.Ordinal);
        int ic = status.IndexOf("Gamma", StringComparison.Ordinal);
        Assert.True(ia >= 0 && ia < ib && ib < ic, status);
        Assert.True(status.IndexOf("status-a", StringComparison.Ordinal) < status.IndexOf("status-b", StringComparison.Ordinal), status);
        Assert.Contains("status-c", status);
    }

    [Fact]
    public void DisposesBothSources()
    {
        var ns2 = new FakeSource();
        var xbox = new FakeSource();
        new AutoInputReader(ns2, xbox).Dispose();
        Assert.True(ns2.Disposed && xbox.Disposed);
    }

    private sealed class FakeSource : IInputReader, IRumbleTarget, IMotionSource
    {
        public (int Large, int Small) Rumble { get; private set; }
        public MotionSample Motion { get; } = new(1, 2, 3, 4, 5, 6);
        public void SetRumble(byte large, byte small) => Rumble = (large, small);
        public bool TryGetMotion(out MotionSample motion) { motion = Motion; return Available; }
        public bool Available { get; set; }
        public string Status { get; set; } = "XInput";
        public bool Throw { get; set; }
        public State State { get; set; }
        public bool Disposed { get; private set; }
        public bool TryGetState(out State state)
        {
            if (Throw) throw new InvalidOperationException("gone");
            state = Available ? State : default;
            return Available;
        }
        public void Dispose() => Disposed = true;
    }
}
