using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TSCC_WASD.Core.Models;

public class MappingProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _name = L.T("自動偵測 → DualShock 4", "Auto-detect → DualShock 4");
    private string? _description = L.T("自動使用 NS2 Pro 或 Xbox 手把，輸出虛擬 DualShock 4。PS 圖示取決於遊戲支援。", "Uses an NS2 Pro or Xbox controller and outputs a virtual DualShock 4. PS prompts depend on the game.");
    private int _pollingRateHz = 125;
    private double _deadZone = 0.08;
    private InputDeviceType _inputType = InputDeviceType.Auto;
    private bool _hidePhysicalControllers = true;
    private bool _forwardRumble = true;
    private bool _xboxBackAsTouchpad = true;
    private bool _swapFaceButtons;
    private OutputControllerType _outputType = OutputControllerType.DualShock4;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get => _name; set => Set(ref _name, value); }
    public string? Description { get => _description; set => Set(ref _description, value); }

    public InputDeviceType InputType { get => _inputType; set => Set(ref _inputType, value); }
    /// <summary>虛擬手把類型：DualShock 4（PS 圖示）或 Xbox 360（Xbox 圖示）。</summary>
    public OutputControllerType OutputType { get => _outputType; set => Set(ref _outputType, value); }

    /// <summary>
    /// 對調 A/B 與 X/Y：右側的鍵變成確認（✕／Xbox A）。給習慣任天堂配置的人，
    /// 也讓 NS2 Pro 依按鍵上的字母而不是位置對應。
    /// </summary>
    public bool SwapFaceButtons { get => _swapFaceButtons; set => Set(ref _swapFaceButtons, value); }

    /// <summary>映射期間以 HidHide 隱藏實體手把，讓遊戲只看到虛擬 DS4。</summary>
    public bool HidePhysicalControllers { get => _hidePhysicalControllers; set => Set(ref _hidePhysicalControllers, value); }

    /// <summary>把遊戲送給虛擬 DS4 的震動轉給實體手把。</summary>
    public bool ForwardRumble { get => _forwardRumble; set => Set(ref _forwardRumble, value); }

    /// <summary>
    /// Xbox 的 View（Back）鍵送出 DS4 觸控板按下（PS 遊戲常用來開地圖或選單）；false 時送出 Share。
    /// NS2 Pro 不受影響：它的－鍵送 Share、截圖鍵送觸控板。
    /// </summary>
    public bool XboxBackAsTouchpad { get => _xboxBackAsTouchpad; set => Set(ref _xboxBackAsTouchpad, value); }

    /// <summary>前景程式名稱 (不含路徑) 符合時自動套用。</summary>
    public string? MatchProcessName { get; set; }

    /// <summary>輪詢頻率 (Hz)。</summary>
    public int PollingRateHz { get => _pollingRateHz; set => Set(ref _pollingRateHz, value); }

    /// <summary>類比搖桿死區 (0~1)。</summary>
    public double DeadZone { get => _deadZone; set => Set(ref _deadZone, value); }

    /// <summary>若為 true，沒有驅動或裝置時不要自動啟動。</summary>
    public bool RequireDrivers { get; set; } = true;

    /// <summary>保留給後續進階設定 (曲線、巨集等)。</summary>
    public Dictionary<string, string> Advanced { get; set; } = new();

    [JsonIgnore]
    public int PollingIntervalMs => Math.Max(2, (int)Math.Round(1000.0 / Math.Max(1, PollingRateHz)));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 100)
            throw new ArgumentException(L.T("設定檔需要 ID 與名稱（名稱最多 100 字）。", "A profile needs an ID and a name (up to 100 characters)."));
        if (InputType is not (InputDeviceType.Auto or InputDeviceType.XInput or InputDeviceType.Switch2ProUsb
                or InputDeviceType.PlayStation or InputDeviceType.SwitchPro or InputDeviceType.Switch2ProBluetooth)
            || OutputType is not (OutputControllerType.DualShock4 or OutputControllerType.Xbox360))
            throw new ArgumentException(L.T("目前支援自動偵測／Xbox／NS2 Pro／DualShock 4／DualSense／Switch Pro → DualShock 4 或 Xbox 360。",
                "Supported: auto-detect / Xbox / NS2 Pro / DualShock 4 / DualSense / Switch Pro → DualShock 4 or Xbox 360."));
        if (PollingRateHz < 30 || PollingRateHz > 500)
            throw new ArgumentException(L.T("輪詢頻率必須介於 30–500 Hz。", "Polling rate must be 30–500 Hz."));
        if (!double.IsFinite(DeadZone) || DeadZone < 0 || DeadZone > 0.95)
            throw new ArgumentException(L.T("死區必須介於 0–0.95。", "Dead zone must be 0–0.95."));
    }

    public MappingProfile Snapshot()
    {
        Validate();
        return new MappingProfile
        {
            Id = Id, Name = Name, Description = Description,
            InputType = InputType, OutputType = OutputType, HidePhysicalControllers = HidePhysicalControllers, ForwardRumble = ForwardRumble,
            XboxBackAsTouchpad = XboxBackAsTouchpad, SwapFaceButtons = SwapFaceButtons,
            PollingRateHz = PollingRateHz, DeadZone = DeadZone,
            MatchProcessName = MatchProcessName, RequireDrivers = RequireDrivers,
            Advanced = Advanced is null ? new() : new(Advanced)
        };
    }
}
