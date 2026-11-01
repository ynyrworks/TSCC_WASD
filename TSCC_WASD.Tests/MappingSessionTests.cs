using Xunit;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;
using TSCC_WASD.Core.Services.VirtualControllers;

namespace TSCC_WASD.Tests;

public class MappingSessionTests
{
    [Fact]
    public async Task DisconnectReleasesHeldButtonsAndStopDisposesResources()
    {
        var input = new FakeInput();
        var output = new FakeOutput();
        var session = new MappingSession(new MappingProfile(), input, output);
        Assert.True(session.TryStart());
        await output.NeutralAfterPress.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.DisposeAsync();
        await session.DisposeAsync();
        Assert.False(session.IsRunning);
        Assert.False(output.IsConnected);
        Assert.Equal(1, output.DisposeCount);
        Assert.True(input.Disposed);
        Assert.Equal(GamepadButtonFlags.None, output.LastState.Gamepad.Buttons);
    }

    [Fact]
    public async Task DriverFailureReconnectsAndKeepsMapping()
    {
        var output = new FakeOutput { FailPushes = 1 };
        var input = new FakeInput();
        await using var session = new MappingSession(new MappingProfile(), input, output);
        Assert.True(session.TryStart());
        await output.Reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.IsRunning);
        Assert.Equal("driver failure", session.LastError);
        await session.StopAsync();
        Assert.False(session.IsRunning);
        Assert.False(output.IsConnected);
        Assert.True(input.Disposed);
    }

    [Fact]
    public async Task ThrowingInputIsTreatedAsDisconnectedWithoutEndingSession()
    {
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile(), new ThrowingInput(), output);
        Assert.True(session.TryStart());
        await output.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.IsRunning);
        Assert.Equal("read failure", session.LastError);
        Assert.Equal(GamepadButtonFlags.None, output.LastState.Gamepad.Buttons);
    }

    [Fact]
    public async Task FailedConnectionCanBeDisposed()
    {
        var output = new FakeOutput { FailConnect = true };
        var session = new MappingSession(new MappingProfile(), new FakeInput(), output);
        Assert.False(session.TryStart());
        await session.DisposeAsync();
        Assert.Equal(1, output.DisposeCount);
    }

    [Fact]
    public async Task ThrowingStatusCallbackDoesNotInterruptMapping()
    {
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile(), new FakeInput(), output,
            _ => throw new InvalidOperationException("UI closed"));
        Assert.True(session.TryStart());
        await output.NeutralAfterPress.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.StopAsync();
        Assert.Null(session.LastError);
    }

    [Theory]
    [InlineData(OutputControllerType.DualSense)]
    [InlineData((OutputControllerType)99)]
    public void UnsupportedOutputFailsExplicitly(OutputControllerType type)
        => Assert.Throws<NotSupportedException>(() => VirtualControllerFactory.Create(type));

    [Fact]
    public async Task InputSummaryIsBuiltFromTheLatestReadingWhenRead()
    {
        var input = new ConstantInput();
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile(), input, output);
        var before = session.InputSummary;
        Assert.True(session.TryStart());
        await output.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var summary = session.InputSummary;
        Assert.Contains("(123, -456)", summary);
        Assert.Contains("(7, 8)", summary);
        Assert.Contains("A", summary);
        Assert.Contains("200/100", summary);
        Assert.NotEqual(before, summary);
        session.Paused = true;
        await Task.Delay(300);
        Assert.DoesNotContain("(123, -456)", session.InputSummary);
    }

    private sealed class ConstantInput : IInputReader
    {
        public bool TryGetState(out State state)
        {
            state = new State { Gamepad = new Gamepad { Buttons = GamepadButtonFlags.A, LeftThumbX = 123, LeftThumbY = -456,
                RightThumbX = 7, RightThumbY = 8, LeftTrigger = 200, RightTrigger = 100 } };
            return true;
        }
        public void Dispose() { }
    }

    private sealed class FakeInput : IInputReader
    {
        private int _reads;
        public bool Disposed { get; private set; }
        public bool TryGetState(out State state)
        {
            state = new State { Gamepad = new Gamepad { Buttons = GamepadButtonFlags.A } };
            return Interlocked.Increment(ref _reads) == 1;
        }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task ForwardsRumbleAndMotionAndStopsRumbleOnDispose()
    {
        var input = new RumbleInput();
        var output = new FakeOutput();
        var session = new MappingSession(new MappingProfile(), input, output);
        Assert.True(session.TryStart());
        await output.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new MotionSample(1, 2, 3, 4, 5, 6), output.LastMotion);
        output.RaiseRumble(200, 50);
        Assert.Equal((200, 50), input.Last);
        await session.DisposeAsync();
        Assert.Equal((0, 0), input.Last);
        Assert.Null(output.LastMotion); // The final neutral report carries no motion.
    }

    [Fact]
    public async Task RumbleForwardingCanBeDisabled()
    {
        var input = new RumbleInput();
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile { ForwardRumble = false }, input, output);
        Assert.True(session.TryStart());
        output.RaiseRumble(200, 50);
        Assert.Equal((0, 0), input.Last);
    }

    [Fact]
    public async Task PauseSendsNeutralWithoutRumbleAndResumes()
    {
        var input = new RumbleInput { Held = GamepadButtonFlags.A };
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile(), input, output);
        Assert.True(session.TryStart());
        await WaitUntil(() => output.LastState.Gamepad.Buttons == GamepadButtonFlags.A);
        output.RaiseRumble(200, 50);

        session.Paused = true;
        Assert.Equal((0, 0), input.Last); // Pausing stops rumble right away.
        await WaitUntil(() => output.LastState.Gamepad.Buttons == GamepadButtonFlags.None && output.LastMotion is null);
        output.RaiseRumble(200, 50);
        Assert.Equal((0, 0), input.Last);
        Assert.True(session.IsRunning);
        Assert.True(output.IsConnected); // The virtual DS4 stays, so nothing has to re-detect it.

        session.Paused = false;
        await WaitUntil(() => output.LastState.Gamepad.Buttons == GamepadButtonFlags.A);
    }

    [Fact]
    public async Task ProfileChangesApplyWhileRunning()
    {
        var input = new RumbleInput();
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile { DeadZone = 0.1 }, input, output);
        Assert.True(session.TryStart());
        await WaitUntil(() => output.LastDeadZone == 0.1);
        output.RaiseRumble(200, 50);

        session.UpdateProfile(new MappingProfile { DeadZone = 0.3, ForwardRumble = false });
        await WaitUntil(() => output.LastDeadZone == 0.3);
        Assert.Equal((0, 0), input.Last); // Turning rumble off stops it.
        output.RaiseRumble(200, 50);
        Assert.Equal((0, 0), input.Last);
    }

    [Fact]
    public void XboxViewButtonBecomesTouchpadOrStaysShare()
    {
        var pressed = GamepadButtonFlags.Back | GamepadButtonFlags.A;
        Assert.Equal(GamepadButtonFlags.Touchpad | GamepadButtonFlags.A, XInputReader.MapBack(pressed, backAsTouchpad: true));
        Assert.Equal(pressed, XInputReader.MapBack(pressed, backAsTouchpad: false));
        Assert.Equal(GamepadButtonFlags.A, XInputReader.MapBack(GamepadButtonFlags.A, backAsTouchpad: true));
        Assert.True(new MappingProfile().XboxBackAsTouchpad); // Default: the touchpad is reachable on Xbox.
    }

    [Fact]
    public async Task ViewButtonOptionFollowsTheProfileLive()
    {
        var input = new OptionInput();
        await using var session = new MappingSession(new MappingProfile(), input, new FakeOutput());
        Assert.True(input.BackAsTouchpad);
        session.UpdateProfile(new MappingProfile { XboxBackAsTouchpad = false });
        Assert.False(input.BackAsTouchpad);

        var auto = new AutoInputReader(new OptionInput(), new OptionInput { BackAsTouchpad = true });
        auto.BackAsTouchpad = false;
        Assert.False(auto.BackAsTouchpad);
    }

    private sealed class OptionInput : IInputReader, IXboxBackButtonOption
    {
        public bool BackAsTouchpad { get; set; }
        public bool TryGetState(out State state) { state = default; return false; }
        public void Dispose() { }
    }

    [Fact]
    public void Xbox360OutputKeepsXInputButtonsAndMapsTouchpadToBack()
    {
        var pressed = GamepadButtonFlags.A | GamepadButtonFlags.Guide | GamepadButtonFlags.DPadUp;
        Assert.Equal((ushort)pressed, Xbox360Report.Buttons(pressed));
        Assert.Equal((ushort)(GamepadButtonFlags.Back | GamepadButtonFlags.Y),
            Xbox360Report.Buttons(GamepadButtonFlags.Touchpad | GamepadButtonFlags.Y));
        Assert.Equal((short)0, Xbox360Report.Axis(1000, 0.1));
        Assert.Equal(short.MaxValue, Xbox360Report.Axis(short.MaxValue, 0.1));
        Assert.Equal(short.MinValue, Xbox360Report.Axis(short.MinValue, 0.1));
        Assert.True(Xbox360Report.Axis(20000, 0.1) is > 0 and < 20000); // Rescaled past the dead zone.
        new MappingProfile { OutputType = OutputControllerType.Xbox360 }.Validate();
    }

    [Fact]
    public void SwappingFaceButtonsExchangesAWithBAndXWithY()
    {
        Assert.Equal(GamepadButtonFlags.B | GamepadButtonFlags.Start,
            MappingSession.SwapFaceButtons(GamepadButtonFlags.A | GamepadButtonFlags.Start));
        Assert.Equal(GamepadButtonFlags.A | GamepadButtonFlags.X,
            MappingSession.SwapFaceButtons(GamepadButtonFlags.B | GamepadButtonFlags.Y));
        Assert.False(new MappingProfile().SwapFaceButtons);
    }

    [Fact]
    public async Task SwapAppliesToWhatTheVirtualPadReceives()
    {
        var output = new FakeOutput();
        await using var session = new MappingSession(new MappingProfile { SwapFaceButtons = true },
            new RumbleInput { Held = GamepadButtonFlags.A }, output);
        Assert.True(session.TryStart());
        await WaitUntil(() => output.LastState.Gamepad.Buttons == GamepadButtonFlags.B);
    }

    [Fact]
    public async Task OwnVirtualXboxSlotIsNeverReadBack()
    {
        var input = new SlotInput();
        var output = new SlotOutput { Slot = 2 };
        await using var session = new MappingSession(new MappingProfile { OutputType = OutputControllerType.Xbox360 }, input, output);
        Assert.True(session.TryStart());
        await WaitUntil(() => input.ExcludedSlot == 2);
    }

    private sealed class SlotInput : IInputReader, IXInputSlotFilter
    {
        public int? ExcludedSlot { get; set; }
        public bool TryGetState(out State state) { state = default; return true; }
        public void Dispose() { }
    }

    private sealed class SlotOutput : IVirtualController, IXInputSlotOwner
    {
        public int? Slot { get; init; }
        public int? XInputSlot => Slot;
        public bool IsConnected { get; private set; }
        public string? LastError => null;
#pragma warning disable CS0067 // Not raised in this test.
        public event Action<byte, byte>? RumbleRequested;
#pragma warning restore CS0067
        public bool TryConnect(out string? error) { error = null; return IsConnected = true; }
        public void PushState(State state, double deadZone, MotionSample? motion = null) { }
        public void Disconnect() => IsConnected = false;
        public void Dispose() => Disconnect();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met.");
            await Task.Delay(10);
        }
    }

    private sealed class RumbleInput : IInputReader, IRumbleTarget, IMotionSource
    {
        public (int Large, int Small) Last { get; private set; }
        public GamepadButtonFlags Held { get; init; }
        public bool TryGetState(out State state) { state = new State { Gamepad = new Gamepad { Buttons = Held } }; return true; }
        public bool TryGetMotion(out MotionSample motion) { motion = new MotionSample(1, 2, 3, 4, 5, 6); return true; }
        public void SetRumble(byte large, byte small) => Last = (large, small);
        public void Dispose() { }
    }

    private sealed class ThrowingInput : IInputReader
    {
        public bool TryGetState(out State state) => throw new InvalidOperationException("read failure");
        public void Dispose() { }
    }

    private sealed class FakeOutput : IVirtualController
    {
        public bool IsConnected { get; private set; }
        public string? LastError => null;
        public int FailPushes { get; set; }
        private int _connects;
        public bool FailConnect { get; init; }
        public int DisposeCount { get; private set; }
        public State LastState { get; private set; }
        private bool _pressed;
        public TaskCompletionSource NeutralAfterPress { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Reconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Pushed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryConnect(out string? error)
        {
            error = FailConnect ? "unavailable" : null;
            if (!FailConnect && ++_connects > 1) Reconnected.TrySetResult();
            return IsConnected = !FailConnect;
        }
        public event Action<byte, byte>? RumbleRequested;
        public void RaiseRumble(byte large, byte small) => RumbleRequested?.Invoke(large, small);
        public MotionSample? LastMotion { get; private set; }
        public double LastDeadZone { get; private set; } = double.NaN;
        public void PushState(State state, double deadZone, MotionSample? motion = null)
        {
            LastMotion = motion;
            LastDeadZone = deadZone;
            if (FailPushes > 0) { FailPushes--; throw new InvalidOperationException("driver failure"); }
            LastState = state;
            Pushed.TrySetResult();
            if (state.Gamepad.Buttons == GamepadButtonFlags.A) _pressed = true;
            if (_pressed && state.Gamepad.Buttons == GamepadButtonFlags.None) NeutralAfterPress.TrySetResult();
        }
        public void Disconnect() => IsConnected = false;
        public void Dispose() { DisposeCount++; Disconnect(); }
    }
}
