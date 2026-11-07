using System.ComponentModel;
using System.Windows;
using TSCC_WASD.App.Pages;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TSCC_WASD.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _closing;
    private bool _readyToClose;

    public MainWindow() : this(new MainViewModel()) { }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        // Follows the Windows light/dark setting, also when it changes while the app runs. Registered once the window
        // really has a handle: the watcher re-applies the theme to every FluentWindow, which breaks tests that build windows on several threads.
        SourceInitialized += (_, _) => SystemThemeWatcher.Watch(this);
        InitializeComponent();
        DataContext = _viewModel;
        RootNavigation.SetPageProviderService(new SharedViewModelPages(_viewModel));
        Loaded += (_, _) => RootNavigation.Navigate(typeof(HomePage));
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    /// <summary>Creates each page once and gives all of them the window's view model.</summary>
    internal sealed class SharedViewModelPages(MainViewModel viewModel) : INavigationViewPageProvider
    {
        private readonly Dictionary<Type, object> _pages = [];

        public object? GetPage(Type pageType)
        {
            if (!_pages.TryGetValue(pageType, out var page))
            {
                var created = (FrameworkElement)Activator.CreateInstance(pageType)!;
                created.DataContext = viewModel;
                _pages[pageType] = page = created;
            }
            return page;
        }
    }

    /// <summary>Restores the window from the notification area (or brings it forward).</summary>
    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Mapping keeps running; the tray icon brings the window back.
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
        {
            _viewModel.FlushProfiles(); // pending profile edits reach the disk before the window goes to the tray
            Hide();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_readyToClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        IsEnabled = false;
        try
        {
            await _viewModel.DisposeAsync();
            // Controllers are unhidden now; Steam keeps running either way.
            await _viewModel.OfferSteamRestartAfterHidingAsync();
        }
        catch (Exception ex)
        {
            AppDialog.Show(ex.Message, L.T("清理控制器時發生錯誤", "Error while cleaning up controllers"));
        }
        finally
        {
            _readyToClose = true;
            // Cleanup may finish synchronously; closing again inside Closing throws.
            _ = Dispatcher.BeginInvoke(Close);
        }
    }
}
