using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;
using TSCC_WASD.App;
using TSCC_WASD.App.Pages;

namespace TSCC_WASD.Tests;

/// <summary>Every button and option on the pages and sub-windows has a "?" tip, and the tip texts exist in both languages.</summary>
[Collection(WpfUiCollection.Name)]
public class HelpTipTests
{
    /// <summary>
    /// Controls that need no "?" (by x:Name): the Cancel and Close buttons and the picker's Select button say what they do.
    /// Anything else interactive without a tip fails the coverage test, so a new control must get a tip or be listed here.
    /// </summary>
    private static readonly string[] Excluded = ["CancelUpdateButton", "CancelButton", "CloseButton", "OkButton"];

    private static bool IsInteractive(object element) =>
        element is ButtonBase or ComboBox or Slider or TextBox or ListBox or MenuItem;

    /// <summary>Every element in the logical tree, plus the menus that drop-down buttons open (they are not children).</summary>
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        if (root is Wpf.Ui.Controls.TitleBar) yield break; // the window's own caption buttons
        var children = LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToList();
        if (root is Wpf.Ui.Controls.DropDownButton { Flyout: ContextMenu menu }) children.Add(menu);
        if (root is ContextMenu contextMenu) children.AddRange(contextMenu.Items.OfType<DependencyObject>());
        foreach (var child in children)
            foreach (var nested in Walk(child))
                yield return nested;
    }

    private static bool InMenuItem(DependencyObject element)
    {
        for (var node = LogicalTreeHelper.GetParent(element); node is not null; node = LogicalTreeHelper.GetParent(node))
            if (node is MenuItem) return true;
        return false;
    }

    private static string Describe(DependencyObject element) =>
        element.GetType().Name + (element is FrameworkElement { Name.Length: > 0 } named ? " " + named.Name : "")
        + (AutomationProperties.GetName(element) is { Length: > 0 } name ? " \"" + name + "\"" : "");

    private static void Check(string where, DependencyObject root, HashSet<string> usedKeys, HashSet<string> seenExcluded)
    {
        var all = Walk(root).ToList();
        var tips = all.OfType<HelpTip>().ToList();
        var tipKeys = tips.Select(t => t.Key).ToHashSet();

        foreach (var tip in tips)
        {
            // Inside a menu the menu item itself is the keyboard stop: it carries the same help text and tooltip.
            if (!InMenuItem(tip)) Assert.True(tip.Focusable, where + ": a \"?\" must be reachable with the keyboard");
            Assert.False(string.IsNullOrEmpty(tip.Key), where + ": a \"?\" without a key");
            Assert.True(HelpTexts.Exists(tip.Key!), where + ": unknown help key " + tip.Key);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(tip)), where + ": " + tip.Key + " has no automation help text");
            Assert.IsType<ToolTip>(tip.ToolTip);
            usedKeys.Add(tip.Key!);
        }

        foreach (var element in all.Where(e => e is not HelpTip && IsInteractive(e)))
        {
            if (element is FrameworkElement { Name: { Length: > 0 } name } && Excluded.Contains(name))
            {
                seenExcluded.Add(name);
                continue;
            }
            if (element is MenuItem item) Assert.IsType<ToolTip>(item.ToolTip);
            var key = HelpTip.GetKey(element);
            Assert.False(string.IsNullOrEmpty(key), where + ": no help tip for " + Describe(element));
            Assert.True(HelpTexts.Exists(key!), where + ": unknown help key " + key + " on " + Describe(element));
            Assert.True(tipKeys.Contains(key), where + ": " + Describe(element) + " uses help key " + key + " but no \"?\" with that key is shown next to it");
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(element)), where + ": " + Describe(element) + " has no automation help text");
            usedKeys.Add(key!);
        }
    }

    [Fact]
    public async Task EveryControlOnThePagesAndSubWindowsHasATipOrIsExcluded()
    {
        await ShellPageTests.RunSta(() =>
        {
            var used = new HashSet<string>();
            var excluded = new HashSet<string>();
            foreach (var page in new Page[] { new HomePage(), new ProfilesPage(), new SettingsPage(), new AboutPage() })
                Check(page.GetType().Name, page, used, excluded);

            var windows = new Window[]
            {
                new CalibrationWindow(),
                new ControllerTestWindow(),
                new ProcessPickerWindow([new ProcessPickerWindow.ProcessEntry("notepad", "Untitled")]),
            };
            foreach (var window in windows)
            {
                Check(window.GetType().Name, window, used, excluded);
                window.Close();
                ThemeEventCleanup.DropSubscribers();
            }

            // Stale entries in the exclusion list would hide a future gap.
            Assert.Equal(Excluded.OrderBy(x => x), excluded.OrderBy(x => x));
            // A tip nobody shows is dead text.
            Assert.Empty(HelpTexts.Keys.Except(used));
        });
    }

    [Fact]
    public void EveryTipHasTextInBothLanguagesOfAFewShortSentences()
    {
        foreach (var key in HelpTexts.Keys)
        {
            var (zh, en) = HelpTexts.Both(key);
            Assert.True(zh.Any(c => c > 0x2E80), key + ": the Chinese text is missing or not Chinese");
            Assert.True(en.Any(c => c is >= 'a' and <= 'z'), key + ": the English text is missing");
            Assert.True(en.Length <= 420, key + ": the English tip is too long to read in a tooltip (" + en.Length + ")");
            Assert.True(zh.Length <= 200, key + ": the Chinese tip is too long to read in a tooltip (" + zh.Length + ")");
        }
    }

    [Fact]
    public async Task ATipIsFocusableShowsOnKeyboardFocusAndGivesTheScreenReaderTheText()
    {
        await ShellPageTests.RunSta(() =>
        {
            var tip = new HelpTip { Key = "home.hide" };
            Assert.Equal("?", tip.Text);
            Assert.True(tip.Focusable);
            var expected = HelpTexts.Get("home.hide");
            Assert.NotEmpty(expected);
            Assert.Equal(expected, AutomationProperties.GetHelpText(tip));
            var tooltip = Assert.IsType<ToolTip>(tip.ToolTip);
            Assert.Equal(expected, Assert.IsType<TextBlock>(tooltip.Content).Text);

            var control = new Button();
            HelpTip.SetKey(control, "home.hide");
            Assert.Equal(expected, AutomationProperties.GetHelpText(control));
        });
    }
}
