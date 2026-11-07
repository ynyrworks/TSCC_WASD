using TSCC_WASD.App.Pages;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;

namespace TSCC_WASD.App;

/// <summary>What a step needs besides fixed text.</summary>
public enum TourStepKind { Plain, Drivers }

/// <summary>
/// One coach mark. <see cref="Targets"/> are x:Names of the elements it points at (the union of their bounds is
/// highlighted); they live in the XAML of <see cref="Page"/>, or in MainWindow.xaml when <see cref="InWindow"/>.
/// No targets: the card is centred.
/// </summary>
public sealed record TourStep(string Id, string Title, string Body, Type Page, string[] Targets, bool InWindow = false,
    TourStepKind Kind = TourStepKind.Plain);

/// <summary>The tour's steps, all in one place. Text follows the UI language chosen at launch.</summary>
public static class TourSteps
{
    public static IReadOnlyList<TourStep> Build() =>
    [
        new("welcome", L.T("歡迎使用 TSCC_WASD", "Welcome to TSCC_WASD"),
            L.T("這個程式讓遊戲把你的 Xbox 或 NS2 Pro 手把當成 DualShock 4，顯示 PS 按鍵圖示。花一分鐘看看怎麼開始？",
                "This app makes games see your Xbox or NS2 Pro controller as a DualShock 4, so they show PS prompts. Take a minute to see how to get going?"),
            typeof(HomePage), []),
        new("drivers", L.T("驅動程式", "Drivers"), "", typeof(SettingsPage), ["DriversRow"], Kind: TourStepKind.Drivers),
        new("controller", L.T("連接手把", "Connect your controller"),
            L.T("USB 直接插上；藍牙請先在 Windows 配對。NS2 Pro 走藍牙時，請先按「開始映射」，再按住手把上的 Sync 鍵。",
                "Plug in over USB, or pair over Bluetooth in Windows first. For an NS2 Pro over Bluetooth, press Start mapping first, then hold the Sync button on the controller."),
            typeof(HomePage), ["ControllerCard"]),
        new("output", L.T("遊戲看到的手把", "The controller games see"),
            L.T("這裡是遊戲實際看到的手把：DualShock 4 讓遊戲顯示 PS 按鍵，Xbox 360 的相容性最好。可在設定檔頁的「輸出成」更換。",
                "This is the controller games actually see: DualShock 4 shows PS prompts, Xbox 360 has the best compatibility. Change it with Output as on the Profiles page."),
            typeof(HomePage), ["OutputCard"]),
        new("start", L.T("開始映射", "Start mapping"),
            L.T("按這個主按鈕開始或停止映射。旁邊的「▾」選單可以暫停，或完全釋放手把。",
                "Press the main button to start or stop mapping. The ▾ menu next to it pauses, or releases everything."),
            typeof(HomePage), ["MainActions"]),
        new("steam", "Steam",
            L.T("如果 Steam 在映射開始前就開著，請在映射中按「重新啟動 Steam」，讓它只看到虛擬手把。",
                "If Steam was already running before mapping started, press Restart Steam while mapping so it sees only the virtual controller."),
            typeof(HomePage), ["SteamCard"]),
        new("profiles", L.T("設定檔與設定", "Profiles and settings"),
            L.T("左側的「設定檔」調整按鍵、搖桿與每款遊戲的自動切換；「程式設定」管理開機與驅動程式。所有變更都會自動儲存。",
                "Profiles on the left tunes buttons and sticks and switches automatically per game; App settings covers start-up and drivers. Every change is saved automatically."),
            typeof(HomePage), ["NavHomeItem", "NavProfilesItem", "NavSettingsItem"], InWindow: true),
        new("done", L.T("完成了", "All set"),
            L.T("需要說明時，到「關於與說明」開啟使用手冊；想再看一次導覽，按那裡的「重新顯示導覽」。",
                "For help, open the user manual under About & help. To see this tour again, press Show the tour again there."),
            typeof(AboutPage), ["HelpCard"]),
    ];
}

/// <summary>
/// The tour's state machine: no UI, so it can be tested directly. Skipping and finishing both call <c>onDone</c> once
/// (the caller stores the "done" flag); Esc counts as skipping.
/// </summary>
public sealed class TourController : ObservableObject
{
    private readonly IReadOnlyList<TourStep> _steps;
    private readonly Func<(bool ViGEm, bool HidHide)> _drivers;
    private readonly Action _onDone;
    private int _index;
    private bool _isActive;

    public TourController(IReadOnlyList<TourStep> steps, Action onDone, Func<(bool ViGEm, bool HidHide)> drivers)
    {
        _steps = steps;
        _onDone = onDone;
        _drivers = drivers;
    }

    /// <summary>Raised when the current step changes or the tour opens, so the view can navigate and re-anchor.</summary>
    public event Action? StepChanged;

    public int Count => _steps.Count;
    public int Index => _index;
    public bool IsActive { get => _isActive; private set => SetField(ref _isActive, value); }
    public TourStep Step => _steps[_index];
    public bool IsFirst => _index == 0;
    public bool IsLast => _index == _steps.Count - 1;
    public bool CanGoBack => !IsFirst;
    public bool ShowSkip => !IsLast;

    public string StepText => L.T($"{_index + 1}／{Count}", $"{_index + 1}/{Count}");
    public string Title => Step.Title;
    public string NextText => IsFirst ? L.T("開始導覽", "Start tour") : IsLast ? L.T("完成", "Done") : L.T("下一步", "Next");

    public string Body => Step.Kind == TourStepKind.Drivers ? DriversBody() : Step.Body;

    /// <summary>The drivers step offers the install button only while something is missing.</summary>
    public bool ShowInstall => IsActive && Step.Kind == TourStepKind.Drivers && Missing().Count > 0;

    private List<string> Missing()
    {
        var (vigem, hidhide) = _drivers();
        var missing = new List<string>();
        if (!vigem) missing.Add("ViGEmBus");
        if (!hidhide) missing.Add("HidHide");
        return missing;
    }

    private string DriversBody()
    {
        var missing = Missing();
        if (missing.Count == 0) return L.T("ViGEmBus 與 HidHide 都已就緒。", "ViGEmBus and HidHide are both ready.");
        string names = string.Join(L.T("、", ", "), missing);
        return L.T($"尚未安裝 {names}。ViGEmBus 建立虛擬手把，HidHide 對遊戲隱藏實體手把；按下面的按鈕即可安裝。",
            $"{names} not installed yet. ViGEmBus creates the virtual controller and HidHide hides the real one from games; press the button below to install.");
    }

    /// <summary>Opens the tour at the first step (also used to replay it).</summary>
    public void Start()
    {
        _index = 0;
        IsActive = true;
        Changed();
    }

    public void Next()
    {
        if (!IsActive) return;
        if (IsLast) { Finish(); return; }
        _index++;
        Changed();
    }

    public void Back()
    {
        if (!IsActive || IsFirst) return;
        _index--;
        Changed();
    }

    public void Skip() => Close();
    public void Finish() => Close();

    /// <summary>Esc: closes the tour as skipped. Returns whether it was open (so the key can be marked handled).</summary>
    public bool Escape()
    {
        if (!IsActive) return false;
        Close();
        return true;
    }

    /// <summary>Re-reads the driver check (after the install button ran).</summary>
    public void RefreshDrivers()
    {
        RaisePropertyChanged(nameof(Body));
        RaisePropertyChanged(nameof(ShowInstall));
    }

    private void Close()
    {
        if (!IsActive) return;
        IsActive = false;
        RaisePropertyChanged(nameof(ShowInstall));
        _onDone();
    }

    private void Changed()
    {
        foreach (var name in new[] { nameof(Index), nameof(Step), nameof(IsFirst), nameof(IsLast), nameof(CanGoBack), nameof(ShowSkip),
                     nameof(StepText), nameof(Title), nameof(Body), nameof(NextText), nameof(ShowInstall) })
            RaisePropertyChanged(name);
        StepChanged?.Invoke();
    }
}

/// <summary>Decides whether the tour may open on its own right now.</summary>
public static class TourGate
{
    public static bool CanAutoStart(bool completed, bool active, bool windowVisible, bool minimized, bool dialogOpen) =>
        !completed && !active && windowVisible && !minimized && !dialogOpen;
}
