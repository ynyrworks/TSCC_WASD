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

    private static readonly string[] Names = ["NS2 Pro", "NS2 Pro（藍牙）", "DualShock 4／DualSense", "Switch Pro", "Xbox／XInput"];

    private static IInputReader[] Waiting(out FakeSource ns2) =>
        [ns2 = new FakeSource { Status = "尋找 NS2 Pro（USB）..." }, new FakeSource { Status = "Waiting for an NS2 Pro over Bluetooth: hold its Sync button" },
         new FakeSource { Status = "Looking for a DualShock 4 / DualSense..." }, new FakeSource { Status = "尋找 Switch Pro 手把..." }, new FakeSource { Status = "XInput" }];

    [Theory]
    [InlineData(false, "Auto-detect: looking for a controller (NS2 Pro, DualShock 4／DualSense, Switch Pro, Xbox)")]
    [InlineData(true, "自動偵測：尋找手把中（NS2 Pro、DualShock 4／DualSense、Switch Pro、Xbox）")]
    public void SearchingWithNothingWrongIsOneShortLine(bool chinese, string expected)
    {
        Assert.Equal(expected, AutoInputReader.SearchingStatus(Waiting(out _), Names, "", chinese));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealReaderWaitingTextsAreNotShownAsProblems(bool chinese)
    {
        string[] texts = chinese
            ?
            [
                "等待 NS2 Pro USB 連線（不支援藍牙）。", "等待 NS2 Pro 藍牙：按住手把上的 Sync 鍵（實驗性）",
                "等待 DualShock 4／DualSense 連線（USB 或藍牙）。", "等待 Switch Pro 手把連線（USB 或藍牙）。", "未連線", "裝置沒有連線",
            ]
            :
            [
                "Waiting for an NS2 Pro over USB (Bluetooth is not supported).", "Waiting for an NS2 Pro over Bluetooth: hold its Sync button (experimental)",
                "Waiting for a DualShock 4 / DualSense (USB or Bluetooth).", "Waiting for a Switch Pro Controller (USB or Bluetooth).", "Not connected", "Device not connected",
            ];
        IInputReader[] sources =
            [new FakeSource { Status = texts[0] }, new FakeSource { Status = texts[1] }, new FakeSource { Status = texts[2] },
             new FakeSource { Status = texts[3] }, new FakeSource { Status = texts[4] }];
        string expected = chinese ? "自動偵測：尋找手把中（NS2 Pro、DualShock 4／DualSense、Switch Pro、Xbox）"
                                  : "Auto-detect: looking for a controller (NS2 Pro, DualShock 4／DualSense, Switch Pro, Xbox)";
        Assert.Equal(expected, AutoInputReader.SearchingStatus(sources, Names, "", chinese));

        sources[0] = new FakeSource { Status = texts[5] };
        Assert.Equal(expected, AutoInputReader.SearchingStatus(sources, Names, "", chinese));
    }

    [Theory]
    [InlineData(false, "NS2 Pro: Initializing NS2 Pro USB and stick calibration")]
    [InlineData(true, "NS2 Pro：初始化 NS2 Pro USB 與搖桿校準")]
    [InlineData(false, "NS2 Pro: Connecting...")]
    [InlineData(true, "NS2 Pro：正在連線...")]
    [InlineData(false, "NS2 Pro: Device not connected")]
    [InlineData(true, "NS2 Pro：裝置沒有連線")]
    [InlineData(false, "Initializing...")]
    [InlineData(true, "初始化...")]
    public void TransientTextsWithOrWithoutNamePrefixAreWaiting(bool chinese, string status)
    {
        var sources = Waiting(out var ns2);
        ns2.Status = status;
        string expected = chinese ? "自動偵測：尋找手把中（NS2 Pro、DualShock 4／DualSense、Switch Pro、Xbox）"
                                  : "Auto-detect: looking for a controller (NS2 Pro, DualShock 4／DualSense, Switch Pro, Xbox)";
        Assert.Equal(expected, AutoInputReader.SearchingStatus(sources, Names, "", chinese));
    }

    [Theory]
    [InlineData(false, "Auto-detect: looking for a controller (NS2 Pro, DualShock 4／DualSense, Switch Pro, Xbox): NS2 Pro USB is in use: exit Steam")]
    [InlineData(true, "自動偵測：尋找手把中（NS2 Pro、DualShock 4／DualSense、Switch Pro、Xbox）：NS2 Pro USB 被占用：請先退出 Steam")]
    public void ProblemIsAddedOnceWithoutRepeatingTheReaderName(bool chinese, string expected)
    {
        var sources = Waiting(out var ns2);
        ns2.Status = chinese ? "NS2 Pro USB 被占用：請先退出 Steam。" : "NS2 Pro USB is in use: exit Steam.";
        string text = AutoInputReader.SearchingStatus(sources, Names, "", chinese);
        Assert.Equal(expected, text);
        Assert.DoesNotContain("NS2 Pro：NS2 Pro", text);
    }

    [Fact]
    public void ProblemWithoutReaderNameGetsItAndPunctuationStaysClean()
    {
        var sources = Waiting(out var ns2);
        ns2.Status = "gone。";
        string text = AutoInputReader.SearchingStatus(sources, Names, "boom。", true);
        Assert.EndsWith("：NS2 Pro：gone；boom", text);
        Assert.DoesNotContain("。", text);
        Assert.DoesNotContain("；）", text);
        Assert.DoesNotContain("。；", text);
        Assert.DoesNotContain("。）", text);
    }

    [Fact]
    public void ActiveControllerStatusStillNamesIt()
    {
        var a = new FakeSource { Available = true, Status = "NS2 Pro（USB，原廠校準）" };
        using var reader = new AutoInputReader([a, new FakeSource()], ["NS2 Pro", "Xbox／XInput"]);
        reader.TryGetState(out _);
        Assert.Contains("NS2 Pro（USB，原廠校準）", reader.Status);
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
