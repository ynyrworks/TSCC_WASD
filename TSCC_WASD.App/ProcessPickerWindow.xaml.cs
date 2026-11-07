using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace TSCC_WASD.App;

public partial class ProcessPickerWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly List<ProcessEntry> _all;

    public sealed record ProcessEntry(string Name, string Title)
    {
        public string Display => string.IsNullOrWhiteSpace(Title) ? Name : $"{Name} - {Title}";
    }

    /// <summary>Process name chosen by the user; null when cancelled.</summary>
    public string? SelectedProcessName { get; private set; }

    public ProcessPickerWindow() : this(EnumerateWindowedProcesses()) { }

    public ProcessPickerWindow(IEnumerable<ProcessEntry> entries)
    {
        InitializeComponent();
        _all = entries.ToList();
        Refresh();
    }

    /// <summary>Lists processes with a main window once, skipping this app and any process that cannot be read.</summary>
    public static List<ProcessEntry> EnumerateWindowedProcesses()
    {
        var result = new List<ProcessEntry>();
        int self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == self || process.MainWindowHandle == IntPtr.Zero) continue;
                string name = process.ProcessName;
                if (string.Equals(name, "TSCC_WASD", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new ProcessEntry(name, process.MainWindowTitle));
            }
            catch { /* exited or access denied: skip */ }
            finally { process.Dispose(); }
        }
        return result;
    }

    private void Refresh()
    {
        string filter = FilterBox.Text.Trim();
        ProcessList.ItemsSource = _all
            .Where(e => filter.Length == 0 || e.Display.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ProcessList.DisplayMemberPath = nameof(ProcessEntry.Display);
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) { Refresh(); }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => OkButton.IsEnabled = ProcessList.SelectedItem is ProcessEntry;
    private void OnDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (ProcessList.SelectedItem is ProcessEntry) OnOk(sender, e); }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (ProcessList.SelectedItem is not ProcessEntry entry) return;
        SelectedProcessName = entry.Name;
        DialogResult = true;
    }
}
