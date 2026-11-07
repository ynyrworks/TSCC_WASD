using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Xunit;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services;
using TSCC_WASD.Core.Services.Setup;

namespace TSCC_WASD.Tests;

[Collection(AppLogCollection.Name)]
public class SetupTests
{
    [Fact]
    public void DriverManifestPinsOfficialHttpsInstallers()
    {
        Assert.True(DriverInstaller.ViGEmBus.Required);
        Assert.False(DriverInstaller.HidHide.Required);
        foreach (var package in DriverInstaller.Packages)
        {
            Assert.StartsWith("https://github.com/nefarius/", package.Url);
            Assert.EndsWith("/" + package.FileName, package.Url);
            Assert.Matches("^[0-9a-f]{64}$", package.Sha256);
            Assert.Contains(package.Version, package.FileName);
        }
    }

    [Fact]
    public void InstallerHashMustMatchExactly()
    {
        string path = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N") + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "installer");
        try
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("installer")));
            Assert.True(DriverInstaller.HasExpectedHash(path, hash.ToLowerInvariant()));
            Assert.True(DriverInstaller.HasExpectedHash(path, hash));
            Assert.False(DriverInstaller.HasExpectedHash(path, new string('0', 64)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void HidHideOfferIsOnByDefault()
    {
        Assert.True(new AppSettings().OfferHidHideInstall);
    }

    [Fact]
    public void DiagnosticReportNamesVersionAndDrivers()
    {
        string report = DiagnosticReport.Build("NS2 Pro: idle", mapping: false);
        Assert.Contains($"TSCC_WASD {UpdateChecker.CurrentVersion.ToString(3)}", report);
        Assert.Contains("ViGEmBus:", report);
        Assert.Contains("HidHide:", report);
        Assert.Contains("Input: NS2 Pro: idle", report);
        Assert.DoesNotContain(Environment.UserName + "\\", report);
    }

    [Fact]
    public void LogAppendsAndKeepsTheNewestSevenFiles()
    {
        string original = AppLog.Directory;
        string dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
        AppLog.Directory = dir;
        try
        {
            AppLog.Info("first");
            AppLog.Error("second", new InvalidOperationException("boom"));
            string text = LogFile.ReadAllText(AppLog.CurrentFile);
            Assert.Contains("INFO first", text);
            Assert.Contains("ERROR second", text);
            Assert.Contains("boom", text);

            for (int day = 1; day <= 9; day++) File.WriteAllText(Path.Combine(dir, $"TSCC_WASD-2020010{day}.log"), "");
            AppLog.Prune();
            var kept = Directory.GetFiles(dir, "TSCC_WASD-*.log").Select(Path.GetFileName).ToList();
            Assert.Equal(7, kept.Count);
            Assert.Contains(Path.GetFileName(AppLog.CurrentFile), kept);
            Assert.DoesNotContain("TSCC_WASD-20200101.log", kept);
        }
        finally
        {
            AppLog.Directory = original;
            TempDir.Delete(dir);
        }
    }

    [Fact]
    public void PortCycleTargetsTheDeviceOnTheHubPort()
    {
        // NS2 Pro over USB: HID interface -> USB interface -> composite device -> hub.
        DeviceNode[] chain =
        [
            new(@"HID\VID_057E&PID_2069&MI_00\8&1", "HidUsb"),
            new(@"USB\VID_057E&PID_2069&MI_00\7&2", "HidUsb"),
            new(@"USB\VID_057E&PID_2069\SERIAL", "usbccgp"),
            new(@"USB\ROOT_HUB30\5&3", "USBHUB3"),
            new(@"PCI\VEN_8086&DEV_A36D\3&4", "USBXHCI"),
        ];
        Assert.Equal(2, UsbPortCycler.HubChildIndex(chain));
        Assert.False(UsbPortCycler.IsBluetooth(chain));

        DeviceNode[] bluetooth =
        [
            new(@"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002045E_PID&0B13\9&1", "HidBth"),
            new(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002045E_PID&0B13\8&2", "HidBth"),
            new(@"USB\VID_8087&PID_0026\5&3", "BTHUSB"),
            new(@"USB\ROOT_HUB30\4&4", "USBHUB3"),
        ];
        Assert.True(UsbPortCycler.IsBluetooth(bluetooth));
        Assert.Null(UsbPortCycler.HubChildIndex([new(@"ROOT\SYSTEM\0001", "ViGEmBus")]));
    }

    [Fact]
    public void ReconnectTaskRunsOnDemandWithFixedArgumentsAsTheUser()
    {
        string xml = ControllerReconnector.TaskXml(@"C:\Program Files\TSCC_WASD\Helper\TSCC_WASD.exe", @"PC\Name & <Co>");
        var doc = XDocument.Parse(xml);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(@"PC\Name & <Co>", doc.Descendants(ns + "UserId").Single().Value);
        Assert.Equal("HighestAvailable", doc.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("InteractiveToken", doc.Descendants(ns + "LogonType").Single().Value);
        Assert.Equal(ControllerReconnector.ReconnectArgument, doc.Descendants(ns + "Arguments").Single().Value);
        Assert.Empty(doc.Descendants(ns + "Triggers")); // Only runs when TSCC_WASD asks for it.
    }

    [Fact]
    public void UnknownCommandLinesAreLeftToTheNormalApp()
    {
        Assert.Null(ControllerReconnector.TryRunCommand([]));
        Assert.Null(ControllerReconnector.TryRunCommand(["--startup"]));
        Assert.Null(ControllerReconnector.TryRunCommand([ControllerReconnector.InstallArgument])); // Needs the user name.
    }

    [Fact]
    public void AntiCheatWarningOnlyForKnownBlockers()
    {
        Assert.Null(AntiCheatWatcher.FindRunning(_ => false));
        var ea = AntiCheatWatcher.FindRunning(name => name == "EAAntiCheat.GameService");
        Assert.NotNull(ea);
        Assert.Contains("Stop", ea.AdviceEn);
        Assert.Null(AntiCheatWatcher.FindRunning(name => name == "EasyAntiCheat")); // Accepts virtual pads.
    }
}
