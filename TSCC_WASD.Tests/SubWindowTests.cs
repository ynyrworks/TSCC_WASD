using System.Windows;
using System.Windows.Controls;
using Xunit;
using TSCC_WASD.App;
using Button = System.Windows.Controls.Button;

namespace TSCC_WASD.Tests;

[Collection(WpfUiCollection.Name)]
public class SubWindowTests
{
    [Theory]
    [InlineData(MessageBoxButton.OK, new[] { MessageBoxResult.OK }, MessageBoxResult.OK)]
    [InlineData(MessageBoxButton.OKCancel, new[] { MessageBoxResult.OK, MessageBoxResult.Cancel }, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.YesNo, new[] { MessageBoxResult.Yes, MessageBoxResult.No }, MessageBoxResult.No)]
    [InlineData(MessageBoxButton.YesNoCancel, new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel }, MessageBoxResult.Cancel)]
    public void ButtonSetsMapToTheirAnswersAndDismissAnswer(MessageBoxButton set, MessageBoxResult[] answers, MessageBoxResult dismissed)
    {
        Assert.Equal(answers, AppDialog.ButtonsFor(set));
        Assert.Equal(dismissed, AppDialog.DismissResult(set));
    }

    [Theory]
    [InlineData(MessageBoxButton.YesNo, 0, MessageBoxResult.Yes)]
    [InlineData(MessageBoxButton.YesNo, 1, MessageBoxResult.No)]
    [InlineData(MessageBoxButton.YesNoCancel, 0, MessageBoxResult.Yes)]
    [InlineData(MessageBoxButton.YesNoCancel, 1, MessageBoxResult.No)]
    [InlineData(MessageBoxButton.YesNoCancel, 2, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.OKCancel, 0, MessageBoxResult.OK)]
    [InlineData(MessageBoxButton.OKCancel, 1, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.OK, 0, MessageBoxResult.OK)]
    public async Task ClickingAButtonSetsThatResultWithoutShowingTheWindow(MessageBoxButton set, int index, MessageBoxResult expected)
    {
        await ShellPageTests.RunSta(() =>
        {
            var dialog = new DialogWindow("text", "caption", set, MessageBoxImage.Question);
            Assert.Equal(AppDialog.ButtonsFor(set).Count, dialog.Buttons.Count);
            Assert.True(dialog.Buttons[0].IsDefault);
            Assert.Equal(AppDialog.DismissResult(set), dialog.Result); // Closed without a click.
            dialog.Buttons[index].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ThemeEventCleanup.DropSubscribers();
            Assert.Equal(expected, dialog.Result);
        });
    }

    [Fact]
    public async Task EveryWindowLoadsOffScreen()
    {
        await ShellPageTests.RunSta(() =>
        {
            var windows = new Window[]
            {
                new CalibrationWindow(),
                new ControllerTestWindow(),
                new ProcessPickerWindow([new ProcessPickerWindow.ProcessEntry("notepad", "Untitled")]),
                new DialogWindow("message", "caption", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning)
            };
            foreach (var window in windows)
            {
                Assert.IsAssignableFrom<Wpf.Ui.Controls.FluentWindow>(window);
                Assert.NotNull(window.Content);
                window.Close();
                ThemeEventCleanup.DropSubscribers();
            }
        });
    }
}
