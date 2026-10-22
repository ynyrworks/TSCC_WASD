using System.IO;
using Xunit;
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

    private static string TempRecord() =>
        Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "hidhide-restore.json");

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
