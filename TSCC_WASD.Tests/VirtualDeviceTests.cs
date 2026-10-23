using Xunit;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Tests;

/// <summary>Device-tree chains recorded on a real PC (2026-10-23), including devices that are no longer present.</summary>
public class VirtualDeviceTests
{
    private static readonly Dictionary<string, string> Parents = new(StringComparer.OrdinalIgnoreCase)
    {
        // Present ViGEm DS4: HID child → virtual USB device → ViGEm bus.
        [@"HID\VID_054C&PID_05C4&REV_0100\2&1B721325&0&0000"] = @"USB\VID_054C&PID_05C4&REV_0100\1&1a590e2c&2&01",
        [@"USB\VID_054C&PID_05C4&REV_0100\1&1a590e2c&2&01"] = @"ROOT\SYSTEM\0002",
        // Leftover ViGEm DS4 that is no longer present: its USB node reports the tree root.
        [@"HID\VID_054C&PID_05C4&REV_0100\2&BE03103&1&0000"] = @"USB\VID_054C&PID_05C4&REV_0100\1&2d12bed1&0&01",
        [@"USB\VID_054C&PID_05C4&REV_0100\1&2d12bed1&0&01"] = @"HTREE\ROOT\0",
        // ViGEm Xbox 360: HID child is three levels below the bus.
        [@"HID\VID_045E&PID_028E&IG_00\3&17161442&0&0000"] = @"USB\VID_045E&PID_028E&IG_00\2&4cba17d&0&00",
        [@"USB\VID_045E&PID_028E&IG_00\2&4cba17d&0&00"] = @"USB\VID_045E&PID_028E\02",
        [@"USB\VID_045E&PID_028E\02"] = @"ROOT\SYSTEM\0002",
        // Physical NS2 Pro on USB (not present any more), physical wired Xbox, Bluetooth LE Xbox.
        [@"HID\VID_057E&PID_2069&MI_00\A&37D9C433&0&0000"] = @"USB\VID_057E&PID_2069&MI_00\9&2356d3fd&0&0000",
        [@"USB\VID_057E&PID_2069&MI_00\9&2356d3fd&0&0000"] = @"USB\VID_057E&PID_2069\00",
        [@"USB\VID_057E&PID_2069\00"] = @"USB\ROOT_HUB30\7&18eea21b&0&0",
        [@"USB\VID_045E&PID_0B12\3039373130383835353332323333"] = @"USB\ROOT_HUB30\7&124f193f&0&0",
        [@"HID\{00001812-0000-1000-8000-00805F9B34FB}&DEV&VID_045E&PID_0B13\D&23B31DF3&0&0000"] = @"BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&02045e_PID&0b13\c&370b6cc7&2&0016",
    };

    private static string? Parent(string id) => Parents.GetValueOrDefault(id);

    [Theory]
    [InlineData(@"HID\VID_054C&PID_05C4&REV_0100\2&1B721325&0&0000")]
    [InlineData(@"HID\VID_054C&PID_05C4&REV_0100\2&BE03103&1&0000")]
    [InlineData(@"HID\VID_045E&PID_028E&IG_00\3&17161442&0&0000")]
    [InlineData(@"HID\VID_DEAD&PID_BEEF\unknown-parent")]
    public void VirtualOrUnknownPadsAreNeverHidden(string id) => Assert.True(WindowsDevicePaths.IsVirtual(id, Parent));

    [Theory]
    [InlineData(@"HID\VID_057E&PID_2069&MI_00\A&37D9C433&0&0000")]
    [InlineData(@"USB\VID_045E&PID_0B12\3039373130383835353332323333")]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805F9B34FB}&DEV&VID_045E&PID_0B13\D&23B31DF3&0&0000")]
    public void PhysicalControllersAreRecognised(string id) => Assert.False(WindowsDevicePaths.IsVirtual(id, Parent));
}
