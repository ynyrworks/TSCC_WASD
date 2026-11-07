using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.App;

/// <summary>Two-step NS2 Pro stick calibration: release the sticks, then circle them at the edge.</summary>
public partial class CalibrationWindow : Wpf.Ui.Controls.FluentWindow
{
    private enum Step { Connecting, Center, MeasuringCenter, Range }

    private readonly Switch2InputReader _reader = new();
    private readonly Switch2CalibrationCache _cache = new();
    private readonly StickCalibrator _calibrator = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(10) };
    private readonly ProgressBar[] _bars;
    private Step _step = Step.Connecting;

    public CalibrationWindow()
    {
        InitializeComponent();
        _bars = [L0, L1, L2, L3, R0, R1, R2, R3];
        ResetButton.IsEnabled = _cache.TryLoadUser(out _, out _);
        _timer.Tick += OnTick;
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); _reader.Dispose(); };
        Show(Step.Connecting);
    }

    private void Show(Step step)
    {
        _step = step;
        (StepTitle.Text, StepText.Text, NextButton.Content) = step switch
        {
            Step.Connecting => (L.T("連接 NS2 Pro…", "Connecting to the NS2 Pro…"),
                L.T("請用 USB 接上 NS2 Pro。", "Connect the NS2 Pro over USB."), L.T("下一步", "Next")),
            Step.Center or Step.MeasuringCenter => (L.T("步驟 1／2：放開搖桿", "Step 1 of 2: release the sticks"),
                L.T("把手把放在桌上、不要碰兩支搖桿，然後按「下一步」。", "Put the controller down without touching either stick, then click Next."),
                L.T("下一步", "Next")),
            _ => (L.T("步驟 2／2：轉圈推到底", "Step 2 of 2: circle at the edge"),
                L.T("把兩支搖桿沿著邊緣各慢慢轉 3 圈，確實推到底。每個方向的進度條滿了就可以儲存。",
                    "Slowly circle each stick around its edge three times, pushed all the way. Save once every bar is full."),
                L.T("儲存", "Save"))
        };
        RangePanel.Visibility = step == Step.Range ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        bool available = _reader.TryGetRawSticks(out var raw);
        RawText.Text = available
            ? L.T($"原始值：左 ({raw.LeftX}, {raw.LeftY})　右 ({raw.RightX}, {raw.RightY})",
                  $"Raw: left ({raw.LeftX}, {raw.LeftY})   right ({raw.RightX}, {raw.RightY})")
            : _reader.Status;
        switch (_step)
        {
            case Step.Connecting:
                if (available) Show(Step.Center);
                NextButton.IsEnabled = false;
                break;
            case Step.Center:
                NextButton.IsEnabled = available;
                break;
            case Step.MeasuringCenter:
                NextButton.IsEnabled = false;
                if (available) _calibrator.AddCenterSample(raw);
                if (_calibrator.CenterReady) Show(Step.Range);
                break;
            case Step.Range:
                if (available) _calibrator.AddRangeSample(raw);
                var progress = _calibrator.Progress;
                for (int i = 0; i < _bars.Length; i++) _bars[i].Value = progress[i];
                NextButton.IsEnabled = _calibrator.RangeReady;
                break;
        }
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step == Step.Center)
        {
            Show(Step.MeasuringCenter);
            return;
        }
        if (_step != Step.Range || !_calibrator.RangeReady) return;
        var (left, right) = _calibrator.Result();
        _cache.SaveUser(left, right);
        DialogResult = true;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _cache.ClearUser();
        ResetButton.IsEnabled = false;
        AppDialog.Show(this, L.T("已清除你的校準，之後會改用原廠校準或自動學習。", "Your calibration was cleared; the factory calibration or automatic learning is used instead."),
            "TSCC_WASD");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
