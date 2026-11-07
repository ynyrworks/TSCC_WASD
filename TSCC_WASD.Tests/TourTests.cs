using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Xunit;
using Button = System.Windows.Controls.Button;
using TSCC_WASD.App;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

[Collection(WpfUiCollection.Name)]
public class TourTests
{
    private static TourController NewTour(out Counter done, (bool, bool)? drivers = null)
    {
        var counter = new Counter();
        done = counter;
        return new TourController(TourSteps.Build(), () => counter.Value++, () => drivers ?? (true, true));
    }

    private sealed class Counter { public int Value; }

    private static string TempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void TheTourHasEightStepsWithUniqueIdsAndText()
    {
        var steps = TourSteps.Build();
        Assert.Equal(["welcome", "drivers", "controller", "output", "start", "steam", "profiles", "done"], steps.Select(s => s.Id));
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Title)));
        Assert.All(steps.Where(s => s.Kind == TourStepKind.Plain), s => Assert.False(string.IsNullOrWhiteSpace(s.Body)));
    }

    [Fact]
    public void NextAndBackWalkTheStepsAndTheCounterFollows()
    {
        var tour = NewTour(out var done);
        Assert.False(tour.IsActive);
        tour.Start();
        Assert.True(tour.IsActive);
        Assert.Equal(0, tour.Index);
        Assert.False(tour.CanGoBack);
        Assert.True(tour.ShowSkip);
        tour.Back(); // nothing before the first step
        Assert.Equal(0, tour.Index);
        tour.Next();
        tour.Next();
        Assert.Equal(2, tour.Index);
        Assert.Contains("3", tour.StepText);
        Assert.Contains("8", tour.StepText);
        Assert.True(tour.CanGoBack);
        tour.Back();
        Assert.Equal(1, tour.Index);
        Assert.Equal(0, done.Value);
    }

    [Fact]
    public void FinishingOnTheLastStepClosesTheTourOnceAndHidesSkip()
    {
        var tour = NewTour(out var done);
        tour.Start();
        for (int i = 0; i < tour.Count - 1; i++) tour.Next();
        Assert.True(tour.IsLast);
        Assert.False(tour.ShowSkip);
        Assert.True(tour.IsActive);
        tour.Next();
        Assert.False(tour.IsActive);
        Assert.Equal(1, done.Value);
        tour.Next(); tour.Skip(); tour.Finish();
        Assert.Equal(1, done.Value);
    }

    [Fact]
    public void SkipAndEscapeCloseTheTourAsDone()
    {
        var tour = NewTour(out var done);
        Assert.False(tour.Escape()); // not open: Esc is left alone
        tour.Start();
        tour.Next();
        tour.Skip();
        Assert.False(tour.IsActive);
        Assert.Equal(1, done.Value);

        tour.Start();
        Assert.Equal(0, tour.Index); // replay starts over
        Assert.True(tour.Escape());
        Assert.False(tour.IsActive);
        Assert.Equal(2, done.Value);
    }

    [Fact]
    public void TheStepChangedEventFiresOnStartNextAndBack()
    {
        var tour = NewTour(out _);
        var seen = new List<string>();
        tour.StepChanged += () => seen.Add(tour.Step.Id);
        tour.Start(); tour.Next(); tour.Back();
        Assert.Equal(["welcome", "drivers", "welcome"], seen);
    }

    [Fact]
    public void TheDriversStepOffersInstallOnlyWhileADriverIsMissing()
    {
        bool vigem = false, hidhide = true;
        var tour = new TourController(TourSteps.Build(), () => { }, () => (vigem, hidhide));
        tour.Start();
        Assert.False(tour.ShowInstall); // welcome step
        tour.Next();
        Assert.Equal("drivers", tour.Step.Id);
        Assert.True(tour.ShowInstall);
        Assert.Contains("ViGEmBus", tour.Body);
        Assert.DoesNotContain("HidHide 未安裝", tour.Body);
        vigem = true; // the install finished
        tour.RefreshDrivers();
        Assert.False(tour.ShowInstall);
        Assert.DoesNotContain("尚未安裝", tour.Body);
        Assert.DoesNotContain("not installed", tour.Body);
        hidhide = false;
        tour.RefreshDrivers();
        Assert.True(tour.ShowInstall);
        Assert.Contains("HidHide", tour.Body);
    }

    [Theory]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(true, false, true, false, false, false)]   // already done
    [InlineData(false, true, true, false, false, false)]   // already showing
    [InlineData(false, false, false, false, false, false)] // window hidden in the tray
    [InlineData(false, false, true, true, false, false)]   // minimized
    [InlineData(false, false, true, false, true, false)]   // a dialog is up
    public void TheTourOpensOnlyForAVisibleUnobstructedWindowThatHasNotSeenIt(bool completed, bool active, bool visible, bool minimized, bool dialog, bool expected) =>
        Assert.Equal(expected, TourGate.CanAutoStart(completed, active, visible, minimized, dialog));

    [Fact]
    public void ADoneFlagMissingFromAnOldSettingsFileMeansTheTourShows()
    {
        string path = TempSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"language\":\"en\",\"minimizeToTray\":false}");
        var loaded = new AppSettingsStore(path).Load();
        Assert.False(loaded.TourCompleted);
        Assert.False(loaded.MinimizeToTray);
    }

    [Fact]
    public async Task TheDoneFlagIsSavedOnceAndSurvivesARestart()
    {
        await ShellPageTests.RunSta(() =>
        {
            string path = TempSettingsPath();
            var store = new AppSettingsStore(path);
            var vm = new MainViewModel(settingsStore: store);
            try
            {
                Assert.False(vm.TourCompleted);
                var tour = new TourController(TourSteps.Build(), vm.MarkTourCompleted, () => (true, true));
                tour.Start();
                tour.Next();
                Assert.False(store.Load().TourCompleted); // not saved before the tour ends
                tour.Skip();
                Assert.True(vm.TourCompleted);
                Assert.True(new AppSettingsStore(path).Load().TourCompleted);
                Assert.True(new MainViewModel(settingsStore: new AppSettingsStore(path)).TourCompleted);
            }
            finally { vm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }

    [Fact]
    public async Task TheReplayCommandAsksTheWindowToRestartTheTour()
    {
        await ShellPageTests.RunSta(() =>
        {
            var vm = new MainViewModel(settingsStore: new AppSettingsStore(TempSettingsPath()));
            try
            {
                int requests = 0;
                vm.TourRequested += () => requests++;
                Assert.True(vm.ReplayTourCommand.CanExecute(null));
                vm.ReplayTourCommand.Execute(null);
                Assert.Equal(1, requests);
            }
            finally { vm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }

    [Fact]
    public void TheCardGoesBelowTheTargetThenAboveThenBesideItAndStaysInsideTheArea()
    {
        var area = new Size(900, 500);
        var card = new Size(340, 200);
        Assert.Equal(new Point(100, 164), TourAnchors.CardPosition(new Rect(100, 50, 300, 100), card, area));       // below
        Assert.Equal(new Point(100, 186), TourAnchors.CardPosition(new Rect(100, 400, 300, 100), card, area));      // above
        Assert.Equal(new Point(414, 8), TourAnchors.CardPosition(new Rect(10, 0, 390, 500), card, area));           // right
        var at = TourAnchors.CardPosition(new Rect(0, 0, 900, 500), card, area);                                    // covers everything
        Assert.InRange(at.X, 8, 900 - 340 - 8);
        Assert.InRange(at.Y, 8, 500 - 200 - 8);
        var edge = TourAnchors.CardPosition(new Rect(800, 50, 90, 40), card, area);                                 // near the right edge
        Assert.True(edge.X + 340 <= 900 - 8);
        Assert.Equal(new Point(280, 150), TourAnchors.Centered(card, area));
    }

    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task EveryStepsTargetsExistAndAreShownOnTheirPageAndTheOverlayHighlightsThem(ApplicationTheme theme)
    {
        await ShellPageTests.RunSta(() =>
        {
            var vm = new MainViewModel(settingsStore: new AppSettingsStore(TempSettingsPath()));
            try
            {
                var window = new MainWindow(vm);
                var content = (Grid)window.Content;
                window.Content = null;
                var host = ShellPageTests.ThemedHost(theme);
                host.Child = content;
                ShellPageTests.Layout(host);
                // The overlay draws with these theme brushes.
                Assert.NotNull(host.FindResource("SystemAccentColorPrimaryBrush"));
                Assert.NotNull(host.FindResource("ApplicationBackgroundBrush"));
                var nav = content.Children.OfType<NavigationView>().Single();
                var overlay = content.Children.OfType<TourOverlay>().Single();
                Page? CurrentPage() => ShellPageTests.FindAll<Page>(host).SingleOrDefault();
                var tour = new TourController(TourSteps.Build(), () => { }, () => (false, true));
                overlay.Attach(tour, vm.InstallDepsCommand, step => TourAnchors.Find(step, window, CurrentPage()));
                tour.Start();
                for (int i = 0; i < tour.Count; i++, tour.Next())
                {
                    var step = tour.Step;
                    nav.Navigate(step.Page);
                    ShellPageTests.Layout(host);
                    var found = TourAnchors.Find(step, window, CurrentPage());
                    Assert.Equal(step.Targets.Length, found.Count);
                    Assert.All(found, e => Assert.True(TourAnchors.IsShown(e), $"{step.Id}: {e.Name} is not shown"));
                    overlay.Place(bringIntoView: true);
                    ShellPageTests.Layout(host);
                    Assert.Equal(step.Targets.Length > 0 ? Visibility.Visible : Visibility.Collapsed, overlay.HighlightElement.Visibility);
                    double left = Canvas.GetLeft(overlay.CardElement), top = Canvas.GetTop(overlay.CardElement);
                    Assert.True(left >= 0 && top >= 0 && left + overlay.CardElement.ActualWidth <= overlay.ActualWidth + 0.5
                        && top + overlay.CardElement.ActualHeight <= overlay.ActualHeight + 0.5, $"{step.Id}: card outside the window");
                }
                Assert.Equal("done", tour.Step.Id);
            }
            finally
            {
                vm.DisposeAsync().AsTask().GetAwaiter().GetResult();
                ThemeEventCleanup.DropSubscribers();
            }
        });
    }

    [Fact]
    public async Task TheCardButtonsDriveTheTourAreKeyboardReachableAndNamed()
    {
        await ShellPageTests.RunSta(() =>
        {
            var vm = new MainViewModel(settingsStore: new AppSettingsStore(TempSettingsPath()));
            try
            {
                var host = ShellPageTests.ThemedHost(ApplicationTheme.Dark);
                var overlay = new TourOverlay();
                host.Child = overlay;
                int done = 0;
                var tour = new TourController(TourSteps.Build(), () => done++, () => (true, true));
                overlay.Attach(tour, vm.InstallDepsCommand, _ => []);
                ShellPageTests.Layout(host);
                Assert.Equal(Visibility.Collapsed, overlay.Visibility);
                tour.Start();
                ShellPageTests.Layout(host);
                Assert.Equal(Visibility.Visible, overlay.Visibility);
                var buttons = ShellPageTests.FindAll<ButtonBase>(overlay).ToList();
                Assert.All(buttons, b => { Assert.True(b.Focusable); Assert.True(b.IsTabStop); });
                Assert.False(string.IsNullOrEmpty((string)overlay.CardElement.GetValue(System.Windows.Automation.AutomationProperties.NameProperty)));
                Click(overlay.NextElement);
                Assert.Equal(1, tour.Index);
                Click(ButtonNamed(overlay, "BackButton"));
                Assert.Equal(0, tour.Index);
                Click(ButtonNamed(overlay, "SkipButton"));
                Assert.False(tour.IsActive);
                Assert.Equal(1, done);
                Assert.Equal(Visibility.Collapsed, overlay.Visibility);
            }
            finally
            {
                vm.DisposeAsync().AsTask().GetAwaiter().GetResult();
                ThemeEventCleanup.DropSubscribers();
            }
        });
    }

    [Fact]
    public async Task TheInstallButtonShowsOnlyWhenADriverIsMissingAndUsesTheExistingCommand()
    {
        await ShellPageTests.RunSta(() =>
        {
            var vm = new MainViewModel(settingsStore: new AppSettingsStore(TempSettingsPath()));
            try
            {
                var host = ShellPageTests.ThemedHost(ApplicationTheme.Dark);
                var overlay = new TourOverlay();
                host.Child = overlay;
                bool missing = true;
                var tour = new TourController(TourSteps.Build(), () => { }, () => (!missing, true));
                overlay.Attach(tour, vm.InstallDepsCommand, _ => []);
                tour.Start();
                tour.Next();
                ShellPageTests.Layout(host);
                var install = ButtonNamed(overlay, "InstallButton");
                Assert.Equal(Visibility.Visible, install.Visibility);
                Assert.Same(vm.InstallDepsCommand, install.Command);
                missing = false;
                tour.RefreshDrivers();
                ShellPageTests.Layout(host);
                Assert.Equal(Visibility.Collapsed, install.Visibility);
            }
            finally
            {
                vm.DisposeAsync().AsTask().GetAwaiter().GetResult();
                ThemeEventCleanup.DropSubscribers();
            }
        });
    }

    private static Button ButtonNamed(TourOverlay overlay, string name) =>
        ShellPageTests.FindAll<Button>(overlay).Single(b => b.Name == name);

    private static void Click(ButtonBase button) =>
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
}
