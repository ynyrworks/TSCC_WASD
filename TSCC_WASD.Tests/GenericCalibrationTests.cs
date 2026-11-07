using System.IO;
using Xunit;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Calibration;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;
using TSCC_WASD.Core.Services.VirtualControllers;

namespace TSCC_WASD.Tests;

public class GenericCalibrationTests
{
    private static Gamepad Pad(short lx = 0, short ly = 0, short rx = 0, short ry = 0) =>
        new() { LeftThumbX = lx, LeftThumbY = ly, RightThumbX = rx, RightThumbY = ry };

    [Fact]
    public void RestMapsToZeroAndFullTravelToFull()
    {
        var axis = new AxisCalibration(1000, 20000, 25000);
        Assert.Equal(0, axis.Apply(1000));
        Assert.Equal(short.MaxValue, axis.Apply(21000));
        Assert.Equal(short.MinValue, axis.Apply(-24000));
        Assert.InRange(axis.Apply(11000), 16000, 16500); // half of the positive travel
    }

    [Fact]
    public void BeyondTravelClampsAndAsymmetricTravelIsPerDirection()
    {
        var axis = new AxisCalibration(-500, 10000, 30000);
        Assert.Equal(short.MaxValue, axis.Apply(short.MaxValue));
        Assert.Equal(short.MinValue, axis.Apply(short.MinValue));
        // The same distance from rest is a bigger output on the short side.
        Assert.True(axis.Apply((short)(-500 + 5000)) > -axis.Apply((short)(-500 - 5000)));
    }

    [Fact]
    public void IdentityAxisKeepsValues()
    {
        var axis = AxisCalibration.Identity;
        foreach (short v in new short[] { short.MinValue, -12345, -1, 0, 1, 777, short.MaxValue })
            Assert.Equal(v, axis.Apply(v));
    }

    [Fact]
    public void SetAppliesToBothSticksAndLeavesButtonsAlone()
    {
        var identity = AxisCalibration.Identity;
        var shifted = new AxisCalibration(2000, 20000, 20000);
        var set = new StickCalibrationSet(new(shifted, identity), new(identity, shifted));
        var pad = Pad(2000, 100, 100, 2000);
        pad.Buttons = GamepadButtonFlags.A;
        pad.LeftTrigger = 7;
        var result = set.Apply(pad);
        Assert.Equal(0, result.LeftThumbX);
        Assert.Equal(100, result.LeftThumbY);
        Assert.Equal(100, result.RightThumbX);
        Assert.Equal(0, result.RightThumbY);
        Assert.Equal(GamepadButtonFlags.A, result.Buttons);
        Assert.Equal(7, result.LeftTrigger);
    }

    private static GenericStickCalibrator CenteredCalibrator(short offset = 0)
    {
        var calibrator = new GenericStickCalibrator();
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++)
            calibrator.AddCenterSample(Pad(offset, offset, offset, offset));
        return calibrator;
    }

    [Fact]
    public void CalibratorNeedsCenterAndTravelInEveryDirection()
    {
        var calibrator = CenteredCalibrator();
        Assert.True(calibrator.CenterReady);
        Assert.False(calibrator.RangeReady);
        Assert.Throws<InvalidOperationException>(() => calibrator.Result());

        // Only three of the four directions on the left stick, nothing on the right one.
        calibrator.AddRangeSample(Pad(20000, 20000, 0, 0));
        calibrator.AddRangeSample(Pad(-20000, -20000, 0, 0));
        Assert.False(calibrator.RangeReady);
        calibrator.AddRangeSample(Pad(0, 0, 20000, 20000));
        calibrator.AddRangeSample(Pad(0, 0, -20000, -20000));
        Assert.True(calibrator.RangeReady);
        Assert.All(calibrator.Progress, p => Assert.Equal(1.0, p));
    }

    [Fact]
    public void TravelBelowTheMinimumIsNotAccepted()
    {
        var calibrator = CenteredCalibrator();
        short small = GenericStickCalibrator.MinimumTravel - 1;
        calibrator.AddRangeSample(Pad(small, small, small, small));
        calibrator.AddRangeSample(Pad((short)-small, (short)-small, (short)-small, (short)-small));
        Assert.False(calibrator.RangeReady);
    }

    [Fact]
    public void ResultUsesMeasuredCenterAndSaturatesFivePercentBeforeTheEdge()
    {
        var calibrator = CenteredCalibrator(offset: 300);
        calibrator.AddRangeSample(Pad(300 + 20000, 300 + 20000, 300 + 20000, 300 + 20000));
        calibrator.AddRangeSample(Pad(300 - 10000, 300 - 10000, 300 - 10000, 300 - 10000));
        var set = calibrator.Result();
        Assert.Equal(300, set.Left.X.Center);
        Assert.Equal(19000, set.Left.X.Positive);
        Assert.Equal(9500, set.Right.Y.Negative);
        Assert.Equal(0, set.Left.X.Apply(300));
        Assert.Equal(short.MaxValue, set.Left.X.Apply(300 + 19000));
        Assert.Equal(short.MinValue, set.Right.Y.Apply(300 - 9500));
    }

    private static StickCalibrationSet SampleSet(short center = 100) =>
        new(new(new(center, 20000, 21000), new(center, 19000, 22000)), new(new(0, 30000, 30000), new(0, 25000, 26000)));

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "stick-calibration.json");

    private static Switch2CalibrationCache TempCache() =>
        new(Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "ns2-calibration.json"));

    [Fact]
    public void IdentityKeysPreferTheDeviceThenTheModel()
    {
        var id = new ControllerId(ControllerId.DualSense, "HID\\VID_054C&PID_0CE6\\7&1");
        Assert.Equal(["dualsense#hid\\vid_054c&pid_0ce6\\7&1", "dualsense"], id.LookupKeys().ToArray());
        Assert.Equal(["xbox"], new ControllerId("xbox").LookupKeys().ToArray());
        Assert.Equal(ControllerId.XInput, new ControllerId(ControllerId.XInput).PreferredKey);
        Assert.Equal(ControllerId.Ns2Pro, ControllerId.ModelOfKey("ns2pro#abc"));
    }

    [Fact]
    public void StoreRoundTripsAndLooksUpDeviceBeforeModel()
    {
        string path = TempFile();
        var store = new StickCalibrationStore(path, TempCache());
        var deviceId = new ControllerId(ControllerId.SwitchPro, "dev1");
        var otherDevice = new ControllerId(ControllerId.SwitchPro, "dev2");
        Assert.False(store.TryGet(deviceId, out _));

        Assert.True(store.Save(ControllerId.SwitchPro, SampleSet(1)));
        Assert.True(store.Save(deviceId.PreferredKey, SampleSet(2)));
        Assert.Equal(2, Center(store, deviceId));
        Assert.Equal(1, Center(store, otherDevice)); // falls back to the model entry

        var reloaded = new StickCalibrationStore(path, TempCache());
        Assert.Equal(2, Center(reloaded, deviceId));
        Assert.Equal(1, Center(reloaded, new ControllerId(ControllerId.SwitchPro)));
        Assert.Equal(SampleSet(2), reloaded.TryGet(deviceId, out var set) ? set : null);

        Assert.True(store.Save(deviceId.PreferredKey, SampleSet(3)));
        Assert.True(File.Exists(path + ".bak"));
    }

    private static int Center(StickCalibrationStore store, ControllerId id) =>
        store.TryGet(id, out var set) ? set.Left.X.Center : int.MinValue;

    [Fact]
    public void ClearRemovesOnlyThatController()
    {
        var store = new StickCalibrationStore(TempFile(), TempCache());
        store.Save("dualsense", SampleSet());
        store.Save("xinput", SampleSet());
        int changes = 0;
        store.Changed += () => changes++;
        Assert.True(store.Clear("dualsense"));
        Assert.False(store.Clear("dualsense"));
        Assert.False(store.TryGet("dualsense", out _));
        Assert.True(store.TryGet("xinput", out _));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SavingRaisesChangedAndBumpsTheVersion()
    {
        var store = new StickCalibrationStore(TempFile(), TempCache());
        int changes = 0, version = store.Version;
        store.Changed += () => changes++;
        Assert.True(store.Save("xinput", SampleSet()));
        Assert.Equal(1, changes);
        Assert.NotEqual(version, store.Version);
    }

    [Fact]
    public void InvalidCalibrationIsRefused()
    {
        var store = new StickCalibrationStore(TempFile(), TempCache());
        var bad = new StickCalibrationSet(new(new(0, 0, 100), new(0, 100, 100)), new(AxisCalibration.Identity, AxisCalibration.Identity));
        Assert.False(store.Save("xinput", bad));
        Assert.False(store.TryGet("xinput", out _));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"controllers\": 5}")]
    [InlineData("{\"controllers\": {\"xinput\": {\"left\": null}}}")]
    [InlineData("")]
    public void BadFileIsIgnoredAndCanBeOverwritten(string content)
    {
        string path = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        var store = new StickCalibrationStore(path, TempCache());
        Assert.False(store.TryGet(new ControllerId(ControllerId.XInput), out _));
        Assert.True(store.Save("xinput", SampleSet()));
        Assert.True(new StickCalibrationStore(path, TempCache()).TryGet("xinput", out _));
    }

    [Fact]
    public void SavingForNs2ProClearsTheOldUserCalibrationOnly()
    {
        var cache = TempCache();
        var left = Switch2StickCalibration.Nominal;
        cache.SaveFactory(left, left);
        cache.SaveUser(left, left);
        var store = new StickCalibrationStore(TempFile(), cache);

        Assert.True(store.Save("dualsense", SampleSet()));
        Assert.True(cache.TryLoadUser(out _, out _)); // another model leaves it alone

        Assert.True(store.Save(new ControllerId(ControllerId.Ns2Pro, "x").PreferredKey, SampleSet()));
        Assert.False(cache.TryLoadUser(out _, out _));
        Assert.True(cache.TryLoadFactory(out _, out _)); // factory calibration stays
    }

    [Fact]
    public void AutoInputReaderForwardsTheActiveIdentity()
    {
        var ns2 = new FakeIdentitySource { Available = false, Id = new ControllerId(ControllerId.Ns2Pro, "a") };
        var xbox = new FakeIdentitySource { Available = true, Id = new ControllerId(ControllerId.XInput) };
        using var auto = new AutoInputReader(ns2, xbox);
        Assert.Null(auto.Identity);
        Assert.True(auto.TryGetState(out _));
        Assert.Equal(xbox.Id, auto.Identity);
        ns2.Available = true;
        ns2.State = new State { Gamepad = new Gamepad { Buttons = GamepadButtonFlags.A } };
        auto.TryGetState(out _);
        Assert.Equal(ns2.Id, auto.Identity);
    }

    private sealed class FakeIdentitySource : IInputReader, IControllerIdentity
    {
        public bool Available { get; set; }
        public State State { get; set; }
        public ControllerId? Id { get; set; }
        public ControllerId? Identity => Available ? Id : null;
        public bool TryGetState(out State state) { state = Available ? State : default; return Available; }
        public void Dispose() { }
    }

    [Fact]
    public async Task SessionAppliesTheCalibrationBeforeTheOutputAndHandsOutUncalibratedSamples()
    {
        var id = new ControllerId(ControllerId.DualShock4, "dev");
        var store = new StickCalibrationStore(TempFile(), TempCache());
        var input = new FakeIdentityInput(id, Pad(1000, -1000, 5000, 0));
        var output = new RecordingOutput();
        await using var session = new MappingSession(new MappingProfile(), input, output, calibrations: store);
        Assert.True(session.TryStart());

        // No saved calibration: the state passes through untouched.
        await WaitFor(() => output.Last.Gamepad.LeftThumbX == 1000);
        Assert.Equal(5000, output.Last.Gamepad.RightThumbX);

        // Saving applies at once to the running session (device key beats model key).
        var calibration = new StickCalibrationSet(new(new(1000, 10000, 10000), AxisCalibration.Identity), new(new(5000, 10000, 10000), AxisCalibration.Identity));
        store.Save(id.PreferredKey, calibration);
        await WaitFor(() => output.Last.Gamepad.LeftThumbX == 0);
        Assert.Equal(0, output.Last.Gamepad.RightThumbX);
        Assert.Equal(-1000, output.Last.Gamepad.LeftThumbY);
        Assert.Equal(1000, session.LatestUncalibratedInput!.State.Gamepad.LeftThumbX);
        Assert.Equal(0, session.LatestInput!.State.Gamepad.LeftThumbX);

        // Clearing returns to the original values.
        store.Clear(id.PreferredKey);
        await WaitFor(() => output.Last.Gamepad.LeftThumbX == 1000);
    }

    [Fact]
    public async Task SessionLeavesAControllerWithoutIdentityOrCalibrationUntouched()
    {
        var store = new StickCalibrationStore(TempFile(), TempCache());
        store.Save(ControllerId.XInput, SampleSet(500));
        var output = new RecordingOutput();
        await using var session = new MappingSession(new MappingProfile(), new FakeIdentityInput(null, Pad(1000, 0, 0, 0)), output, calibrations: store);
        Assert.True(session.TryStart());
        await WaitFor(() => output.Last.Gamepad.LeftThumbX == 1000);
        await Task.Delay(50);
        Assert.Equal(1000, output.Last.Gamepad.LeftThumbX);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met.");
            await Task.Delay(5);
        }
    }

    private sealed class FakeIdentityInput(ControllerId? id, Gamepad pad) : IInputReader, IControllerIdentity
    {
        public ControllerId? Identity => id;
        public bool TryGetState(out State state) { state = new State { Gamepad = pad }; return true; }
        public void Dispose() { }
    }

    private sealed class RecordingOutput : IVirtualController
    {
        private State _last;
        public State Last { get { lock (this) return _last; } }
        public bool IsConnected { get; private set; }
        public string? LastError => null;
        public bool TryConnect(out string? error) { error = null; return IsConnected = true; }
        public event Action<byte, byte>? RumbleRequested { add { } remove { } }
        public void PushState(State state, double deadZone, MotionSample? motion = null) { lock (this) _last = state; }
        public void Disconnect() => IsConnected = false;
        public void Dispose() => Disconnect();
    }
}
