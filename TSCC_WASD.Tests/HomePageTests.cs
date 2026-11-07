using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Input;
using Xunit;

namespace TSCC_WASD.Tests;

public class HomePageTests
{
    [Theory]
    [InlineData(false, true, "開始映射")]
    [InlineData(true, true, "停止映射")]
    [InlineData(false, false, "Start mapping")]
    [InlineData(true, false, "Stop mapping")]
    public void MainButtonLabelSwitchesBetweenStartAndStop(bool running, bool zh, string expected) =>
        Assert.Equal(expected, HomeText.MainButton(running, zh));

    [Fact]
    public async Task MainCommandIsStartWhileStopped()
    {
        var vm = new MainViewModel();
        try
        {
            Assert.False(vm.IsRunning);
            Assert.Same(vm.StartCommand, vm.MainCommand);
        }
        finally { await vm.DisposeAsync(); }
    }

    [Theory]
    [InlineData(BatteryLevel.Empty, "電量 空")]
    [InlineData(BatteryLevel.Critical, "電量 極低")]
    [InlineData(BatteryLevel.Low, "電量 低")]
    [InlineData(BatteryLevel.Medium, "電量 中")]
    [InlineData(BatteryLevel.Full, "電量 滿")]
    public void KnownBatteryUsesTheFiveLevelNames(BatteryLevel level, string expected) =>
        Assert.Equal(expected, HomeText.Battery(BatteryStatus.Of(level, false), running: true, zh: true));

    [Fact]
    public void BatteryEnglishNamesAndCharging()
    {
        Assert.Equal("Battery Critical", HomeText.Battery(BatteryStatus.Of(BatteryLevel.Critical, false), true, false));
        Assert.Equal("電量 低 · 充電中", HomeText.Battery(BatteryStatus.Of(BatteryLevel.Low, true), true, true));
        Assert.Equal("Battery Full · Charging", HomeText.Battery(BatteryStatus.Of(BatteryLevel.Full, true), false, false));
    }

    [Fact]
    public void UnknownBatteryShowsNothingWhenStoppedAndADashWhileMapping()
    {
        Assert.Equal("", HomeText.Battery(BatteryStatus.Unknown, running: false, zh: true));
        Assert.Equal("電量 —", HomeText.Battery(BatteryStatus.Unknown, running: true, zh: true));
    }

    [Fact]
    public void LastKnownBatteryStaysAfterMappingStops() =>
        Assert.Equal("電量 中", HomeText.Battery(BatteryStatus.Of(BatteryLevel.Medium, false), running: false, zh: true));

    [Fact]
    public void UnsupportedBatteryShowsWired()
    {
        Assert.Equal("有線", HomeText.Battery(BatteryStatus.Unsupported, true, true));
        Assert.Equal("Wired", HomeText.Battery(BatteryStatus.Unsupported, true, false));
    }

    [Fact]
    public void StatusLineForRunningAndStopped()
    {
        Assert.Equal("已停止：實體手把可見", HomeText.StatusLine(false, false, OutputControllerType.DualShock4, true));
        Assert.Equal("映射中：遊戲看到虛擬 DualShock 4", HomeText.StatusLine(true, false, OutputControllerType.DualShock4, true));
        Assert.Equal("映射中：遊戲看到虛擬 Xbox 360", HomeText.StatusLine(true, false, OutputControllerType.Xbox360, true));
        Assert.StartsWith("Mapping:", HomeText.StatusLine(true, false, OutputControllerType.DualShock4, false));
        Assert.StartsWith("已暫停", HomeText.StatusLine(true, true, OutputControllerType.DualShock4, true));
    }

    [Fact]
    public void FooterFollowsRunningAndHiding()
    {
        Assert.Equal("按「開始映射」後，遊戲只會看到虛擬手把", HomeText.Footer(false, false, true));
        Assert.Contains("HidHide", HomeText.Footer(true, true, true));
        Assert.NotEqual(HomeText.Footer(true, true, true), HomeText.Footer(true, false, true));
    }
}
