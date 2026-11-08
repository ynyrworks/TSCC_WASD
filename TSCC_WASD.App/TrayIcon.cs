using System.Runtime.InteropServices;
using System.Windows.Input;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;
using Forms = System.Windows.Forms;

namespace TSCC_WASD.App;

/// <summary>
/// Notification-area icon: double-click restores the window; the menu starts, stops or exits.
/// A green dot on the icon means mapping is running, an amber one that it is paused.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _idleIcon, _mappingIcon, _pausedIcon;
    private readonly MainViewModel _viewModel;
    private readonly Forms.ToolStripMenuItem _start, _pause, _stop;

    public TrayIcon(MainViewModel viewModel, Action show, Action exit)
    {
        _viewModel = viewModel;
        _start = new Forms.ToolStripMenuItem(L.T("開始映射", "Start mapping"), null, (_, _) => Run(viewModel.StartCommand));
        _pause = new Forms.ToolStripMenuItem(L.T("暫停", "Pause"), null, (_, _) => Run(viewModel.PauseCommand));
        _stop = new Forms.ToolStripMenuItem(L.T("停止映射", "Stop mapping"), null, (_, _) => Run(viewModel.StopCommand));
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.T("顯示視窗", "Show window"), null, (_, _) => show());
        menu.Items.Add(_start);
        menu.Items.Add(_pause);
        menu.Items.Add(_stop);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.T("結束", "Exit"), null, (_, _) => exit());
        menu.Opening += (_, _) =>
        {
            _start.Enabled = viewModel.StartCommand.CanExecute(null);
            _pause.Enabled = viewModel.PauseCommand.CanExecute(null);
            _pause.Text = viewModel.PauseButtonText;
            _stop.Enabled = viewModel.StopCommand.CanExecute(null);
        };
        _idleIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        _mappingIcon = WithStatusDot(_idleIcon, System.Drawing.Color.FromArgb(52, 199, 89));
        _pausedIcon = WithStatusDot(_idleIcon, System.Drawing.Color.FromArgb(255, 179, 64));
        _icon = new Forms.NotifyIcon
        {
            Icon = _idleIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => show();
        viewModel.PropertyChanged += OnViewModelChanged;
        UpdateText();
    }

    private static void Run(ICommand command)
    {
        if (command.CanExecute(null)) command.Execute(null);
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.IsPaused)) UpdateText();
    }

    private void UpdateText()
    {
        string state = !_viewModel.IsRunning ? L.T("已停止", "stopped") : _viewModel.IsPaused ? L.T("已暫停", "paused") : L.T("映射中", "mapping");
        _icon.Text = $"TSCC_WASD · {state}"; // NotifyIcon text is limited to 63 characters.
        _icon.Icon = !_viewModel.IsRunning ? _idleIcon : _viewModel.IsPaused ? _pausedIcon : _mappingIcon;
    }

    private static System.Drawing.Icon WithStatusDot(System.Drawing.Icon source, System.Drawing.Color color)
    {
        using var bitmap = source.ToBitmap();
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int size = bitmap.Width / 2, x = bitmap.Width - size, y = bitmap.Height - size;
            using var outline = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(32, 32, 32));
            using var fill = new System.Drawing.SolidBrush(color);
            g.FillEllipse(outline, x, y, size, size);
            g.FillEllipse(fill, x + 2, y + 2, size - 4, size - 4);
        }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)borrowed.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public void ShowBalloon(string text, bool warning = false) =>
        _icon.ShowBalloonTip(warning ? 10000 : 3000, "TSCC_WASD", text, warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _idleIcon.Dispose();
        _mappingIcon.Dispose();
        _pausedIcon.Dispose();
    }
}
