using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.App.ViewModels;

/// <summary>Texts of the Home page. Pure functions that take the language, so they test without shared state.</summary>
public static class HomeText
{
    private static string T(bool zh, string zhText, string enText) => zh ? zhText : enText;

    public static string LevelName(BatteryLevel level, bool zh) => level switch
    {
        BatteryLevel.Empty => T(zh, "空", "Empty"),
        BatteryLevel.Critical => T(zh, "極低", "Critical"),
        BatteryLevel.Low => T(zh, "低", "Low"),
        BatteryLevel.Medium => T(zh, "中", "Medium"),
        _ => T(zh, "滿", "Full")
    };

    /// <summary>
    /// "電量 低 · 充電中", "有線" when the connection has no battery. Unknown shows nothing,
    /// or "電量 —" while mapping runs (a reading is expected but not there yet).
    /// </summary>
    public static string Battery(BatteryStatus battery, bool running, bool zh)
    {
        switch (battery.Availability)
        {
            case BatteryAvailability.Known:
                string text = T(zh, "電量 ", "Battery ") + LevelName(battery.Level, zh);
                return battery.Charging ? text + " · " + T(zh, "充電中", "Charging") : text;
            case BatteryAvailability.Unsupported:
                return T(zh, "有線", "Wired");
            default:
                return running ? T(zh, "電量 —", "Battery —") : "";
        }
    }

    public static string Connection(ControllerConnection connection, bool zh) => connection switch
    {
        ControllerConnection.Usb => "USB",
        ControllerConnection.Bluetooth => T(zh, "藍牙", "Bluetooth"),
        _ => ""
    };

    /// <summary>"NS2 Pro · USB"; the connection is left out when unknown.</summary>
    public static string Controller(ControllerInfo info, bool zh)
    {
        string connection = Connection(info.Connection, zh);
        return connection.Length == 0 ? info.Name : info.Name + " · " + connection;
    }

    /// <summary>Controller card line: "NS2 Pro · USB · 電量 低 · 充電中". Parts that are empty are left out.</summary>
    public static string ControllerLine(string controller, string battery) =>
        battery.Length == 0 ? controller : controller.Length == 0 ? battery : controller + " · " + battery;

    public static string StatusLine(bool running, bool paused, OutputControllerType output, bool zh)
    {
        if (!running) return T(zh, "已停止：實體手把可見", "Stopped: physical controllers visible");
        string name = OutputName(output);
        if (paused) return T(zh, $"已暫停：遊戲看到放開所有按鍵的虛擬 {name}", $"Paused: games see a virtual {name} with nothing pressed");
        return T(zh, $"映射中：遊戲看到虛擬 {name}", $"Mapping: games see a virtual {name}");
    }

    public static string OutputName(OutputControllerType output) =>
        output == OutputControllerType.Xbox360 ? "Xbox 360" : "DualShock 4";

    public static string OutputDetail(OutputControllerType output, bool zh) =>
        output == OutputControllerType.Xbox360
            ? T(zh, "Xbox 圖示，相容性最好", "Xbox prompts, best compatibility")
            : T(zh, "PS 圖示、觸控板、體感", "PS prompts, touchpad, motion");

    public static string MainButton(bool running, bool zh) =>
        running ? T(zh, "停止映射", "Stop mapping") : T(zh, "開始映射", "Start mapping");

    public static string Footer(bool running, bool hiding, bool zh, int? hiddenCount = null) =>
        !running ? T(zh, "按「開始映射」後，遊戲只會看到虛擬手把", "After you press Start mapping, games only see the virtual controller")
        : hiding && hiddenCount is > 0 ? T(zh, $"HidHide 正在隱藏 {hiddenCount} 個實體手把介面，遊戲只看到虛擬手把", $"HidHide is hiding {hiddenCount} physical controller interface(s); games only see the virtual one")
        : hiding ? T(zh, "HidHide 正在隱藏實體手把，遊戲只看到虛擬手把", "HidHide is hiding the physical controllers; games only see the virtual one")
        : T(zh, "實體手把沒有隱藏，遊戲可能同時看到兩支", "Physical controllers are not hidden; games may see both");
}
