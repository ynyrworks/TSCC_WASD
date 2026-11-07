using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TSCC_WASD.App.Pages;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services.Setup;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TSCC_WASD.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _closing;
    private bool _readyToClose;
    private readonly SharedViewModelPages _pages;
    private readonly TourController _tour;
    private readonly DispatcherTimer _tourTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow() : this(new MainViewModel()) { }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        // Follows the Windows light/dark setting, also when it changes while the app runs. Registered once the window
        // really has a handle: the watcher re-applies the theme to every FluentWindow, which breaks tests that build windows on several threads.
        SourceInitialized += (_, _) => SystemThemeWatcher.Watch(this);
        InitializeComponent();
        DataContext = _viewModel;
        _pages = new SharedViewModelPages(_viewModel);
        RootNavigation.SetPageProviderService(_pages);
        _tour = new TourController(TourSteps.Build(), OnTourDone,
            () => (DependencyChecker.IsViGEmInstalled(), DependencyChecker.IsHidHideInstalled()));
        Tour.Attach(_tour, _viewModel.InstallDepsCommand, step => TourAnchors.Find(step, this, _pages.GetPage(step.Page) as FrameworkElement));
        _tour.StepChanged += OnTourStepChanged;
        _viewModel.TourRequested += StartTour;
        _viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.DriverSummary)) _tour.RefreshDrivers(); };
        // The tour opens by itself once the window is really on screen and no dialog is up; at a silent sign-in launch the
        // window stays in the tray, so this waits (and keeps checking) until it is first shown.
        _tourTimer.Tick += (_, _) => TryAutoStartTour();
        IsVisibleChanged += (_, _) => TryAutoStartTour();
        Closed += (_, _) => _tourTimer.Stop();
        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(typeof(HomePage));
            if (!_viewModel.TourCompleted) _tourTimer.Start();
            TryAutoStartTour();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _tour.Escape()) e.Handled = true; };
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private bool DialogOpen() => Application.Current?.Windows.OfType<Window>()
        .Any(w => w != this && w.IsVisible && (w is DialogWindow || w.Owner == this)) == true;

    private void TryAutoStartTour()
    {
        if (_viewModel.TourCompleted) { _tourTimer.Stop(); return; }
        if (!IsLoaded) return;
        if (!TourGate.CanAutoStart(_viewModel.TourCompleted, _tour.IsActive, IsVisible, WindowState == WindowState.Minimized, DialogOpen())) return;
        _tourTimer.Stop();
        StartTour();
    }

    private void StartTour() => _tour.Start();

    private void OnTourStepChanged()
    {
        NavigateTo(_tour.Step.Page);
        Tour.ShowStep();
    }

    private void OnTourDone()
    {
        _tourTimer.Stop();
        _viewModel.MarkTourCompleted();
        NavigateTo(typeof(HomePage));
    }

    private void NavigateTo(Type pageType)
    {
        if (RootNavigation.SelectedItem is NavigationViewItem { TargetPageType: { } current } && current == pageType) return;
        RootNavigation.Navigate(pageType);
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
