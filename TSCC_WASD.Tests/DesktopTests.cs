using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;
using TSCC_WASD.App;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core.Services;

namespace TSCC_WASD.Tests;

public class DesktopTests
{
    [Fact]
    public async Task EditorCommandsAndWindowBindingsWorkWithoutDrivers()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "TSCC_WASD-tests", Guid.NewGuid().ToString("N"));
            var errors = new StringWriter();
            using var listener = new TextWriterTraceListener(errors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            MainViewModel? vm = null;
            try
            {
                var store = new ProfileStore(Path.Combine(dir, "profiles.json"));
                // Screenshot runs pick the UI language; normal runs keep the default.
                if (Environment.GetEnvironmentVariable("YNYRWASD_LANGUAGE") is { Length: > 0 } language)
                    TSCC_WASD.Core.L.Apply(language);
                vm = new MainViewModel(store, new AppSettingsStore(Path.Combine(dir, "settings.json")));
                Assert.Single(vm.Profiles);
                vm.NewCommand.Execute(null);
                Assert.Equal(2, vm.Profiles.Count);
                string profileName = TSCC_WASD.Core.L.T("測試設定", "Test profile");
                vm.SelectedProfile!.Name = profileName;
                vm.SelectedProfile.DeadZone = 0.2;
                vm.SelectedProfile.InputType = TSCC_WASD.Core.Models.InputDeviceType.Switch2ProUsb;
                vm.SaveCommand.Execute(null);
                Assert.Equal(profileName, store.LoadProfiles()[1].Name);
                Assert.Equal(0.2, store.LoadProfiles()[1].DeadZone);
                Assert.Equal(TSCC_WASD.Core.Models.InputDeviceType.Switch2ProUsb, store.LoadProfiles()[1].InputType);
                vm.ReloadCommand.Execute(null);
                vm.DeleteCommand.Execute(null);
                Assert.Single(vm.Profiles);
                Assert.False(vm.DeleteCommand.CanExecute(null));
                Assert.True(vm.StartCommand.CanExecute(null));
                Assert.False(vm.StopCommand.CanExecute(null));

                var window = new MainWindow(vm);
                // Four navigation entries, About & help in the footer; every page binds to the same view model.
                var nav = ((System.Windows.Controls.Grid)window.Content).Children.OfType<Wpf.Ui.Controls.NavigationView>().Single();
                Assert.Equal(new[] { typeof(TSCC_WASD.App.Pages.HomePage), typeof(TSCC_WASD.App.Pages.ProfilesPage), typeof(TSCC_WASD.App.Pages.SettingsPage) },
                    nav.MenuItems.Cast<Wpf.Ui.Controls.NavigationViewItem>().Select(i => i.TargetPageType).ToArray());
                Assert.Equal(typeof(TSCC_WASD.App.Pages.AboutPage),
                    nav.FooterMenuItems.Cast<Wpf.Ui.Controls.NavigationViewItem>().Single().TargetPageType);
                var pages = new System.Windows.Controls.Page[]
                {
                    new TSCC_WASD.App.Pages.HomePage(), new TSCC_WASD.App.Pages.ProfilesPage(),
                    new TSCC_WASD.App.Pages.SettingsPage(), new TSCC_WASD.App.Pages.AboutPage()
                };
                // The provider MainWindow really uses: one page instance per type, all bound to the shared view model.
                var provider = new MainWindow.SharedViewModelPages(vm);
                foreach (var pageType in nav.MenuItems.Cast<object>().Concat(nav.FooterMenuItems.Cast<object>()).Cast<Wpf.Ui.Controls.NavigationViewItem>().Select(i => i.TargetPageType!))
                {
                    var served = Assert.IsAssignableFrom<FrameworkElement>(provider.GetPage(pageType));
                    Assert.IsType(pageType, served);
                    Assert.Same(vm, served.DataContext);
                    Assert.Same(served, provider.GetPage(pageType));
                }
                string? screenshotDir = Environment.GetEnvironmentVariable("YNYRWASD_SCREENSHOTS");
                var surfaces = new List<System.Windows.Controls.Border>();
                foreach (var page in pages)
                {
                    // A Page only lives inside a Window or Frame, so lay out its content on a plain surface.
                    var content = (UIElement)page.Content!;
                    page.Content = null;
                    var surface = new System.Windows.Controls.Border { Child = content, Resources = page.Resources, DataContext = vm };
                    surface.Measure(new Size(760, 600));
                    surface.Arrange(new Rect(0, 0, 760, 600));
                    surface.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    surfaces.Add(surface);
                }
                var profilesSurface = surfaces[1];
                // The game-match text box edits the selected profile immediately, and the picker command exists.
                var matchBox = FindAll<System.Windows.Controls.TextBox>(profilesSurface)
                    .Single(t => (string)t.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == TSCC_WASD.Core.L.T("遊戲程序名稱", "Game process name"));
                matchBox.Text = "Game.exe";
                Assert.Equal("Game.exe", vm.SelectedProfile!.MatchProcessName);
                vm.SelectedProfile.MatchProcessName = "Other";
                Assert.Equal("Other", matchBox.Text);
                Assert.True(vm.PickProcessCommand.CanExecute(null));
                matchBox.Text = "";
                // Every command the old single column offered is still bound somewhere in the pages.
                var bound = surfaces.SelectMany(FindAll<System.Windows.Controls.Primitives.ButtonBase>)
                    .Select(b => b.Command).Where(c => c is not null).ToHashSet();
                foreach (var command in new System.Windows.Input.ICommand[]
                {
                    vm.StartCommand, vm.PauseCommand, vm.StopCommand, vm.RestartSteamCommand, vm.InstallDepsCommand,
                    vm.PickProcessCommand, vm.CalibrateCommand, vm.OpenControllerTestCommand, vm.SaveCommand, vm.NewCommand,
                    vm.DeleteCommand, vm.ImportCommand, vm.ExportCommand, vm.ReloadCommand, vm.OpenProfilesCommand,
                    vm.SetupReconnectCommand, vm.RemoveReconnectCommand, vm.OpenHelpCommand, vm.CheckUpdatesCommand,
                    vm.CancelUpdateCommand, vm.AboutCommand
                })
                    Assert.Contains(command, bound);
                if (!string.IsNullOrEmpty(screenshotDir))
                {
                    Directory.CreateDirectory(screenshotDir);
                    string[] names = { "home", "profiles", "settings", "about-page" };
                    for (int i = 0; i < surfaces.Count; i++)
                    {
                        var bitmap = new RenderTargetBitmap(760, 600, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(surfaces[i]);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(screenshotDir, names[i] + ".png"));
                        encoder.Save(stream);
                    }
                }
                // About binds the diagnostics, log and Ko-fi commands to the same view model.
                var about = new AboutWindow { DataContext = vm };
                var aboutContent = (FrameworkElement)about.Content;
                about.Content = null;
                var aboutSurface = new System.Windows.Controls.Border
                {
                    Child = aboutContent, Background = about.Background,
                    Resources = about.Resources, DataContext = vm, Width = 460
                };
                System.Windows.Documents.TextElement.SetForeground(aboutSurface, about.Foreground);
                aboutSurface.Measure(new Size(460, double.PositiveInfinity));
                aboutSurface.Arrange(new Rect(aboutSurface.DesiredSize));
                aboutSurface.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(vm.CopyDiagnosticsCommand.CanExecute(null));
                Assert.True(vm.SupportCommand.CanExecute(null));
                if (!string.IsNullOrEmpty(screenshotDir))
                {
                    var aboutBitmap = new RenderTargetBitmap(460, (int)Math.Ceiling(aboutSurface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    aboutBitmap.Render(aboutSurface);
                    var aboutEncoder = new PngBitmapEncoder();
                    aboutEncoder.Frames.Add(BitmapFrame.Create(aboutBitmap));
                    using var stream = File.Create(Path.Combine(screenshotDir, "about.png"));
                    aboutEncoder.Save(stream);
                }
                about.Close();

                Assert.Equal("", errors.ToString());
                window.Close();
                completion.TrySetResult();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally
            {
                vm?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // Generous: a cold GitHub runner once needed over 20 s for the first WPF render.
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
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
    public async Task ProcessPickerSortsFiltersAndEnumerates()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ProcessPickerWindow(new[]
                {
                    new ProcessPickerWindow.ProcessEntry("zeta", "Z"), new ProcessPickerWindow.ProcessEntry("Alpha", "A game")
                });
                var list = (System.Windows.Controls.ListBox)window.FindName("ProcessList");
                var names = list.Items.Cast<ProcessPickerWindow.ProcessEntry>().Select(e => e.Name).ToArray();
                Assert.Equal(new[] { "Alpha", "zeta" }, names);
                ((System.Windows.Controls.TextBox)window.FindName("FilterBox")).Text = "ZET";
                Assert.Single(list.Items);
                Assert.Null(window.SelectedProcessName);
                Assert.DoesNotContain(ProcessPickerWindow.EnumerateWindowedProcesses(), e => e.Name.Equals("TSCC_WASD", StringComparison.OrdinalIgnoreCase));
                window.Close();
                completion.TrySetResult();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task CalibrationWizardLoadsAndRenders()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            CalibrationWindow? window = null;
            try
            {
                // Without an NS2 Pro attached the wizard waits on its first step; it must still load.
                window = new CalibrationWindow();
                var rangePanel = (FrameworkElement)window.FindName("RangePanel");
                var content = (FrameworkElement)window.Content;
                window.Content = null;
                var surface = new System.Windows.Controls.Border { Child = content, Background = window.Background, Resources = window.Resources };
                System.Windows.Documents.TextElement.SetForeground(surface, window.Foreground);
                surface.Measure(new Size(560, double.PositiveInfinity));
                surface.Arrange(new Rect(new Point(), surface.DesiredSize));
                surface.UpdateLayout();
                Assert.True(surface.ActualHeight > 100);
                string? screenshotDir = Environment.GetEnvironmentVariable("YNYRWASD_SCREENSHOTS");
                if (!string.IsNullOrEmpty(screenshotDir))
                {
                    // Show the second step so the screenshot includes the progress bars.
                    rangePanel.Visibility = Visibility.Visible;
                    surface.Measure(new Size(560, double.PositiveInfinity));
                    surface.Arrange(new Rect(new Point(), surface.DesiredSize));
                    surface.UpdateLayout();
                    Directory.CreateDirectory(screenshotDir);
                    var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(screenshotDir, "calibration.png"));
                    encoder.Save(stream);
                }
                window.Close();
                completion.TrySetResult();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // Generous: a cold GitHub runner once needed over 20 s for the first WPF render.
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }
}
