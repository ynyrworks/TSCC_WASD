using System.IO;
using Xunit;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Setup;

namespace TSCC_WASD.Tests;

public class HidHideGuardTests
{
    private const string Ns2 = @"HID\VID_057E&PID_2069&MI_00\a&37d9c433&0&0000";
    private const string Xbox = @"HID\{00001812-0000-1000-8000-00805f9b34fb}&Dev&VID_045e&PID_0b13&REV_0509&686ce65b1cfd&IG_00\d&23b31df3&0&0000";
    // Not a controller TSCC_WASD reads (a Logitech receiver), so it must never be hidden.
    private const string Other = @"HID\VID_046D&PID_C52B&MI_02\8&1b721325&0&0000";
    private const string App = @"C:\Apps\TSCC_WASD.exe";

    private static string Gaming(params string[] paths) =>
        "[" + string.Join(",", paths.Select(p =>
            "{\"friendlyName\":\"x\",\"devices\":[{\"present\":true,\"deviceInstancePath\":\"" + p.Replace(@"\", @"\\") + "\"}]}")) + "]";

    [Fact]
    public void HidesMatchingControllersThenRestoresOnlyItsOwnChanges()
    {
        var cli = new FakeCli { Gaming = Gaming(Ns2, Xbox, Other), Hidden = [Xbox] };
        var guard = new HidHideGuard(cli.Run, TempRecord());

        string message = guard.Hide(App, InputDeviceType.Auto);
        Assert.Contains("1", message);
        Assert.True(guard.HasPendingRestore);
        Assert.Equal(["--app-reg", App, "--dev-hide", Ns2, "--cloak-on"], cli.Changes.Single());

        // A rescan with nothing new does not issue changes.
        guard.Hide(App, InputDeviceType.Auto);
        Assert.Single(cli.Changes);

        guard.Restore();
        Assert.Equal(["--dev-unhide", Ns2, "--app-unreg", App, "--cloak-off"], cli.Changes.Last());
        Assert.False(guard.HasPendingRestore);
    }

    [Fact]
    public void KeepsUserSettingsThatWereAlreadyPresent()
    {
        var cli = new FakeCli { Gaming = Gaming(Ns2, Xbox), CloakOn = true, Apps = [App] };
        var guard = new HidHideGuard(cli.Run, TempRecord());
        guard.Hide(App, InputDeviceType.XInput);
        Assert.Equal(["--dev-hide", Xbox, "--cloak-on"], cli.Changes.Single());
        guard.Restore();
        Assert.Equal(["--dev-unhide", Xbox], cli.Changes.Last());
    }

    [Fact]
    public void RecordSurvivesRestartForCrashRecovery()
    {
        string record = TempRecord();
        var cli = new FakeCli { Gaming = Gaming(Ns2) };
        new HidHideGuard(cli.Run, record).Hide(App, InputDeviceType.Switch2ProUsb);
        var afterRestart = new HidHideGuard(cli.Run, record);
        Assert.True(afterRestart.HasPendingRestore);
        afterRestart.Restore();
        Assert.Equal(["--dev-unhide", Ns2, "--app-unreg", App, "--cloak-off"], cli.Changes.Last());
    }

    [Fact]
    public void AlsoHidesWiredXboxDevicesSuppliedOutsideDevGaming()
    {
        const string wired = @"USB\VID_045E&PID_0B12\3032363030333130";
        var cli = new FakeCli { Gaming = Gaming(Ns2) };
        var guard = new HidHideGuard(cli.Run, TempRecord(), () => [wired, Ns2]);
        guard.Hide(App, InputDeviceType.XInput);
        Assert.Equal(["--app-reg", App, "--dev-hide", wired, "--cloak-on"], cli.Changes.Single());
    }

    [Fact]
    public void TruncatedCliDeviceListStillHidesEnumeratedControllers()
    {
        // HidHideCLI stops mid-JSON at the first non-ASCII description (e.g. "XINPUT 相容 HID 裝置").
        // That used to throw and leave every controller visible.
        const string truncated = "[ { \"friendlyName\" : \"x\" , \"devices\" : [\n{ \"present\" : true ,\n\"description\" : \"XINPUT ";
        var cli = new FakeCli { Gaming = truncated };
        var guard = new HidHideGuard(cli.Run, TempRecord(), () => [Ns2, Xbox]);
        Assert.Contains("2", guard.Hide(App, InputDeviceType.Auto));
        Assert.Equal(["--app-reg", App, "--dev-hide", Ns2, "--dev-hide", Xbox, "--cloak-on"], cli.Changes.Single());
    }

    [Fact]
    public void NeverHidesItsOwnVirtualOutput()
    {
        // --dev-gaming lists the ViGEm DS4 / Xbox 360 pads too; hiding them left games with no controller.
        const string virtualDs4 = @"HID\VID_054C&PID_05C4\7&2b3e4f5a&0&0000";
        const string virtualX360 = @"USB\VID_045E&PID_028E\1&2d595ca7&0&01";
        const string realDs4 = @"HID\VID_054C&PID_09CC\7&11aa22bb&0&0000";
        var cli = new FakeCli { Gaming = Gaming(Ns2, virtualDs4, virtualX360, realDs4) };
        var guard = new HidHideGuard(cli.Run, TempRecord(), isVirtual: path => path is virtualDs4 or virtualX360);
        guard.Hide(App, InputDeviceType.Auto);
        Assert.Equal(["--app-reg", App, "--dev-hide", Ns2, "--dev-hide", realDs4, "--cloak-on"], cli.Changes.Single());
    }

    [Fact]
    public void InverseModeIsLeftAlone()
    {
        var cli = new FakeCli { Gaming = Gaming(Ns2), Inverse = true };
        var guard = new HidHideGuard(cli.Run, TempRecord());
        Assert.Contains(TSCC_WASD.Core.L.T("反向", "inverse"), guard.Hide(App, InputDeviceType.Auto));
        Assert.Empty(cli.Changes);
        Assert.False(guard.HasPendingRestore);
    }

    [Fact]
    public void FailedSaveKeepsOldRecordAndLeavesNoTempFile()
    {
        string record = TempRecord();
        var cli = new FakeCli { Gaming = Gaming(Ns2, Xbox) };
        var guard = new HidHideGuard(cli.Run, record);
        guard.Hide(App, InputDeviceType.Switch2ProUsb);
        string before = File.ReadAllText(record);

        // Holding the record open makes the atomic replace fail after the temp file was written.
        using (new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => guard.Hide(App, InputDeviceType.XInput));

        Assert.Equal(before, File.ReadAllText(record));
        Assert.Equal([record], Directory.GetFiles(Path.GetDirectoryName(record)!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"AppPath\":\"C:\\\\Apps\\\\TS")]
    [InlineData("null")]
    public void DamagedRecordIsQuarantinedAndRestoreUnhidesMatchingDevices(string content)
    {
        using var log = TempLog();
        string record = TempRecord();
        Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        File.WriteAllText(record, content);
        var cli = new FakeCli { Hidden = [Ns2, Other] };
        var guard = new HidHideGuard(cli.Run, record);

        guard.Restore();

        Assert.False(guard.HasPendingRestore);
        Assert.Equal(content, File.ReadAllText(record + ".corrupt"));
        // Only matching controllers are unhidden; cloak and the application list are untouched.
        Assert.Equal(["--dev-unhide", Ns2], cli.Changes.Single());
        Assert.False(string.IsNullOrEmpty(guard.RecoveryNotice));
        Assert.Contains("damaged", File.ReadAllText(AppLog.CurrentFile));
    }

    [Fact]
    public void DamagedRecordDoesNotBlockHideAndRepeatedDamageKeepsEveryCopy()
    {
        using var log = TempLog();
        string record = TempRecord();
        Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        File.WriteAllText(record, "first");
        var cli = new FakeCli { Gaming = Gaming(Ns2), Hidden = [Xbox] };
        var guard = new HidHideGuard(cli.Run, record);

        guard.Hide(App, InputDeviceType.Switch2ProUsb);
        Assert.True(guard.HasPendingRestore);
        Assert.NotNull(guard.RecoveryNotice);

        File.WriteAllText(record, "second");
        guard.Restore();

        Assert.Equal("first", File.ReadAllText(record + ".corrupt"));
        Assert.Equal("second", File.ReadAllText(record + ".corrupt1"));
    }

    private static IDisposable TempLog()
    {
        string old = AppLog.Directory;
        AppLog.Directory = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        return new Restorer(() => AppLog.Directory = old);
    }

    private sealed class Restorer(Action undo) : IDisposable { public void Dispose() => undo(); }

    private static string TempRecord() =>
        Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "hidhide-restore.json");

    private const string ThirdPartyUsb = @"USB\VID_2DC8&PID_3106\5&1&0&3";
    private const string ThirdPartyHid = @"HID\{00001812-0000-1000-8000-00805f9b34fb}&Dev&VID_2dc8&PID_6101&REV_0100&aabbccddeeff&IG_00\d&1&0&0000";
    private const string PlainJoystick = @"HID\VID_046D&PID_C21D\7&1&0&0000";

    [Fact]
    public void ThirdPartyXInputRuleMatchesClassOrIgMarkerOnly()
    {
        Func<string, bool> physical = _ => false;
        Assert.True(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(ThirdPartyUsb, true, true, physical));
        Assert.True(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(ThirdPartyHid, false, true, physical));
        Assert.False(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(PlainJoystick, false, true, physical));
        Assert.False(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(ThirdPartyUsb, true, true, _ => true));
        Assert.False(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(ThirdPartyHid, false, true, _ => true));
        Assert.False(TSCC_WASD.Core.Services.Input.WindowsDevicePaths.IsThirdPartyXInput(ThirdPartyUsb, true, false, physical));
    }

    [Theory]
    [InlineData(InputDeviceType.Auto, true)]
    [InlineData(InputDeviceType.XInput, true)]
    [InlineData(InputDeviceType.PlayStation, false)]
    [InlineData(InputDeviceType.SwitchPro, false)]
    [InlineData(InputDeviceType.Switch2ProUsb, false)]
    [InlineData(InputDeviceType.Switch2ProBluetooth, false)]
    public void OnlyAutoAndXInputAllowThirdPartyXInput(InputDeviceType type, bool expected) =>
        Assert.Equal(expected, HidHideGuard.AllowsThirdPartyXInput(type));

    [Fact]
    public void HidesThirdPartyOnlyWhenEnabledAndTypeAllows()
    {
        string[] Hide(Func<IEnumerable<string>>? third, InputDeviceType type, string[]? hidden = null)
        {
            var cli = new FakeCli { Gaming = Gaming(), Hidden = hidden ?? [] };
            var guard = new HidHideGuard(cli.Run, TempRecord(), isVirtual: p => p == "VIRTUAL", thirdPartyDevices: third);
            guard.Hide(App, type);
            return cli.Changes.Count == 0 ? [] : cli.Changes[0];
        }
        Func<IEnumerable<string>> list = () => [ThirdPartyUsb, "VIRTUAL"];
        Assert.Equal(["--app-reg", App, "--dev-hide", ThirdPartyUsb, "--cloak-on"], Hide(list, InputDeviceType.Auto));
        Assert.DoesNotContain("--dev-hide", Hide(null, InputDeviceType.Auto));
        Assert.DoesNotContain("--dev-hide", Hide(list, InputDeviceType.PlayStation));
    }

    private const string Ns2Interface = @"\\?\HID#VID_057E&PID_2069&MI_00#a&37d9c433&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    [Fact]
    public void UnhidesOneOwnDeviceForTheOpenAndHidesItAgain()
    {
        var cli = new FakeCli { Gaming = Gaming(Ns2) };
        string record = TempRecord();
        var guard = new HidHideGuard(cli.Run, record);
        guard.Hide(App, InputDeviceType.Switch2ProUsb);
        cli.Changes.Clear();

        Assert.True(guard.IsHiddenByUs(Ns2Interface.ToLowerInvariant()));
        guard.ReRegisterApp();
        Assert.Equal(["--app-reg", App], cli.Changes.Single());
        cli.Changes.Clear();

        var handles = guard.OpenWhileUnhidden(Ns2Interface, () => (Read: new Microsoft.Win32.SafeHandles.SafeFileHandle(), Write: new Microsoft.Win32.SafeHandles.SafeFileHandle()));
        Assert.NotNull(handles.Read);
        Assert.NotNull(handles.Write);
        Assert.Equal([["--dev-unhide", Ns2], ["--dev-hide", Ns2]], cli.Changes);
        Assert.True(guard.HasPendingRestore);
        guard.Restore();
        Assert.Equal(["--dev-unhide", Ns2, "--app-unreg", App, "--cloak-off"], cli.Changes.Last());
    }

    [Fact]
    public void HidesAgainWhenTheOpenThrows()
    {
        var cli = new FakeCli { Gaming = Gaming(Ns2) };
        var guard = new HidHideGuard(cli.Run, TempRecord());
        guard.Hide(App, InputDeviceType.Switch2ProUsb);
        cli.Changes.Clear();

        Assert.Throws<IOException>(() => guard.OpenWhileUnhidden<int>(Ns2Interface, () => throw new IOException("denied")));
        Assert.Equal([["--dev-unhide", Ns2], ["--dev-hide", Ns2]], cli.Changes);
        // Still in the record, so a later Restore unhides it.
        guard.Restore();
        Assert.Equal("--dev-unhide", cli.Changes.Last()[0]);
        Assert.Contains(Ns2, cli.Changes.Last());
    }

    [Fact]
    public void RefusesToUnhideADeviceNotInTheRecord()
    {
        const string otherInterface = @"\\?\HID#VID_046D&PID_C52B&MI_02#8&1b721325&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        var cli = new FakeCli { Gaming = Gaming(Ns2), Hidden = [Other] };
        var guard = new HidHideGuard(cli.Run, TempRecord());
        guard.Hide(App, InputDeviceType.Switch2ProUsb);
        cli.Changes.Clear();
        bool opened = false;

        Assert.False(guard.IsHiddenByUs(otherInterface));
        Assert.Throws<InvalidOperationException>(() => guard.OpenWhileUnhidden(otherInterface, () => { opened = true; return new Microsoft.Win32.SafeHandles.SafeFileHandle(); }));
        Assert.False(opened);
        Assert.Empty(cli.Changes);
    }

    [Fact]
    public void NothingToDoWithoutARecord()
    {
        var cli = new FakeCli();
        var guard = new HidHideGuard(cli.Run, TempRecord());
        Assert.False(guard.IsHiddenByUs(Ns2Interface));
        guard.ReRegisterApp();
        Assert.Empty(cli.Changes);
        Assert.Throws<InvalidOperationException>(() => guard.OpenWhileUnhidden(Ns2Interface, () => new Microsoft.Win32.SafeHandles.SafeFileHandle()));
    }

    private sealed class FakeCli
    {
        public string Gaming { get; init; } = "[]";
        public bool CloakOn { get; init; }
        public bool Inverse { get; init; }
        public string[] Hidden { get; init; } = [];
        public string[] Apps { get; init; } = [];
        public List<string[]> Changes { get; } = new();

        public string Run(IReadOnlyList<string> args) => args[0] switch
        {
            "--inv-state" => Inverse ? "--inv-on" : "--inv-off",
            "--cloak-state" => CloakOn ? "--cloak-on" : "--cloak-off",
            "--app-list" => string.Join("\n", Apps.Select(a => $"--app-reg \"{a}\"")),
            "--dev-list" => string.Join("\n", Hidden.Select(d => $"--dev-hide \"{d}\"")),
            "--dev-gaming" => Gaming,
            _ => Record(args)
        };

        private string Record(IReadOnlyList<string> args) { Changes.Add(args.ToArray()); return ""; }
    }
}
