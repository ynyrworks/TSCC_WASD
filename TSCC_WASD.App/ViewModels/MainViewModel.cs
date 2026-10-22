using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services;
using TSCC_WASD.Core.Services.Mapping;
using TSCC_WASD.Core.Services.Setup;

namespace TSCC_WASD.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly MappingCoordinator _coordinator;
    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly List<AsyncRelayCommand> _commands = new();
    private MappingProfile? _selectedProfile;
    private string _statusMessage = L.T("選擇設定檔後啟動映射。", "Choose a profile and start mapping.");
    private string _inputSummary = L.T("尚未啟動映射", "Mapping not started");
    private bool _isRunning, _isPaused, _busy, _closing, _loadFailed, _updating;
    private CancellationTokenSource? _updateCts;
    private double _updateProgress;
    private string? _updateProgressText;
    private bool? _reconnectInstalled;
    private DateTime? _hiddenSince;
    private string _dependencyStatus = DependencyChecker.BuildStatusText();

    public MainViewModel(ProfileStore? profileStore = null, AppSettingsStore? settingsStore = null)
    {
        _coordinator = new MappingCoordinator(profileStore);
        _settingsStore = settingsStore ?? new AppSettingsStore();
        Settings = _settingsStore.Load();
        StartCommand = Command(() => StartAsync(automatic: false, atSignIn: false), () => !IsRunning && SelectedProfile is not null);
        StopCommand = Command(StopAsync, () => IsRunning);
        PauseCommand = Command(() =>
        {
            IsPaused = !IsPaused;
            StatusMessage = IsPaused
                ? L.T("已暫停：遊戲看到的是放開所有按鍵的虛擬 DS4，實體手把仍隱藏，Steam 不會重新抓到它。",
                    "Paused: games see a virtual DS4 with nothing pressed; the controllers stay hidden so Steam can't grab them.")
                : L.T("已繼續映射。", "Mapping resumed.");
            return Task.CompletedTask;
        }, () => IsRunning);
        SetupReconnectCommand = Command(async () =>
        {
            if (await SetUpReconnectHelperAsync() && IsRunning && SteamHelper.IsRunning) await ReconnectControllersAsync();
        });
        RemoveReconnectCommand = Command(async () =>
        {
            bool removed = await ControllerReconnector.UninstallAsync();
            _reconnectInstalled = null;
            RaisePropertyChanged(nameof(ReconnectHelperStatus));
            StatusMessage = removed
                ? L.T("已移除自動重新連接工具。", "The automatic reconnect helper was removed.")
                : L.T("沒有移除自動重新連接工具（已取消或失敗）。", "The automatic reconnect helper was not removed (cancelled or failed).");
        }, () => ControllerReconnector.IsPresent);
        RestartSteamCommand = Command(() => RestartSteamAsync(silent: false), () => IsRunning);
        ReloadCommand = Command(() => { Reload(); return Task.CompletedTask; }, () => !IsRunning);
        SaveCommand = Command(() => { Save(); return Task.CompletedTask; }, () => !_loadFailed);
        NewCommand = Command(() =>
        {
            var profile = new MappingProfile { Name = L.T("新設定檔", "New profile") };
            Profiles.Add(profile);
            SelectedProfile = profile;
            return Task.CompletedTask;
        }, () => !_loadFailed);
        DeleteCommand = Command(() =>
        {
            if (SelectedProfile is not null) Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = L.T("已移除，按儲存套用至磁碟。", "Removed. Click Save all to apply.");
            return Task.CompletedTask;
        }, () => !IsRunning && !_loadFailed && Profiles.Count > 1 && SelectedProfile is not null);
        ImportCommand = Command(() =>
        {
            var dialog = new OpenFileDialog { Filter = L.T("JSON 設定檔|*.json", "JSON profiles|*.json") };
            if (dialog.ShowDialog() == true)
            {
                var imported = new ProfileStore(dialog.FileName).LoadProfiles();
                foreach (var profile in imported)
                {
                    profile.Id = Guid.NewGuid().ToString();
                    Profiles.Add(profile);
                }
                SelectedProfile = Profiles.Last();
                StatusMessage = L.T("匯入完成，按儲存套用至磁碟。", "Imported. Click Save all to apply.");
            }
            return Task.CompletedTask;
        }, () => !IsRunning && !_loadFailed);
        ExportCommand = Command(() =>
        {
            var dialog = new SaveFileDialog { Filter = L.T("JSON 設定檔|*.json", "JSON profiles|*.json"), FileName = "TSCC_WASD-profiles.json" };
            if (dialog.ShowDialog() == true)
            {
                new ProfileStore(dialog.FileName).SaveProfiles(Profiles);
                StatusMessage = L.T("匯出完成。", "Exported.");
            }
            return Task.CompletedTask;
        }, () => Profiles.Count > 0);
        InstallDepsCommand = Command(async () =>
        {
            var missing = DriverInstaller.Missing();
            if (missing.Count == 0)
            {
                StatusMessage = L.T("ViGEmBus 與 HidHide 都已安裝。", "ViGEmBus and HidHide are both installed.");
                return;
            }
            if (ConfirmDriverInstall(missing)) await InstallDriversAsync(missing);
        }, () => !IsRunning);
        CopyDiagnosticsCommand = Command(() =>
        {
            Clipboard.SetText(DiagnosticReport.Build(InputSummary, IsRunning) + $"Log: {AppLog.CurrentFile}{Environment.NewLine}");
            StatusMessage = L.T("診斷資訊已複製，可直接貼到 GitHub 問題回報。", "Diagnostics copied. Paste them into a GitHub issue.");
            return Task.CompletedTask;
        });
        OpenLogsCommand = Command(() =>
        {
            Directory.CreateDirectory(AppLog.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { AppLog.Directory }, UseShellExecute = true });
            return Task.CompletedTask;
        });
        SupportCommand = Command(() =>
        {
            OpenUrl(KoFiUrl);
            return Task.CompletedTask;
        });
        CalibrateCommand = Command(() =>
        {
            // The wizard reads the controller through its own shared HID handle, so mapping can keep running;
            // a running mapping applies the saved calibration immediately.
            var window = new CalibrationWindow { Owner = Application.Current?.MainWindow };
            if (window.ShowDialog() == true)
                StatusMessage = L.T("已儲存並套用你的搖桿校準。", "Your stick calibration was saved and applied.");
            return Task.CompletedTask;
        });
        CheckUpdatesCommand = Command(() => CheckUpdatesAsync(quiet: false));
        CancelUpdateCommand = new RelayCommand(() => _updateCts?.Cancel());
        OpenHelpCommand = Command(() =>
        {
            OpenUrl(L.T($"https://github.com/{UpdateChecker.Repository}/blob/main/docs/README.zh-TW.md",
                $"https://github.com/{UpdateChecker.Repository}#readme"));
            return Task.CompletedTask;
        });
        AboutCommand = Command(() =>
        {
            new AboutWindow { Owner = Application.Current?.MainWindow, DataContext = this }.ShowDialog();
            return Task.CompletedTask;
        });
        OpenProfilesCommand = Command(() =>
        {
            var path = AppPaths.DataDirectory;
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = true });
            return Task.CompletedTask;
        });
        Reload();
        if (_coordinator.RecoverHiddenControllers() is { } recovered) StatusMessage = recovered;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private AsyncRelayCommand Command(Func<Task> action, Func<bool>? canExecute = null)
    {
        var command = new AsyncRelayCommand(() => RunGuardedAsync(action), () => !_closing && !_busy && !_updating && (canExecute?.Invoke() ?? true));
        _commands.Add(command);
        return command;
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        _busy = true;
        NotifyState();
        try { await action(); }
        catch (Exception ex) { StatusMessage = L.T($"操作失敗：{ex.Message}", $"Failed: {ex.Message}"); }
        finally { _busy = false; NotifyState(); }
    }

    public ObservableCollection<MappingProfile> Profiles { get; } = new();
    public IReadOnlyList<InputOption> InputOptions { get; } =
    [
        new(InputDeviceType.Auto, L.T("自動偵測（所有支援的手把，建議）", "Auto-detect (every supported controller, recommended)")),
        new(InputDeviceType.XInput, L.T("只用 Xbox／XInput", "Xbox / XInput only")),
        new(InputDeviceType.Switch2ProUsb, L.T("只用 Nintendo Switch 2 Pro（USB）", "Nintendo Switch 2 Pro (USB) only")),
        new(InputDeviceType.PlayStation, L.T("只用 DualShock 4／DualSense（實驗性）", "DualShock 4 / DualSense only (experimental)")),
        new(InputDeviceType.SwitchPro, L.T("只用 Switch Pro 手把（第一代，實驗性）", "Switch Pro Controller, original (experimental)"))
    ];
    public sealed record InputOption(InputDeviceType Type, string Name);
    public IReadOnlyList<OutputOption> OutputOptions { get; } =
    [
        new(OutputControllerType.DualShock4, L.T("DualShock 4（PS 按鍵圖示）", "DualShock 4 (PlayStation prompts)")),
        new(OutputControllerType.Xbox360, L.T("Xbox 360（Xbox 按鍵圖示）", "Xbox 360 (Xbox prompts)"))
    ];
    public sealed record OutputOption(OutputControllerType Type, string Name);

    /// <summary>Messages worth a notification while the window may be hidden behind a game.</summary>
    public event Action<string>? BackgroundNotice;
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("auto", L.T("跟隨 Windows", "Follow Windows")),
        new("zh-TW", "繁體中文"),
        new("en", "English")
    ];
    public sealed record LanguageOption(string Code, string Name);

    public MappingProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            var previous = _selectedProfile;
            if (!SetField(ref _selectedProfile, value)) return;
            if (previous is not null) previous.PropertyChanged -= OnSelectedProfileEdited;
            if (value is not null) value.PropertyChanged += OnSelectedProfileEdited;
            NotifyState();
            if (IsRunning && value is not null) _ = ApplyLiveAsync(value, switched: true);
        }
    }

    private void OnSelectedProfileEdited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (IsRunning && sender is MappingProfile profile) _ = ApplyLiveAsync(profile, switched: false);
    }

    /// <summary>Edits and profile switches take effect immediately; mapping (and hiding) keeps running.</summary>
    private async Task ApplyLiveAsync(MappingProfile profile, bool switched)
    {
        string? message;
        try { message = await _coordinator.ApplyProfileAsync(profile); }
        catch (Exception ex) { message = L.T($"無法套用設定：{ex.Message}", $"Could not apply the change: {ex.Message}"); }
        if (switched)
        {
            Settings.LastProfileId = profile.Id;
            SaveSettings();
            message ??= L.T($"已切換到「{profile.Name}」，不需重新啟動映射。", $"Switched to \"{profile.Name}\" without restarting mapping.");
        }
        if (message is not null) StatusMessage = message;
        IsRunning = _coordinator.IsRunning;
    }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string InputSummary { get => _inputSummary; private set => SetField(ref _inputSummary, value); }
    public string DependencyStatus { get => _dependencyStatus; private set => SetField(ref _dependencyStatus, value); }
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value)) return;
            if (!value) IsPaused = false;
            RaisePropertyChanged(nameof(StatusTitle));
            NotifyState();
        }
    }
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (!SetField(ref _isPaused, value)) return;
            _coordinator.Paused = value;
            RaisePropertyChanged(nameof(StatusTitle));
            RaisePropertyChanged(nameof(PauseButtonText));
        }
    }

    public string PauseButtonText => IsPaused ? L.T("繼續", "Resume") : L.T("暫停", "Pause");

    /// <summary>Profile fields can be edited while mapping; edits apply immediately.</summary>
    public bool CanEdit => !_busy && !_closing && !_loadFailed && !_updating;

    /// <summary>Download progress (0–1) while an update is being downloaded.</summary>
    public double UpdateProgress { get => _updateProgress; private set => SetField(ref _updateProgress, value); }
    /// <summary>Shown under the status line while an update downloads; null hides the progress bar.</summary>
    public string? UpdateProgressText
    {
        get => _updateProgressText;
        private set
        {
            if (SetField(ref _updateProgressText, value)) RaisePropertyChanged(nameof(IsDownloadingUpdate));
        }
    }
    public bool IsDownloadingUpdate => _updateProgressText is not null;

    public string ReconnectHelperStatus
    {
        get
        {
            _reconnectInstalled ??= ControllerReconnector.IsInstalled;
            return _reconnectInstalled.Value
                ? L.T("已設定：Steam 握著手把時會自動重新連接 USB 手把，不必重啟 Steam。", "Set up: when Steam holds a controller, USB controllers are reconnected automatically instead of restarting Steam.")
                : ControllerReconnector.NeedsUpdate
                    ? L.T("需要更新（TSCC_WASD 已更新），設定一次即可。", "Needs an update for this TSCC_WASD version; set it up once more.")
                    : L.T("尚未設定：Steam 握著手把時只能重啟 Steam。", "Not set up: when Steam holds a controller, Steam has to restart.");
        }
    }

    public AppSettings Settings { get; }

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows == value) return;
            try
            {
                StartupRegistration.Set(value, Environment.ProcessPath ?? throw new InvalidOperationException());
                Settings.StartWithWindows = value;
                SaveSettings();
            }
            catch (Exception ex) { StatusMessage = L.T($"無法變更開機啟動：{ex.Message}", $"Could not change sign-in launch: {ex.Message}"); }
            RaisePropertyChanged();
        }
    }
    public bool StartMappingOnLaunch { get => Settings.StartMappingOnLaunch; set => SetSetting(() => Settings.StartMappingOnLaunch = value); }
    public bool MinimizeToTray { get => Settings.MinimizeToTray; set => SetSetting(() => Settings.MinimizeToTray = value); }
    public bool ManageSteam { get => Settings.ManageSteam; set => SetSetting(() => Settings.ManageSteam = value); }
    public bool LaunchSteamAfterAutoStart { get => Settings.LaunchSteamAfterAutoStart; set => SetSetting(() => Settings.LaunchSteamAfterAutoStart = value); }
    public bool CheckUpdatesOnStartup { get => Settings.CheckUpdatesOnStartup; set => SetSetting(() => Settings.CheckUpdatesOnStartup = value); }
    public string Language
    {
        get => Settings.Language;
        set
        {
            SetSetting(() => Settings.Language = value);
            StatusMessage = L.T("語言會在下次開啟程式時套用。", "The language applies the next time TSCC_WASD opens.");
        }
    }

    /// <summary>Shows a one-off message from the app shell (for example after a data migration).</summary>
    public void ShowNotice(string message) => StatusMessage = message;

    private void SetSetting(Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        apply();
        SaveSettings();
        RaisePropertyChanged(property);
    }

    private void SaveSettings()
    {
        try { _settingsStore.Save(Settings); }
        catch (Exception ex) { StatusMessage = L.T($"無法儲存程式設定：{ex.Message}", $"Could not save settings: {ex.Message}"); }
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand SetupReconnectCommand { get; }
    public ICommand RemoveReconnectCommand { get; }
    public ICommand RestartSteamCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand InstallDepsCommand { get; }
    public ICommand OpenProfilesCommand { get; }
    public ICommand CalibrateCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand CancelUpdateCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand CopyDiagnosticsCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand SupportCommand { get; }

    public const string KoFiUrl = "https://ko-fi.com/ynyr5566";

    public string VersionText => $"v{UpdateChecker.CurrentVersion.ToString(3)}";

    /// <summary>Headline of the status card.</summary>
    public string StatusTitle => !IsRunning ? L.T("已停止", "Stopped") : IsPaused ? L.T("已暫停", "Paused") : L.T("映射中", "Mapping");

    internal static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();

    /// <summary>
    /// At launch, offers to install missing drivers: ViGEmBus always (mapping needs it), HidHide
    /// until the user declines once. Declining HidHide is remembered; the Install drivers link still works.
    /// </summary>
    public async Task OfferMissingDriversAsync()
    {
        var missing = DriverInstaller.Missing().Where(p => p.Required || Settings.OfferHidHideInstall).ToList();
        if (missing.Count == 0 || IsRunning || _busy) return;
        if (ConfirmDriverInstall(missing))
        {
            await RunGuardedAsync(() => InstallDriversAsync(missing));
            return;
        }
        if (missing.Any(p => !p.Required))
        {
            Settings.OfferHidHideInstall = false;
            SaveSettings();
        }
        StatusMessage = L.T("之後可隨時按「安裝驅動程式」安裝。", "You can install them any time with Install drivers.");
    }

    private static bool ConfirmDriverInstall(IReadOnlyList<DriverPackage> packages)
    {
        var lines = string.Join(Environment.NewLine, packages.Select(p => p.Required
            ? L.T($"• {p.Name} {p.Version}（必要：建立虛擬 DS4）", $"• {p.Name} {p.Version} (required: creates the virtual DS4)")
            : L.T($"• {p.Name} {p.Version}（強烈建議：對遊戲隱藏實體手把）", $"• {p.Name} {p.Version} (strongly recommended: hides the real controller from games)")));
        var source = packages.All(DriverInstaller.IsBundled)
            ? L.T("將執行程式資料夾 drivers 內附的 Nefarius 官方安裝程式", "The official Nefarius installers in the drivers folder will run")
            : L.T("將從 GitHub 下載 Nefarius 官方安裝程式", "The official Nefarius installers will be downloaded from GitHub");
        var answer = MessageBox.Show(
            L.T($"尚未安裝下列免費驅動程式：\n\n{lines}\n\n要現在安裝嗎？{source}，執行前會核對 SHA-256。" +
                "Windows 會要求管理員權限，安裝後可能需要重新開機。\n解除安裝：Windows 設定 → 應用程式。",
                $"These free drivers are not installed:\n\n{lines}\n\nInstall them now? {source}; each is checked against its SHA-256 first. " +
                "Windows asks for administrator rights, and a restart may be needed afterwards.\nTo uninstall: Windows Settings → Apps."),
            AppPaths.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return answer == MessageBoxResult.Yes;
    }

    /// <summary>Runs each official installer in turn; stops at the first failure or cancelled UAC prompt.</summary>
    private async Task InstallDriversAsync(IReadOnlyList<DriverPackage> packages)
    {
        foreach (var package in packages)
        {
            StatusMessage = L.T($"正在準備 {package.Name} {package.Version} 安裝程式…", $"Preparing the {package.Name} {package.Version} installer…");
            string installer;
            try { installer = await DriverInstaller.GetInstallerAsync(package); }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException
                                           or IOException or InvalidDataException or UnauthorizedAccessException)
            {
                OpenUrl(DriverDownloadsUrl);
                StatusMessage = L.T($"無法取得 {package.Name} 安裝程式：{ex.Message} 已開啟官方下載頁。",
                    $"Could not get the {package.Name} installer: {ex.Message} Opened the official downloads page.");
                return;
            }
            StatusMessage = L.T($"請在 {package.Name} 安裝程式中完成安裝…", $"Finish the {package.Name} installer…");
            if (await DriverInstaller.RunAsync(installer) is null)
            {
                StatusMessage = L.T($"已取消安裝 {package.Name}。", $"{package.Name} installation was cancelled.");
                return;
            }
        }
        DependencyStatus = DependencyChecker.BuildStatusText();
        var stillMissing = DriverInstaller.Missing().Select(p => p.Id).Intersect(packages.Select(p => p.Id)).Any();
        StatusMessage = stillMissing
            ? L.T("仍偵測不到部分驅動程式。若安裝程式要求重新開機，請重開機後再開啟 TSCC_WASD。",
                "Some drivers are still not detected. If an installer asked for a restart, restart Windows and open TSCC_WASD again.")
            : L.T("驅動程式安裝完成。若安裝程式要求重新開機，請先重開機再啟動映射。",
                "Drivers installed. If an installer asked for a restart, restart Windows before mapping.");
    }

    private const string DriverDownloadsUrl = "https://docs.nefarius.at/Downloads/";

    /// <summary>Asks GitHub for the newest release. Quiet mode (startup check) only speaks up for an update.</summary>
    public async Task CheckUpdatesAsync(bool quiet)
    {
        if (!quiet) StatusMessage = L.T("正在檢查更新…", "Checking for updates…");
        UpdateInfo? latest;
        try { latest = await UpdateChecker.GetLatestAsync(); }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (!quiet) StatusMessage = L.T($"無法檢查更新：{ex.Message}", $"Could not check for updates: {ex.Message}");
            return;
        }
        if (latest is null || !UpdateChecker.IsNewer(latest, UpdateChecker.CurrentVersion))
        {
            if (!quiet) StatusMessage = L.T($"已是最新版本（{VersionText}）。", $"You have the latest version ({VersionText}).");
            return;
        }
        StatusMessage = L.T($"有新版本 {latest.Tag} 可下載。", $"{latest.Tag} is available.");
        if (_updating) return;
        var package = UpdateChecker.SelectPackage(latest);
        string? problem = package is null
            ? L.T("這個發行版沒有本程式可用的 Windows ZIP。", "This release has no Windows ZIP TSCC_WASD can install.")
            : UpdateInstaller.InPlaceProblem();
        if (package is null || problem is not null)
        {
            // Can't install from here; the release page still has the download.
            var open = MessageBox.Show(
                L.T($"有新版本 {latest.Tag}（目前 {VersionText}）。{problem}\n\n要開啟下載頁嗎？",
                    $"{latest.Tag} is available (you have {VersionText}). {problem}\n\nOpen the download page?"),
                AppPaths.Name, MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes) OpenUrl(latest.Url);
            return;
        }
        string size = package.Zip.Size > 0 ? $"{package.Zip.Size / 1048576.0:0.0} MB" : "?";
        var answer = MessageBox.Show(
            L.T($"有新版本 {latest.Tag}（目前 {VersionText}）。要下載並安裝嗎？\n\n" +
                $"將從 TSCC_WASD 的 GitHub 發行版下載 {package.Zip.Name}（{size}），安裝前會核對 SHA-256 與數位簽章。",
                $"{latest.Tag} is available (you have {VersionText}). Download and install it?\n\n" +
                $"{package.Zip.Name} ({size}) is downloaded from TSCC_WASD's GitHub release and checked against its SHA-256 and digital signature before installing."),
            AppPaths.Name, MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes) await DownloadAndInstallAsync(package);
    }

    /// <summary>Downloads and verifies the update, then (once the user agrees) stops mapping, installs it and restarts.</summary>
    private async Task DownloadAndInstallAsync(UpdatePackage package)
    {
        string tag = package.Release.Tag;
        _updating = true;
        _updateCts = new CancellationTokenSource();
        NotifyState();
        try
        {
            DownloadedUpdate update;
            UpdateProgress = 0;
            UpdateProgressText = L.T($"正在下載 {tag}…", $"Downloading {tag}…");
            var progress = new Progress<double>(p =>
            {
                if (UpdateProgressText is null) return;
                UpdateProgress = p;
                UpdateProgressText = p >= 1
                    ? L.T($"正在驗證 {tag}…", $"Verifying {tag}…")
                    : L.T($"正在下載 {tag}… {p:P0}", $"Downloading {tag}… {p:P0}");
            });
            try { update = await UpdateInstaller.PrepareAsync(package, progress, _updateCts.Token); }
            catch (OperationCanceledException) when (_updateCts.IsCancellationRequested)
            {
                StatusMessage = L.T("已取消下載更新。", "The update download was cancelled.");
                return;
            }
            catch (UpdateVerificationException ex)
            {
                StatusMessage = L.T($"更新未通過驗證：{ex.Message}", $"The update failed verification: {ex.Message}");
                MessageBox.Show(ex.Message, AppPaths.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException
                                           or IOException or UnauthorizedAccessException)
            {
                StatusMessage = L.T($"無法下載更新：{ex.Message}", $"Could not download the update: {ex.Message}");
                return;
            }
            finally { UpdateProgressText = null; }

            var install = MessageBox.Show(
                L.T($"已下載並驗證 {tag}。要現在安裝並重新啟動 TSCC_WASD 嗎？" +
                    (IsRunning ? "\n\n會先停止映射並還原 HidHide 設定，再更新程式。" : ""),
                    $"{tag} is downloaded and verified. Install it and restart TSCC_WASD now?" +
                    (IsRunning ? "\n\nMapping stops and HidHide's settings are restored before the update is installed." : "")),
                AppPaths.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (install != MessageBoxResult.Yes)
            {
                StatusMessage = L.T($"{tag} 已下載，之後按「檢查更新」即可安裝。", $"{tag} is downloaded; click Check for updates to install it later.");
                return;
            }
            // Never replace the EXE while controllers are hidden: stop mapping and restore HidHide first.
            if (IsRunning || _coordinator.IsRunning) await StopAsync();
            if (_coordinator.IsRunning || _coordinator.HidingSince is not null)
            {
                StatusMessage = L.T("無法停止映射，沒有安裝更新。", "Mapping could not be stopped, so the update was not installed.");
                return;
            }
            StatusMessage = L.T($"正在安裝 {tag}…", $"Installing {tag}…");
            try { UpdateInstaller.Install(update); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusMessage = L.T($"無法安裝更新，已保留目前版本：{ex.Message}", $"Could not install the update; this version was kept: {ex.Message}");
                return;
            }
            try { UpdateInstaller.Restart(); }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
            {
                StatusMessage = L.T($"已安裝 {tag}，但無法自動重新啟動，請關閉後再開啟 TSCC_WASD：{ex.Message}",
                    $"{tag} is installed but TSCC_WASD could not restart itself. Close it and open it again: {ex.Message}");
                return;
            }
            StatusMessage = L.T($"已安裝 {tag}，正在重新啟動…", $"{tag} is installed; restarting…");
            Application.Current?.MainWindow?.Close();
        }
        finally
        {
            _updateCts.Dispose();
            _updateCts = null;
            _updating = false;
            NotifyState();
        }
    }

    private void Reload()
    {
        try
        {
            var profiles = _coordinator.LoadProfiles();
            Profiles.Clear();
            foreach (var profile in profiles) Profiles.Add(profile);
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == Settings.LastProfileId) ?? Profiles.FirstOrDefault();
            _loadFailed = false;
            StatusMessage = L.T("設定檔已載入。", "Profiles loaded.");
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            Profiles.Clear();
            SelectedProfile = null;
            StatusMessage = L.T($"無法讀取設定檔，原始檔案已保留。請開啟設定資料夾修正 profiles.json 後重新載入：{ex.Message}",
                $"Could not read profiles; the file was kept. Open the settings folder, fix profiles.json and reload: {ex.Message}");
        }
        DependencyStatus = DependencyChecker.BuildStatusText();
        NotifyState();
    }

    private void Save()
    {
        _coordinator.SaveProfiles(Profiles);
        StatusMessage = L.T("設定已儲存，前一版保留為 profiles.json.bak。", "Saved. The previous version is kept as profiles.json.bak.");
        System.Windows.Data.CollectionViewSource.GetDefaultView(Profiles).Refresh();
    }

    /// <summary>Starts mapping without user interaction when the settings ask for it.</summary>
    /// <param name="atSignIn">Launched by Windows at sign-in (<c>--startup</c>).</param>
    public Task AutoStartAsync(bool atSignIn)
    {
        if (!atSignIn && !Settings.StartMappingOnLaunch) return Task.CompletedTask;
        if (IsRunning || SelectedProfile is null) return Task.CompletedTask;
        return RunGuardedAsync(() => StartAsync(automatic: true, atSignIn));
    }

    private async Task StartAsync(bool automatic, bool atSignIn)
    {
        if (SelectedProfile is null) return;
        var result = await _coordinator.StartAsync(SelectedProfile, UpdateStatusFromWorker);
        IsRunning = _coordinator.IsRunning;
        StatusMessage = result.message;
        if (!IsRunning) return;
        Settings.LastProfileId = SelectedProfile.Id;
        SaveSettings();
        _hiddenSince = _coordinator.HidingSince;
        if (_hiddenSince is null) return;

        if (_coordinator.SteamSeesPhysicalControllers)
        {
            // Preferred: reconnect the USB controllers so Steam loses them; no restart, no prompt.
            if (ReconnectAvailable)
            {
                await ReconnectControllersAsync();
                return;
            }
            if (automatic && Settings.ManageSteam)
            {
                await RestartSteamAsync(silent: atSignIn);
                return;
            }
            if (automatic) return;
            var answer = MessageBox.Show(
                L.T("Steam 在隱藏實體手把之前就已啟動，仍握有實體手把，會透過 Steam Input 轉給遊戲，造成按鍵圖示在 Xbox／PS 之間交替。\n\n" +
                    "• 是：設定「自動重新連接」（只需一次，會要求一次管理員權限）。之後 TSCC_WASD 會自動重新連接 USB 手把讓 Steam 放開它，不必再重啟 Steam。\n" +
                    "• 否：這次先重新啟動 Steam（請先關閉正在執行的 Steam 遊戲）。\n" +
                    "• 取消：暫不處理。",
                    "Steam started before the physical controllers were hidden, so it still holds them and Steam Input forwards " +
                    "them to games, making prompts alternate between Xbox and PS.\n\n" +
                    "• Yes: set up automatic reconnect (once, with one administrator prompt). From then on TSCC_WASD reconnects USB " +
                    "controllers so Steam lets go of them, with no Steam restart.\n" +
                    "• No: restart Steam this time (close any running Steam game first).\n" +
                    "• Cancel: leave it for now."),
                AppPaths.Name, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                if (await SetUpReconnectHelperAsync(confirm: false)) await ReconnectControllersAsync();
            }
            else if (answer == MessageBoxResult.No) await RestartSteamAsync(silent: false);
            else StatusMessage = L.T($"{result.message} Steam 仍看得到實體手把，可稍後按「重新啟動 Steam」。",
                $"{result.message} Steam can still see the physical controller; click Restart Steam later.");
        }
        else if (automatic && Settings.LaunchSteamAfterAutoStart)
        {
            if (SteamHelper.LaunchIfNotRunning(silent: atSignIn))
                StatusMessage = L.T($"{result.message} 已在隱藏手把後啟動 Steam。", $"{result.message} Steam was started after hiding the controllers.");
        }
    }

    private bool _reconnectedWhileHidden;

    private bool ReconnectAvailable => _reconnectInstalled ??= ControllerReconnector.IsInstalled;

    /// <summary>Sets up the elevated reconnect helper (one administrator prompt). True when it is ready.</summary>
    private async Task<bool> SetUpReconnectHelperAsync(bool confirm = true)
    {
        if (confirm && MessageBox.Show(
                L.T("「自動重新連接」會在 Steam 握著實體手把時，讓 USB 手把斷電重新連接一次（約 1–3 秒），Steam 就會放開它，不必重啟 Steam。\n\n" +
                    $"設定時 Windows 會要求一次管理員權限：TSCC_WASD 會複製到 {ControllerReconnector.HelperDirectory}，並建立一個只在需要時執行的排程工作。" +
                    "之後不再詢問。可隨時在「程式設定」移除。\n\n要現在設定嗎？",
                    "Automatic reconnect power-cycles USB controllers once (about 1–3 seconds) when Steam holds them, so Steam lets go " +
                    "without a restart.\n\nSetting it up asks for administrator rights once: TSCC_WASD is copied to " +
                    $"{ControllerReconnector.HelperDirectory} and an on-demand scheduled task is created. You won't be asked again, " +
                    "and you can remove it any time in App settings.\n\nSet it up now?"),
                AppPaths.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return false;
        StatusMessage = L.T("正在設定自動重新連接（請在 Windows 提示中允許）…", "Setting up automatic reconnect (allow the Windows prompt)…");
        bool ok = await ControllerReconnector.InstallAsync();
        _reconnectInstalled = null;
        RaisePropertyChanged(nameof(ReconnectHelperStatus));
        ok = ok && ReconnectAvailable;
        StatusMessage = ok
            ? L.T("自動重新連接已設定完成，之後不必再重啟 Steam。", "Automatic reconnect is set up; Steam no longer needs restarting.")
            : L.T("沒有完成自動重新連接的設定（已取消或失敗）。可稍後在「程式設定」再試。", "Automatic reconnect was not set up (cancelled or failed). You can try again in App settings.");
        return ok;
    }

    /// <summary>Power-cycles the controllers' USB ports through the helper and reports what happened.</summary>
    private async Task ReconnectControllersAsync(bool afterStop = false)
    {
        StatusMessage = afterStop
            ? L.T("正在重新連接手把，讓 Steam 重新認得它…", "Reconnecting controllers so Steam detects them again…")
            : L.T("正在重新連接 USB 手把，讓 Steam 放開它…", "Reconnecting USB controllers so Steam lets go of them…");
        var result = await ControllerReconnector.ReconnectAsync();
        if (result is null)
        {
            StatusMessage = L.T("自動重新連接沒有回應。可以改按「重新啟動 Steam」，或拔插一次手把。",
                "Automatic reconnect did not respond. Click Restart Steam, or unplug and replug the controller.");
            return;
        }
        if (!afterStop) _reconnectedWhileHidden = true;
        var parts = new List<string>();
        if (result.UsbPorts > 0)
            parts.Add(afterStop
                ? L.T($"已重新連接 {result.UsbPorts} 個 USB 手把，Steam 可以再看到它們。", $"Reconnected {result.UsbPorts} USB controller(s); Steam can see them again.")
                : L.T($"已重新連接 {result.UsbPorts} 個 USB 手把，Steam 不再握著實體手把。", $"Reconnected {result.UsbPorts} USB controller(s); Steam no longer holds them."));
        if (result.WirelessAdapter)
            parts.Add(L.T("無線接收器上的手把會一起重新連線，請稍候幾秒。", "Controllers on the wireless adapter reconnect too; give them a few seconds."));
        if (result.Bluetooth > 0)
            parts.Add(L.T($"{result.Bluetooth} 個藍牙手把無法自動重新連接，請把手把關閉再打開一次。",
                $"{result.Bluetooth} Bluetooth controller(s) can't be reconnected automatically; turn the controller off and on once."));
        if (result.Errors.Count > 0)
            parts.Add(L.T($"有 {result.Errors.Count} 個連接埠失敗，請拔插一次手把或重新啟動 Steam。", $"{result.Errors.Count} port(s) failed; replug the controller or restart Steam."));
        if (parts.Count == 0) parts.Add(L.T("沒有找到需要重新連接的手把。", "No connected controllers needed reconnecting."));
        foreach (var error in result.Errors) AppLog.Warn($"Reconnect: {error}");
        StatusMessage = string.Join(" ", parts);
    }

    private async Task RestartSteamAsync(bool silent)
    {
        StatusMessage = L.T("正在重新啟動 Steam...", "Restarting Steam...");
        await SteamHelper.RestartAsync(silent);
        StatusMessage = L.T("Steam 已重新啟動，現在只看得到虛擬 DS4。", "Steam restarted and now only sees the virtual DS4.");
    }

    private async Task StopAsync()
    {
        try { await _coordinator.StopAsync(); }
        finally { IsRunning = false; }
        StatusMessage = L.T("映射已停止。", "Mapping stopped.");
        await OfferSteamRestartAfterHidingAsync();
    }

    /// <summary>
    /// A Steam that started while controllers were hidden may not notice them once unhidden.
    /// Call after mapping has stopped; asks at most once per hidden period. Steam keeps running either way.
    /// </summary>
    public async Task OfferSteamRestartAfterHidingAsync()
    {
        var since = _hiddenSince;
        bool reconnected = _reconnectedWhileHidden;
        _hiddenSince = null;
        _reconnectedWhileHidden = false;
        if (since is null || !SteamHelper.IsRunning) return;
        // Steam lost the controllers (started while hidden, or reconnected away); a reconnect makes them arrive again.
        if (!SteamHelper.StartedSince(since.Value) && !reconnected) return;
        if (ReconnectAvailable)
        {
            await ReconnectControllersAsync(afterStop: true);
            return;
        }
        if (!SteamHelper.StartedSince(since.Value)) return;
        var answer = MessageBox.Show(
            L.T("Steam 是在實體手把被隱藏期間啟動的，現在可能看不到實體手把。\n\n" +
                "要再重新啟動 Steam 一次，讓它重新認得實體手把嗎？\n（請先關閉正在執行的 Steam 遊戲。）",
                "Steam started while the physical controllers were hidden and may not see them now.\n\n" +
                "Restart Steam once more so it detects them again?\n(Close any running Steam game first.)"),
            "TSCC_WASD", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            StatusMessage = L.T("映射已停止。之後若 Steam 看不到實體手把，重啟 Steam 或重新連接手把即可。",
                "Mapping stopped. If Steam doesn't see the controller later, restart Steam or reconnect the controller.");
            return;
        }
        StatusMessage = L.T("正在重新啟動 Steam...", "Restarting Steam...");
        await SteamHelper.RestartAsync();
        StatusMessage = L.T("映射已停止，Steam 已重新啟動並可看到實體手把。", "Mapping stopped; Steam restarted and can see the controller.");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        InputSummary = _coordinator.InputSummary;
        bool running = _coordinator.IsRunning;
        if (IsRunning && !running)
            StatusMessage = L.T($"映射已結束：{_coordinator.LastError ?? "已停止"}", $"Mapping ended: {_coordinator.LastError ?? "stopped"}");
        IsRunning = running;
        if (running && ++_antiCheatTicks % 20 == 0) CheckAntiCheat(); // Every 5 seconds.
        if (!running) _antiCheatWarned = null;
    }

    private int _antiCheatTicks;
    private string? _antiCheatWarned;

    /// <summary>Warns once per mapping session when an anti-cheat known to reject virtual pads starts.</summary>
    private void CheckAntiCheat()
    {
        var found = AntiCheatWatcher.FindRunning();
        if (found is null || found.ProcessName == _antiCheatWarned) return;
        _antiCheatWarned = found.ProcessName;
        string message = L.T(found.AdviceZh, found.AdviceEn);
        StatusMessage = message;
        BackgroundNotice?.Invoke(message);
    }

    private void UpdateStatusFromWorker(string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
            dispatcher.BeginInvoke(() => { if (!_closing) StatusMessage = message; });
    }

    private void NotifyState()
    {
        RaisePropertyChanged(nameof(CanEdit));
        foreach (var command in _commands) command.RaiseCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _closing = true;
        _updateCts?.Cancel();
        _timer.Stop();
        _timer.Tick -= OnTick;
        NotifyState();
        await _coordinator.DisposeAsync();
    }
}
