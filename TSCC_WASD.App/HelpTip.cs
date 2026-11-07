using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using TSCC_WASD.Core;

namespace TSCC_WASD.App;

/// <summary>
/// The small "?" next to a control. <c>&lt;app:HelpTip Key="home.main"/&gt;</c> shows the tip from <see cref="HelpTexts"/> as a
/// tooltip on hover and while it has keyboard focus (Tab to it; Esc hides it), and gives a screen reader the same text as its help text.
/// The same key can also be set on the control it explains (<c>app:HelpTip.Key="home.main"</c>), which gives that control the
/// help text too and lets the tests check that every control is covered.
/// </summary>
public sealed class HelpTip : TextBlock
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(HelpTip), new PropertyMetadata(null, OnKeyChanged));

    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);
    public static void SetKey(DependencyObject element, string? value) => element.SetValue(KeyProperty, value);

    /// <summary>The tip's key; the same as the attached property.</summary>
    public string? Key
    {
        get => GetKey(this);
        set => SetKey(this, value);
    }

    public HelpTip()
    {
        Text = "?";
        Focusable = true;
        // The look (colour, size, cursor) lives in Shell.xaml; a dynamic reference keeps it working in any host that merges it.
        SetResourceReference(StyleProperty, "HelpMark");
        GotKeyboardFocus += (_, _) => Show(true);
        LostKeyboardFocus += (_, _) => Show(false);
        KeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            Show(false);
            e.Handled = true;
        };
    }

    private void Show(bool open)
    {
        if (ToolTip is ToolTip tip && (!open || IsKeyboardFocused)) tip.IsOpen = open;
    }

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = HelpTexts.Get(e.NewValue as string ?? "");
        if (text.Length == 0) return;
        AutomationProperties.SetHelpText(d, text);
        switch (d)
        {
            case HelpTip tip:
                tip.ToolTip = new ToolTip { MaxWidth = 340, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
                ToolTipService.SetShowDuration(tip, 30000);
                AutomationProperties.SetName(tip, L.T("說明", "Help"));
                break;
            case MenuItem item when item.ToolTip is null:
                item.ToolTip = new ToolTip { MaxWidth = 340, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
                break;
        }
    }
}
