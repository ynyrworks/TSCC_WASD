using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TSCC_WASD.Core;
using TSCC_WASD.Core.Services.Calibration;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.App;

/// <summary>Stick calibration wizard for the controller in use: release the sticks, then circle them at the edge.</summary>
public partial class CalibrationWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly CalibrationWizardModel _model;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(10) };
    private readonly ProgressBar[] _bars;
    private CalibrationWizardModel.Step _shown = (CalibrationWizardModel.Step)(-1);

    public CalibrationWindow(CalibrationWizardModel model)
    {
        _model = model;
        InitializeComponent();
        string title = L.T($"校準 {model.ControllerName} 搖桿", $"Calibrate {model.ControllerName} sticks");
        Title = title;
        WindowTitleBar.Title = title;
        _bars = [L0, L1, L2, L3, R0, R1, R2, R3];
        ResetButton.IsEnabled = model.HasStoredCalibration;
        _timer.Tick += OnTick;
        _timer.Start();
        // Closing without saving (Cancel, the X button, Alt+F4) puts the previous calibration back.
        Closed += (_, _) => { _timer.Stop(); _model.Dispose(); };
        ShowStep();
    }

    /// <summary>Display name of a controller model when the live reader did not give one.</summary>
    public static string ModelName(ControllerId id) => id.Model switch
    {
        ControllerId.Ns2Pro => "NS2 Pro",
        ControllerId.DualShock4 => "DualShock 4",
        ControllerId.DualSense => "DualSense",
        ControllerId.SwitchPro => "Switch Pro",
        ControllerId.XInput => L.T("Xbox 手把", "Xbox controller"),
        _ => id.Model
    };

    private void ShowStep()
    {
        var step = _model.State;
        _shown = step;
        (StepTitle.Text, StepText.Text, NextButton.Content) = step switch
        {
            CalibrationWizardModel.Step.Center or CalibrationWizardModel.Step.MeasuringCenter =>
                (L.T("步驟 1／2：放開搖桿", "Step 1 of 2: release the sticks"),
                 L.T($"把 {_model.ControllerName} 放在桌上、不要碰兩支搖桿，然後按「下一步」。", $"Put the {_model.ControllerName} down without touching either stick, then click Next."),
                 L.T("下一步", "Next")),
            _ => (L.T("步驟 2／2：轉圈推到底", "Step 2 of 2: circle at the edge"),
                L.T("把兩支搖桿沿著邊緣各慢慢轉 3 圈，確實推到底。每個方向的進度條滿了就可以儲存。",
                    "Slowly circle each stick around its edge three times, pushed all the way. Save once every bar is full."),
                L.T("儲存", "Save"))
        };
        RangePanel.Visibility = step == CalibrationWizardModel.Step.Range ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _model.Tick();
        var pad = _model.Sticks;
        RawText.Text = _model.InputAvailable
            ? L.T($"目前輸入：左 ({pad.LeftThumbX}, {pad.LeftThumbY})　右 ({pad.RightThumbX}, {pad.RightThumbY})",
                  $"Input: left ({pad.LeftThumbX}, {pad.LeftThumbY})   right ({pad.RightThumbX}, {pad.RightThumbY})")
            : L.T("讀不到手把輸入。請確認映射正在執行、手把已連接。", "No controller input. Make sure mapping is running and the controller is connected.");
        CenterWarn.Visibility = _model.State == CalibrationWizardModel.Step.MeasuringCenter && _model.CenterProblem != CenterCheck.Ok
            ? Visibility.Visible : Visibility.Collapsed;
        NoReportWarn.Visibility = _model.NoReports ? Visibility.Visible : Visibility.Collapsed;
        if (_model.State != _shown) ShowStep();
        var progress = _model.Progress;
        for (int i = 0; i < _bars.Length; i++) _bars[i].Value = progress[i];
        NextButton.IsEnabled = _model.CanAdvance;
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        bool saved = _model.Advance();
        if (saved) DialogResult = true;
        else if (_model.SaveFailed)
            AppDialog.Show(this, L.T("無法儲存校準，請看記錄檔。", "The calibration could not be saved; see the log."), "TSCC_WASD");
        else ShowStep();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        if (AppDialog.Show(this, L.T($"要清除 {_model.ControllerName} 的校準嗎？之後改用原廠或自動學習的結果。", $"Clear the calibration of the {_model.ControllerName}? Its factory values or automatic learning are used instead."),
                "TSCC_WASD", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _model.Clear();
        ResetButton.IsEnabled = false;
        AppDialog.Show(this, L.T("已清除校準，映射中立即生效。", "The calibration was cleared; mapping picks that up at once."), "TSCC_WASD");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
