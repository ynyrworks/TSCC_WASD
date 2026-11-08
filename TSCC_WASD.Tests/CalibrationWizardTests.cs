using System.IO;
using TSCC_WASD.App;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Services.Calibration;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;
using Xunit;

namespace TSCC_WASD.Tests;

/// <summary>Temp store, temp NS2 cache and a fake input source for the wizard; nothing touches real folders or controllers.</summary>
internal sealed class CalibrationKit : IDisposable
{
    public readonly string Dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", "calib-" + Guid.NewGuid().ToString("N"));
    public readonly Switch2CalibrationCache Ns2;
    public readonly StickCalibrationStore Store;
    public Gamepad Pad;
    public bool Available = true;
    public long NowMs;

    public CalibrationKit()
    {
        Directory.CreateDirectory(Dir);
        Ns2 = new Switch2CalibrationCache(Path.Combine(Dir, "ns2.json"));
        Store = new StickCalibrationStore(Path.Combine(Dir, "stick.json"), Ns2);
    }

    public CalibrationWizardModel Model(ControllerId id, string name = "Test pad") =>
        new(id, name, () => new MappingSession.InputSnapshot(false, Available, new State { Gamepad = Pad }),
            Store, Ns2, () => NowMs);

    public void Dispose() => TempDir.Delete(Dir);

    /// <summary>A wizard window on a fake source in a temp folder, for layout tests.</summary>
    public static CalibrationWindow Window() =>
        new(new CalibrationKit().Model(new ControllerId(ControllerId.XInput), "Xbox controller"));

    public static Gamepad Stick(int lx, int ly, int rx, int ry) =>
        new() { LeftThumbX = (short)lx, LeftThumbY = (short)ly, RightThumbX = (short)rx, RightThumbY = (short)ry };

    /// <summary>Runs the whole wizard to the Save step: rest, then every direction pushed to 20000.</summary>
    public bool RunToSave(CalibrationWizardModel model)
    {
        Pad = Stick(100, -50, 0, 0);
        model.Tick();
        Assert.True(model.CanAdvance);
        model.Advance();
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++) model.Tick();
        Assert.Equal(CalibrationWizardModel.Step.Range, model.State);
        foreach (var pad in new[] { Stick(20100, 0, 20000, 0), Stick(-20000, 0, -20000, 0), Stick(0, 20000, 0, 20000), Stick(0, -20000, 0, -20000) })
        {
            Pad = pad;
            model.Tick();
        }
        Assert.True(model.CanAdvance);
        return model.Advance();
    }

    public static Switch2StickCalibration Ns2User => new(new(2000, 1500, 1600), new(2100, 1400, 1500));
}

public class CalibrationWizardTests
{
    private static readonly ControllerId Switch = new(ControllerId.SwitchPro, "AA:BB");

    [Fact]
    public void FullFlowSavesForTheRightIdentity()
    {
        using var kit = new CalibrationKit();
        using var model = kit.Model(Switch, "Switch Pro");
        int version = kit.Store.Version;
        Assert.True(kit.RunToSave(model));
        Assert.Equal(CalibrationWizardModel.Step.Saved, model.State);
        Assert.True(kit.Store.Version > version);
        Assert.True(kit.Store.TryGet("switchpro#aa:bb", out var saved));
        Assert.Equal(100, saved.Left.X.Center);
        Assert.Equal(-50, saved.Left.Y.Center);
        Assert.False(kit.Store.TryGet(new ControllerId(ControllerId.SwitchPro, "CC:DD"), out _));
        Assert.False(kit.Store.TryGet(new ControllerId(ControllerId.XInput), out _));
    }

    [Fact]
    public void ControllerWithoutDeviceIdSavesPerModel()
    {
        using var kit = new CalibrationKit();
        using var model = kit.Model(new ControllerId(ControllerId.XInput), "Xbox");
        Assert.True(kit.RunToSave(model));
        Assert.True(kit.Store.TryGet("xinput", out _));
    }

    [Fact]
    public void NothingAdvancesWithoutInput()
    {
        using var kit = new CalibrationKit { Available = false };
        using var model = kit.Model(Switch);
        model.Tick();
        Assert.False(model.InputAvailable);
        Assert.False(model.CanAdvance);
        Assert.False(model.Advance());
    }

    [Fact]
    public void CancelRestoresAnOldNs2UserCalibration()
    {
        using var kit = new CalibrationKit();
        kit.Ns2.SaveUser(CalibrationKit.Ns2User, CalibrationKit.Ns2User);
        var model = kit.Model(new ControllerId(ControllerId.Ns2Pro, "serial1"), "NS2 Pro");
        Assert.False(kit.Ns2.TryLoadUser(out _, out _)); // BeginCalibration cleared it so samples are factory-calibrated
        Assert.True(model.HasStoredCalibration); // still the one to clear or restore
        model.Dispose();
        Assert.True(kit.Ns2.TryLoadUser(out var left, out var right));
        Assert.Equal(CalibrationKit.Ns2User, left);
        Assert.Equal(CalibrationKit.Ns2User, right);
        model.Dispose(); // closing twice changes nothing
        Assert.True(kit.Ns2.TryLoadUser(out _, out _));
    }

    [Fact]
    public void CancelAfterCenterStepKeepsAPreviousGenericCalibration()
    {
        using var kit = new CalibrationKit();
        var previous = new StickCalibrationSet(new(new(5, 30000, 31000), new(6, 29000, 28000)), new(new(7, 27000, 26000), new(8, 25000, 24000)));
        Assert.True(kit.Store.Save(Switch.PreferredKey, previous));
        var model = kit.Model(Switch);
        kit.Pad = CalibrationKit.Stick(0, 0, 0, 0);
        model.Tick();
        model.Advance();
        for (int i = 0; i < 60; i++) model.Tick();
        model.Dispose();
        Assert.True(kit.Store.TryGet(Switch.PreferredKey, out var after));
        Assert.Equal(previous, after);
    }

    [Fact]
    public void SavingAnNs2CalibrationDoesNotBringTheOldOneBack()
    {
        using var kit = new CalibrationKit();
        kit.Ns2.SaveUser(CalibrationKit.Ns2User, CalibrationKit.Ns2User);
        var model = kit.Model(new ControllerId(ControllerId.Ns2Pro, "s"), "NS2 Pro");
        Assert.True(kit.RunToSave(model));
        model.Dispose();
        Assert.False(kit.Ns2.TryLoadUser(out _, out _));
        Assert.True(kit.Store.TryGet("ns2pro#s", out _));
    }

    [Fact]
    public void ClearRemovesOnlyTheCurrentControllersEntry()
    {
        using var kit = new CalibrationKit();
        var set = new StickCalibrationSet(new(new(0, 30000, 30000), new(0, 30000, 30000)), new(new(0, 30000, 30000), new(0, 30000, 30000)));
        var other = new ControllerId(ControllerId.SwitchPro, "CC:DD");
        var xbox = new ControllerId(ControllerId.XInput);
        kit.Store.Save(Switch.PreferredKey, set);
        kit.Store.Save(other.PreferredKey, set);
        kit.Store.Save(xbox.PreferredKey, set);
        using var model = kit.Model(Switch);
        Assert.True(model.HasStoredCalibration);
        int version = kit.Store.Version;
        Assert.True(model.Clear());
        Assert.True(kit.Store.Version > version); // a running session picks it up
        Assert.False(kit.Store.TryGet(Switch, out _));
        Assert.True(kit.Store.TryGet(other, out _));
        Assert.True(kit.Store.TryGet(xbox, out _));
        Assert.False(model.Clear());
    }

    [Fact]
    public void ClearKeepsTheModelEntryWhenTheControllerHasItsOwnDeviceEntry()
    {
        using var kit = new CalibrationKit();
        var set = new StickCalibrationSet(new(new(0, 30000, 30000), new(0, 30000, 30000)), new(new(0, 30000, 30000), new(0, 30000, 30000)));
        var otherPad = new ControllerId(ControllerId.SwitchPro, "CC:DD");
        kit.Store.Save("switchpro", set);
        kit.Store.Save(Switch.PreferredKey, set);
        using (var model = kit.Model(Switch))
            Assert.True(model.Clear());
        Assert.False(kit.Store.TryGet(Switch.PreferredKey, out _));
        Assert.True(kit.Store.TryGet("switchpro", out _));
        Assert.True(kit.Store.TryGet(otherPad, out _)); // falls back to the model entry
        // A controller with no device entry clears the model entry.
        using (var model = kit.Model(otherPad))
            Assert.True(model.Clear());
        Assert.False(kit.Store.TryGet("switchpro", out _));
    }

    [Fact]
    public void ClearThenCancelDoesNotRestoreAnNs2UserCalibration()
    {
        using var kit = new CalibrationKit();
        kit.Ns2.SaveUser(CalibrationKit.Ns2User, CalibrationKit.Ns2User);
        var model = kit.Model(new ControllerId(ControllerId.Ns2Pro, "s"), "NS2 Pro");
        Assert.True(model.Clear());
        model.Dispose();
        Assert.False(kit.Ns2.TryLoadUser(out _, out _));
    }

    [Fact]
    public async Task EntryPointsAreDisabledWhileNotMapping()
    {
        var vm = new MainViewModel(hidHideFactory: () => null);
        try
        {
            Assert.False(vm.IsRunning);
            Assert.False(vm.CalibrateCommand.CanExecute(null));
        }
        finally { await vm.DisposeAsync(); }
    }

    [Fact]
    public void ModelNamesAreNotEmptyForEveryController()
    {
        foreach (var model in new[] { ControllerId.Ns2Pro, ControllerId.DualShock4, ControllerId.DualSense, ControllerId.SwitchPro, ControllerId.XInput })
            Assert.False(string.IsNullOrWhiteSpace(CalibrationWindow.ModelName(new ControllerId(model))));
    }

    private static CalibrationWizardModel StartMeasuring(CalibrationKit kit, Gamepad rest)
    {
        var model = kit.Model(new ControllerId(ControllerId.SwitchPro));
        kit.Pad = rest;
        model.Tick();
        model.Advance();
        return model;
    }

    [Fact]
    public void Identical_readings_at_a_plausible_center_complete_and_save()
    {
        using var kit = new CalibrationKit();
        var id = new ControllerId(ControllerId.DualSense);
        var model = kit.Model(id);
        kit.Pad = CalibrationKit.Stick(-1600, -3800, 0, 0);
        model.Tick();
        model.Advance();
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++) model.Tick();
        Assert.Equal(CalibrationWizardModel.Step.Range, model.State);
        foreach (var pad in new[] { CalibrationKit.Stick(18000, 0, 20000, 0), CalibrationKit.Stick(-20000, 0, -20000, 0),
            CalibrationKit.Stick(0, 20000, 0, 20000), CalibrationKit.Stick(0, -20000, 0, -20000) })
        {
            kit.Pad = pad;
            model.Tick();
        }
        Assert.True(model.Advance());
        Assert.True(kit.Store.TryGet(id, out var saved));
        Assert.Equal(-1600, saved.Left.X.Center);
        Assert.Equal(-3800, saved.Left.Y.Center);
    }

    [Fact]
    public void Unavailable_for_two_seconds_shows_no_data_and_adds_no_samples()
    {
        using var kit = new CalibrationKit();
        var model = StartMeasuring(kit, CalibrationKit.Stick(0, 0, 0, 0));
        kit.Available = false;
        kit.NowMs = 1000;
        for (int i = 0; i < 100; i++) model.Tick();
        Assert.False(model.NoReports);
        kit.NowMs = 2500;
        for (int i = 0; i < 100; i++) model.Tick();
        Assert.True(model.NoReports);
        Assert.Equal(CalibrationWizardModel.Step.MeasuringCenter, model.State);
        kit.Available = true;
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded - 1; i++) model.Tick();
        Assert.Equal(CalibrationWizardModel.Step.MeasuringCenter, model.State);
        Assert.False(model.NoReports);
        model.Tick();
        Assert.Equal(CalibrationWizardModel.Step.Range, model.State);
    }

    [Fact]
    public void Noisy_center_samples_restart_the_step()
    {
        using var kit = new CalibrationKit();
        var model = StartMeasuring(kit, default);
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++)
        {
            kit.Pad = CalibrationKit.Stick(i % 2 == 0 ? -2000 : 2000, 0, 0, 0);
            model.Tick();
        }
        Assert.Equal(CalibrationWizardModel.Step.MeasuringCenter, model.State);
        Assert.Equal(CenterCheck.Unsteady, model.CenterProblem);
    }

    [Fact]
    public void Off_center_rest_is_refused_and_nothing_is_saved()
    {
        using var kit = new CalibrationKit();
        var id = new ControllerId(ControllerId.SwitchPro);
        var model = StartMeasuring(kit, CalibrationKit.Stick(-9876, -16901, 0, 0));
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++) model.Tick();
        Assert.Equal(CalibrationWizardModel.Step.MeasuringCenter, model.State);
        Assert.Equal(CenterCheck.OffCenter, model.CenterProblem);
        Assert.False(kit.Store.TryGet(id, out _));
    }

    [Fact]
    public void Normal_rest_with_small_noise_succeeds_and_saves()
    {
        using var kit = new CalibrationKit();
        var id = new ControllerId(ControllerId.SwitchPro);
        var model = StartMeasuring(kit, default);
        for (int i = 0; i < GenericStickCalibrator.CenterSamplesNeeded; i++)
        {
            kit.Pad = CalibrationKit.Stick(-1600 + i % 5 - 2, -3800 + i % 7 - 3, 10, -10);
            model.Tick();
        }
        Assert.Equal(CalibrationWizardModel.Step.Range, model.State);
        Assert.Equal(CenterCheck.Ok, model.CenterProblem);
        foreach (var pad in new[] { CalibrationKit.Stick(18000, 0, 20000, 0), CalibrationKit.Stick(-20000, 0, -20000, 0),
            CalibrationKit.Stick(0, 20000, 0, 20000), CalibrationKit.Stick(0, -20000, 0, -20000) })
        {
            kit.Pad = pad;
            model.Tick();
        }
        Assert.True(model.Advance());
        Assert.True(kit.Store.TryGet(id, out var saved));
        Assert.InRange(saved.Left.X.Center, -1605, -1595);
        Assert.InRange(saved.Left.Y.Center, -3805, -3795);
    }
}
