using System.Reflection;
using Wpf.Ui.Appearance;

namespace TSCC_WASD.Tests;

/// <summary>
/// WPF-UI's title bar subscribes to the static <see cref="ApplicationThemeManager.Changed"/> event and only unsubscribes when it
/// unloads, which a window that was never shown does not do. A later theme change on another test thread would then call into a
/// control owned by a finished thread, so tests that build windows drop the subscribers when they are done.
/// </summary>
internal static class ThemeEventCleanup
{
    private static readonly FieldInfo? ChangedField =
        typeof(ApplicationThemeManager).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

    internal static void DropSubscribers() => ChangedField?.SetValue(null, null);
}
