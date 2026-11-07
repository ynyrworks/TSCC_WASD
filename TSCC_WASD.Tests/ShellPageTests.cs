using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Xunit;
using TextBlock = System.Windows.Controls.TextBlock;
using TSCC_WASD.App;
using TSCC_WASD.App.ViewModels;

namespace TSCC_WASD.Tests;

public class ShellPageTests
{
    private static Task RunSta(Action body)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { body(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static Border ThemedHost(ApplicationTheme theme)
    {
        var host = new Border();
        host.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = theme });
        host.Resources.MergedDictionaries.Add(new ControlsDictionary());
        return host;
    }

    private static void Layout(Border host)
    {
        host.Measure(new Size(960, 600));
        host.Arrange(new Rect(0, 0, 960, 600));
        host.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        host.UpdateLayout();
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindAll<T>(child)) yield return nested;
        }
    }

    [Fact]
    public async Task PagesScrollWithTheWheelFromAnywhereThroughOneScrollViewer()
    {
        await RunSta(() =>
        {
            var vm = new MainViewModel();
            var host = ThemedHost(ApplicationTheme.Dark);
            var nav = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left };
            nav.SetPageProviderService(new MainWindow.SharedViewModelPages(vm));
            host.Child = nav;
            Layout(host);
            try
            {
                foreach (var pageType in new[] { typeof(TSCC_WASD.App.Pages.HomePage), typeof(TSCC_WASD.App.Pages.ProfilesPage),
                                                  typeof(TSCC_WASD.App.Pages.SettingsPage), typeof(TSCC_WASD.App.Pages.AboutPage) })
                {
                    nav.Navigate(pageType);
                    Layout(host);
                    var page = FindAll<Page>(host).Single();
                    // The navigation view's own content presenter scrolls the page; the page must not add a second one.
                    Assert.IsNotType<ScrollViewer>(page.Content);
                    var outer = FindAll<NavigationViewContentPresenter>(host).Single();
                    var scrollers = FindAll<ScrollViewer>(page).Where(s => s.VerticalScrollBarVisibility is ScrollBarVisibility.Auto or ScrollBarVisibility.Visible).ToList();
                    Assert.Empty(scrollers);
                    var scroller = FindAll<ScrollViewer>(outer).First();
                    if (scroller.ScrollableHeight <= 0) continue;
                    // A wheel turn over page content (not the scroll bar) moves the page.
                    var target = (UIElement)FindAll<TextBlock>(page).First();
                    double before = scroller.VerticalOffset;
                    target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -240)
                    { RoutedEvent = UIElement.MouseWheelEvent, Source = target });
                    Layout(host);
                    Assert.True(scroller.VerticalOffset > before, pageType.Name + " did not scroll with the wheel");
                }
            }
            finally { vm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }

    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task HomePageFitsTheDefaultWindowWidthWithItsControls(ApplicationTheme theme)
    {
        await RunSta(() =>
        {
            var vm = new MainViewModel();
            var host = ThemedHost(theme);
            var nav = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left };
            nav.SetPageProviderService(new MainWindow.SharedViewModelPages(vm));
            host.Child = nav;
            Layout(host);
            try
            {
                nav.Navigate(typeof(TSCC_WASD.App.Pages.HomePage));
                Layout(host);
                var page = FindAll<Page>(host).Single();
                Assert.True(page.ActualWidth <= 960, "Home is wider than the window: " + page.ActualWidth);
                Assert.Equal(3, FindAll<ToggleSwitch>(page).Count());
                Assert.Single(FindAll<DropDownButton>(page));
                var content = (FrameworkElement)page.Content;
                Assert.True(content.ActualWidth <= page.ActualWidth + 0.5);
            }
            finally { vm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }

    [Fact]
    public async Task HomePageShowsTheLiveInputSummaryOnlyWhileRunning()
    {
        await RunSta(() =>
        {
            var vm = new MainViewModel();
            var host = ThemedHost(ApplicationTheme.Dark);
            var nav = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left };
            nav.SetPageProviderService(new MainWindow.SharedViewModelPages(vm));
            host.Child = nav;
            Layout(host);
            try
            {
                nav.Navigate(typeof(TSCC_WASD.App.Pages.HomePage));
                Layout(host);
                var page = FindAll<Page>(host).Single();
                var summary = FindAll<TextBlock>(page).Single(t => t.Name == "InputSummaryText");
                typeof(MainViewModel).GetProperty(nameof(MainViewModel.InputSummary))!.SetValue(vm, "A pressed");
                Assert.Equal(Visibility.Collapsed, summary.Visibility);
                typeof(MainViewModel).GetProperty(nameof(MainViewModel.IsRunning))!.SetValue(vm, true);
                Layout(host);
                Assert.Equal(Visibility.Visible, summary.Visibility);
                Assert.Equal("A pressed", summary.Text);
                Assert.Equal("A pressed", summary.ToolTip);
                Assert.Equal(TextTrimming.CharacterEllipsis, summary.TextTrimming);
                Assert.Contains(FindAll<TextBlock>(page), t => t.Text == vm.StatusMessage);
            }
            finally { vm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }

    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task PageTextStylesUseTheWpfUiThemeBrushes(ApplicationTheme theme)
    {
        await RunSta(() =>
        {
            var host = ThemedHost(theme);
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/TSCC_WASD;component/Themes/Shell.xaml") });
            var panel = new StackPanel();
            var expected = new Dictionary<string, string>
            {
                ["Muted"] = "TextFillColorSecondaryBrush", ["Hint"] = "TextFillColorSecondaryBrush",
                ["Label"] = "TextFillColorPrimaryBrush", ["PageTitle"] = "TextFillColorPrimaryBrush",
            };
            var blocks = expected.Keys.ToDictionary(k => k, k => new TextBlock { Text = k, Style = (Style)host.FindResource(k) });
            foreach (var block in blocks.Values) panel.Children.Add(block);
            host.Child = panel;
            Layout(host);
            foreach (var (key, brushKey) in expected)
            {
                var wanted = (SolidColorBrush)host.FindResource(brushKey);
                var actual = Assert.IsType<SolidColorBrush>(blocks[key].Foreground);
                Assert.Equal(wanted.Color, actual.Color);
                // Never the old palette's blue-grey.
                Assert.NotEqual(Color.FromRgb(0x93, 0xA4, 0xC3), actual.Color);
            }
        });
    }
}
