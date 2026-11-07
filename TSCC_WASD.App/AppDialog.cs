using System.Windows;
using TSCC_WASD.Core;

namespace TSCC_WASD.App;

/// <summary>
/// The app's question and message boxes in the WPF-UI look. Same call shape and result type as the native
/// <see cref="MessageBox"/>, and safe to call from any thread and while the main window is hidden in the tray.
/// </summary>
public static class AppDialog
{
    /// <summary>Buttons shown left to right; the first one is the default button.</summary>
    internal static IReadOnlyList<MessageBoxResult> ButtonsFor(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OK => [MessageBoxResult.OK],
        MessageBoxButton.OKCancel => [MessageBoxResult.OK, MessageBoxResult.Cancel],
        MessageBoxButton.YesNo => [MessageBoxResult.Yes, MessageBoxResult.No],
        MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],
        _ => [MessageBoxResult.OK]
    };

    /// <summary>The answer when the dialog is dismissed with Esc or the close button: the cancel button, else the safe "No".</summary>
    internal static MessageBoxResult DismissResult(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OK => MessageBoxResult.OK,
        MessageBoxButton.YesNo => MessageBoxResult.No,
        _ => MessageBoxResult.Cancel
    };

    internal static string ButtonText(MessageBoxResult result) => result switch
    {
        MessageBoxResult.Yes => L.T("是", "Yes"),
        MessageBoxResult.No => L.T("否", "No"),
        MessageBoxResult.Cancel => L.T("取消", "Cancel"),
        _ => L.T("確定", "OK")
    };

    public static MessageBoxResult Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, Window? owner = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            return dispatcher.Invoke(() => ShowOnUiThread(text, caption, buttons, image, owner));
        return ShowOnUiThread(text, caption, buttons, image, owner);
    }

    public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None) => Show(text, caption, buttons, image, owner);

    private static MessageBoxResult ShowOnUiThread(string text, string caption, MessageBoxButton buttons, MessageBoxImage image, Window? owner)
    {
        owner = UsableOwner(owner) ?? UsableOwner(Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive));
        var dialog = new DialogWindow(text, caption, buttons, image);
        // Without a visible owner (hidden in the tray) the dialog stands alone, on top, with a taskbar button.
        if (owner is not null) dialog.Owner = owner;
        else { dialog.Topmost = true; dialog.ShowInTaskbar = true; dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen; }
        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? UsableOwner(Window? window) =>
        window is { IsVisible: true, WindowState: not WindowState.Minimized } ? window : null;
}
