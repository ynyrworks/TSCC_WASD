using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.App;

/// <summary>Live view of the latest mapped input, plus a short raw-report recording saved to a file the user picks.</summary>
public partial class ControllerTestWindow : Window
{
    private readonly Func<MappingSession.InputSnapshot?> _latest;
    private readonly Func<bool> _isRunning;
    private readonly Func<string?> _status;
    private readonly Func<string> _diagnostics;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Dictionary<GamepadButtonFlags, Border> _chips = new();
    private DateTime? _recordingUntil;

    public ControllerTestWindow() : this(() => null, () => false, () => null, () => "") { }

    public ControllerTestWindow(Func<MappingSession.InputSnapshot?> latest, Func<bool> isRunning, Func<string?> status, Func<string> diagnostics)
    {
        _latest = latest;
        _isRunning = isRunning;
        _status = status;
        _diagnostics = diagnostics;
        InitializeComponent();
        foreach (var flag in Enum.GetValues<GamepadButtonFlags>().Where(f => f != GamepadButtonFlags.None))
        {
            var chip = new Border
            {
                Margin = new Thickness(2), Padding = new Thickness(2, 4, 2, 4), CornerRadius = new CornerRadius(4),
                Background = (Brush)FindResource("RaisedBrush"),
                Child = new TextBlock { Text = flag.ToString(), FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            };
            _chips[flag] = chip;
            ButtonGrid.Children.Add(chip);
        }
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); ReportCapture.Shared.Stop(); };
        Refresh();
    }

    private void Refresh()
    {
        bool running = _isRunning();
        var snapshot = running ? _latest() : null;
        SourceText.Text = running
            ? L.T($"目前來源：{_status()}", $"Current source: {_status()}")
            : L.T("請先開始映射", "Start mapping first");
        ReasonText.Text = !running
            ? L.T("測試頁顯示映射中讀到的輸入；映射沒有執行時沒有資料可看。請回主視窗按「啟動映射」。", "This page shows the input read while mapping. With mapping stopped there is nothing to show; click Start in the main window.")
            : snapshot is null ? L.T("尚未收到輸入", "No input yet")
            : snapshot.Paused ? L.T("映射已暫停，因此顯示為放開所有按鍵。", "Mapping is paused, so everything shows as released.")
            : !snapshot.Available ? L.T("尚未收到有效輸入。", "No valid input yet.") : "";
        var gamepad = snapshot is { Paused: false, Available: true } ? snapshot.State.Gamepad : default;
        foreach (var (flag, chip) in _chips)
            chip.Background = (Brush)FindResource(gamepad.Buttons.HasFlag(flag) ? "AccentBrush" : "RaisedBrush");
        Place(LeftDot, gamepad.LeftThumbX, gamepad.LeftThumbY);
        Place(RightDot, gamepad.RightThumbX, gamepad.RightThumbY);
        LeftValue.Text = $"({gamepad.LeftThumbX}, {gamepad.LeftThumbY})";
        RightValue.Text = $"({gamepad.RightThumbX}, {gamepad.RightThumbY})";
        LeftTrigger.Value = gamepad.LeftTrigger;
        RightTrigger.Value = gamepad.RightTrigger;
        if (_recordingUntil is { } until) UpdateRecording(until);
    }

    private static void Place(FrameworkElement dot, short x, short y)
    {
        Canvas.SetLeft(dot, 50 + x / 32768.0 * 44 - 6);
        Canvas.SetTop(dot, 50 - y / 32768.0 * 44 - 6);
    }

    private void OnRecord(object sender, RoutedEventArgs e)
    {
        if (!_isRunning())
        {
            RecordStatus.Text = L.T("請先開始映射再錄製。", "Start mapping before recording.");
            return;
        }
        RecordButton.IsEnabled = false;
        _recordingUntil = DateTime.UtcNow + ReportCapture.DefaultDuration;
        ReportCapture.Shared.Start();
        UpdateRecording(_recordingUntil.Value);
    }

    private void UpdateRecording(DateTime until)
    {
        var left = until - DateTime.UtcNow;
        if (left > TimeSpan.Zero && ReportCapture.Shared.IsRecording)
        {
            RecordStatus.Text = L.T($"錄製中… {Math.Ceiling(left.TotalSeconds):0} 秒（請操作手把）", $"Recording… {Math.Ceiling(left.TotalSeconds):0} s (use the controller)");
            return;
        }
        _recordingUntil = null;
        ReportCapture.Shared.Stop();
        RecordButton.IsEnabled = true;
        RecordStatus.Text = "";
        Save();
    }

    private void Save()
    {
        var dialog = new SaveFileDialog { Filter = L.T("文字檔|*.txt", "Text file|*.txt"), FileName = "TSCC_WASD-controller-report.txt" };
        if (dialog.ShowDialog(this) != true) return; // Cancelled: nothing is written.
        try
        {
            File.WriteAllText(dialog.FileName, ReportCapture.Shared.Export(_diagnostics()));
            RecordStatus.Text = L.T("已儲存。上傳前請先看過內容。", "Saved. Please read it before uploading.");
        }
        catch (Exception ex) { RecordStatus.Text = L.T($"無法儲存：{ex.Message}", $"Could not save: {ex.Message}"); }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
