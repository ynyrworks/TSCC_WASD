using System.Windows;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.App;

public partial class App : Application
{
    private const string ShowEventName = @"Local\TSCC_WASD.Show";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Elevated helper runs (reconnect, set up, remove) do their one job without any window.
        if (TSCC_WASD.Core.Services.Setup.ControllerReconnector.TryRunCommand(e.Args) is { } exitCode)
        {
            Shutdown(exitCode);
            return;
        }
        // After an in-app update the old version is still closing; wait for it before taking the mutex.
        bool updated = UpdateInstaller.FinishUpdate(e.Args);
        _instanceMutex = new Mutex(true, @"Local\TSCC_WASD.Desktop", out bool created);
        if (!created)
        {
            // Bring the running instance forward instead of starting a second mapper.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                using (existing) existing.Set();
            Shutdown();
            return;
        }
        base.OnStartup(e);
        RegisterCrashHandlers();
        AppLog.Prune();
        AppLog.Info($"TSCC_WASD {UpdateChecker.CurrentVersion.ToString(3)} starting; args: {string.Join(' ', e.Args)}");

        // Before the rename this app was YnyrWASD; two mappers would fight over the same controllers.
        if (Mutex.TryOpenExisting(@"Local\" + AppPaths.LegacyName + ".Desktop", out var legacyInstance))
        {
            legacyInstance.Dispose();
            MessageBox.Show(L.T("舊版 YnyrWASD 仍在執行，請先關閉它再開啟 TSCC_WASD。",
                "The old YnyrWASD is still running. Close it before opening TSCC_WASD."), AppPaths.Name);
            Shutdown();
            return;
        }
        bool migrated = AppPaths.MigrateLegacyData();
        var settings = new AppSettingsStore().Load();
        try { StartupRegistration.MigrateLegacy(settings.StartWithWindows, Environment.ProcessPath!); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        L.Apply(settings.Language); // Before any window or view model text is created.
        bool atSignIn = e.Args.Contains(StartupRegistration.StartupArgument, StringComparer.OrdinalIgnoreCase);

        var viewModel = new MainViewModel();
        AppLog.Info(TSCC_WASD.Core.Services.Setup.DiagnosticReport.Build(viewModel.InputSummary, mapping: false).TrimEnd());
        // The status line already narrates every start, stop, error and recovery, so it doubles as the log.
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.StatusMessage)) AppLog.Info(viewModel.StatusMessage);
        };
        if (updated)
            viewModel.ShowNotice(L.T($"已更新到 {viewModel.VersionText}。", $"Updated to {viewModel.VersionText}."));
        if (migrated)
            viewModel.ShowNotice(L.T("已從舊版 YnyrWASD 搬移設定檔、校準與程式設定。", "Profiles, calibration and settings were moved over from YnyrWASD."));
        var window = new MainWindow(viewModel);
        MainWindow = window;
        _tray = new TrayIcon(viewModel, () => window.ShowFromTray(), () => window.Close());
        viewModel.BackgroundNotice += message => _tray?.ShowBalloon(message, warning: true);

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => window.ShowFromTray()), null, Timeout.Infinite, executeOnlyOnce: false);

        if (atSignIn && viewModel.MinimizeToTray)
            _tray.ShowBalloon(L.T("TSCC_WASD 已在背景執行。", "TSCC_WASD is running in the background."));
        else
            window.Show();
        _ = StartAsync(viewModel, atSignIn);
    }

    private static async Task StartAsync(MainViewModel viewModel, bool atSignIn)
    {
        // A silent sign-in launch never pops up installers; mapping reports the missing driver instead.
        if (!atSignIn) await viewModel.OfferMissingDriversAsync();
        await viewModel.AutoStartAsync(atSignIn);
        if (viewModel.CheckUpdatesOnStartup) await viewModel.CheckUpdatesAsync(quiet: true);
    }

    private void RegisterCrashHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception", args.Exception);
            // Keep running: the mapper and HidHide restore live outside the failed UI action.
            args.Handled = true;
            MessageBox.Show(L.T($"發生未預期的錯誤：{args.Exception.Message}\n\n詳細內容已寫入日誌：\n{AppLog.CurrentFile}\n\n" +
                    "回報問題時請附上日誌（關於 → 開啟日誌資料夾）。",
                    $"An unexpected error occurred: {args.Exception.Message}\n\nDetails were written to the log:\n{AppLog.CurrentFile}\n\n" +
                    "Please attach the log when reporting the problem (About → Open log folder)."),
                AppPaths.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled exception; the app is closing", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null) AppLog.Info("TSCC_WASD exiting.");
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _tray?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
