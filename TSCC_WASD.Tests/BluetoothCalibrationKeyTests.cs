using System.IO;
using TSCC_WASD.Core.Services.Calibration;
using TSCC_WASD.Core.Services.Input;
using Xunit;

namespace TSCC_WASD.Tests;

public class BluetoothCalibrationKeyTests
{
    private const string Address = "bcce25c5ff8e";
    private const string Old4 = @"hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&2009\c&6767fcd&4&0000";
    private const string Old7 = @"hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&2009\c&6767fcd&7&0000";
    private const string OtherOld = @"hid\{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&2009\c&1111111&4&0000";
    private const string Parent = @"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002057e_PID&2009\9&2a1b3c4d&0&BCCE25C5FF8E_C00000000";
    private const string OtherParent = @"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002057e_PID&2009\9&2a1b3c4d&0&001122334455_C00000000";

    private static string? Parents(string id) => id.ToLowerInvariant() switch
    {
        Old4 or Old7 => Parent,
        OtherOld => OtherParent,
        _ => null,
    };

    private static StickCalibrationSet Set(short rest) =>
        new(new(new(rest, 30000, 30000), new(0, 30000, 30000)), new(new(0, 30000, 30000), new(0, 30000, 30000)));

    private sealed class Dir : IDisposable
    {
        public readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TSCC_WASD-tests", "btkey-" + Guid.NewGuid().ToString("N"));
        public Dir() => Directory.CreateDirectory(Path);
        public string File => System.IO.Path.Combine(Path, "stick.json");
        public StickCalibrationStore Store(Func<string, string?>? parentOf = null) =>
            new(File, new Switch2CalibrationCache(System.IO.Path.Combine(Path, "ns2.json")), parentOf ?? Parents);
        public void Dispose() => TempDir.Delete(Path);
    }

    private static ControllerId Bt(string instance) => new(ControllerId.SwitchPro, Address, instance);

    [Fact]
    public void AddressIsParsedFromBthEnumParent()
    {
        Assert.Equal(Address, HidDevice.BluetoothAddress(Old4, Parents));
        Assert.Equal(Address, HidDevice.BluetoothAddress("child", id => id == "child" ? "mid" : id == "mid" ? Parent : null));
    }

    [Theory]
    [InlineData(@"USB\VID_057E&PID_2009\5&1&0&2")]
    [InlineData(@"BTHLEDEVICE\{0000fe59-0000-1000-8000-00805f9b34fb}_Dev_VID&02057e_PID&2009\7&1&0&bcce25c5ff8e")]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002057e_PID&2009\9&1&0&BCCE25C5FF_C00000000")]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0002057e_PID&2009\9&1&0&NOTHEXNOTHE_C00000000")]
    [InlineData(@"BTHENUM\DEV_BCCE25C5FF8E\9&1&0&BTHENUM")]
    public void AddressIsNullForUsbBleOrMalformed(string parent)
    {
        Assert.Null(HidDevice.BluetoothAddress("child", _ => parent));
    }

    [Fact]
    public void AddressIsNullForUnknownOrThrowingParent()
    {
        Assert.Null(HidDevice.BluetoothAddress("child", _ => null));
        Assert.Null(HidDevice.BluetoothAddress("child", _ => throw new InvalidOperationException()));
        Assert.Null(HidDevice.BluetoothAddress("child", _ => @"ROOT\SYSTEM\0001"));
    }

    [Fact]
    public void BluetoothIdentityUsesAddressAndKeepsInstanceAsLegacy()
    {
        var id = ControllerId.ForHid(ControllerId.SwitchPro, Old7, true, Parents);
        Assert.Equal("switchpro#" + Address, id.PreferredKey);
        Assert.Equal(new[] { "switchpro#" + Address, "switchpro#" + Old7, "switchpro" }, id.LookupKeys());
    }

    [Fact]
    public void BluetoothWithoutAddressFallsBackToInstanceId()
    {
        var id = ControllerId.ForHid(ControllerId.SwitchPro, Old7, true, _ => null);
        Assert.Equal("switchpro#" + Old7, id.PreferredKey);
        Assert.Null(id.LegacyDeviceKey);
        Assert.Equal(new[] { "switchpro#" + Old7, "switchpro" }, id.LookupKeys());
    }

    [Fact]
    public void UsbKeysAreUnchanged()
    {
        const string usb = @"hid\vid_057e&pid_2009\7&1&0&0000";
        var id = ControllerId.ForHid(ControllerId.SwitchPro, usb, false, _ => throw new InvalidOperationException("no lookup for USB"));
        Assert.Equal("switchpro#" + usb, id.PreferredKey);
        Assert.Equal(new[] { "switchpro#" + usb, "switchpro" }, id.LookupKeys());
        Assert.Equal(new[] { "xinput" }, new ControllerId(ControllerId.XInput).LookupKeys());
    }

    [Fact]
    public void OldFileStillAppliesWithoutAnyAddress()
    {
        using var dir = new Dir();
        Assert.True(dir.Store().Save("switchpro#" + Old4, Set(11)));
        var store = dir.Store(_ => null);
        var id = ControllerId.ForHid(ControllerId.SwitchPro, Old4, true, _ => null);
        Assert.False(store.AdoptLegacy(id));
        Assert.True(store.TryGet(id, out var found));
        Assert.Equal(11, found.Left.X.Center);
    }

    [Fact]
    public void LegacyKeyOfCurrentInstanceApplies()
    {
        using var dir = new Dir();
        var store = dir.Store();
        store.Save("switchpro#" + Old7, Set(12));
        Assert.True(store.TryGet(Bt(Old7), out var found));
        Assert.Equal(12, found.Left.X.Center);
    }

    [Fact]
    public void CalibrationSurvivesRepairingViaAdopt()
    {
        using var dir = new Dir();
        dir.Store().Save("switchpro#" + Old4, Set(21));
        var store = dir.Store();
        int version = store.Version;
        string before = File.ReadAllText(dir.File);
        Assert.False(store.TryGet(new ControllerId(ControllerId.SwitchPro, Address, Old7), out _)); // not yet adopted
        Assert.Equal(before, File.ReadAllText(dir.File)); // lookups never write

        Assert.True(store.AdoptLegacy(Bt(Old7)));
        Assert.True(store.Version > version);
        Assert.True(store.TryGet(Bt(Old7), out var adopted));
        Assert.Equal(21, adopted.Left.X.Center);

        var reloaded = dir.Store();
        Assert.True(reloaded.TryGet("switchpro#" + Address, out _));
        Assert.False(reloaded.TryGet("switchpro#" + Old4, out _));
        Assert.False(store.AdoptLegacy(Bt(Old7))); // nothing left to do
    }

    [Fact]
    public void TwoOldEntriesCollapseIntoOne()
    {
        using var dir = new Dir();
        var store = dir.Store();
        store.Save("switchpro#" + Old4, Set(31));
        store.Save("switchpro#" + Old7, Set(32));
        Assert.True(store.AdoptLegacy(Bt(Old7)));
        var reloaded = dir.Store();
        Assert.True(reloaded.TryGet("switchpro#" + Address, out var found));
        Assert.Equal(32, found.Left.X.Center); // the current instance wins
        Assert.False(reloaded.TryGet("switchpro#" + Old4, out _));
        Assert.False(reloaded.TryGet("switchpro#" + Old7, out _));
    }

    [Fact]
    public void ExistingAddressEntryIsKeptAndOldOnesRemoved()
    {
        using var dir = new Dir();
        var store = dir.Store();
        store.Save("switchpro#" + Old4, Set(41));
        store.Save("switchpro#" + Address, Set(42));
        Assert.True(store.AdoptLegacy(Bt(Old7)));
        Assert.True(store.TryGet("switchpro#" + Address, out var found));
        Assert.Equal(42, found.Left.X.Center);
        Assert.False(store.TryGet("switchpro#" + Old4, out _));
    }

    [Fact]
    public void OtherControllersOldEntryIsNotAdopted()
    {
        using var dir = new Dir();
        var store = dir.Store();
        store.Save("switchpro#" + OtherOld, Set(51));
        store.Save("switchpro", Set(52));
        Assert.False(store.AdoptLegacy(Bt(Old7)));
        Assert.True(store.TryGet("switchpro#" + OtherOld, out _));
        Assert.False(store.TryGet("switchpro#" + Address, out _));
        Assert.True(store.TryGet("switchpro", out _));
    }

    [Fact]
    public void SaveRemovesLegacyKeyAndClearRemovesBoth()
    {
        using var dir = new Dir();
        var store = dir.Store();
        store.Save("switchpro#" + Old7, Set(61));
        Assert.True(store.Save(Bt(Old7), Set(62)));
        Assert.False(store.TryGet("switchpro#" + Old7, out _));
        Assert.True(store.TryGet("switchpro#" + Address, out _));

        store.Save("switchpro#" + Old7, Set(63));
        Assert.True(store.Clear(["switchpro#" + Address, "switchpro#" + Old7]));
        Assert.False(store.TryGet("switchpro#" + Address, out _));
        Assert.False(store.TryGet("switchpro#" + Old7, out _));
    }

    [Fact]
    public void WizardClearRemovesAddressAndLegacyKeys()
    {
        using var kit = new CalibrationKit();
        kit.Store.Save("switchpro#" + Address, Set(71));
        kit.Store.Save("switchpro#" + Old7, Set(72));
        kit.Store.Save("switchpro", Set(73));
        using var model = kit.Model(Bt(Old7));
        Assert.True(model.Clear());
        Assert.False(kit.Store.TryGet("switchpro#" + Address, out _));
        Assert.False(kit.Store.TryGet("switchpro#" + Old7, out _));
        Assert.True(kit.Store.TryGet("switchpro", out _)); // model entry stays
    }

    [Fact]
    public void WizardSaveUsesAddressKeyAndDropsLegacy()
    {
        using var kit = new CalibrationKit();
        kit.Store.Save("switchpro#" + Old7, Set(81));
        using var model = kit.Model(Bt(Old7));
        Assert.True(kit.RunToSave(model));
        Assert.True(kit.Store.TryGet("switchpro#" + Address, out _));
        Assert.False(kit.Store.TryGet("switchpro#" + Old7, out _));
    }
}
