using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>One NS2 Pro USB device. Background I/O never blocks the mapping poll loop.</summary>
public sealed class Switch2InputReader : IInputReader, IMotionSource, IRumbleTarget
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private static readonly TimeSpan RumbleInterval = TimeSpan.FromMilliseconds(12);
    private State _latest;
    private MotionSample _motion;
    private RawSticks _raw;
    private bool _hasMotion;
    private volatile int _rumble; // large << 8 | small
    private volatile bool _calibrationChanged;
    private long _receivedAt;
    private bool _hasState;
    private readonly Switch2CalibrationCache _calibrationCache;
    private string _status = L.T("尋找 NS2 Pro（USB）...", "Looking for NS2 Pro (USB)...");
    private bool _disposed;

    public Switch2InputReader(Switch2CalibrationCache? calibrationCache = null)
    {
        _calibrationCache = calibrationCache ?? new Switch2CalibrationCache();
        Switch2CalibrationCache.UserCalibrationChanged += OnCalibrationChanged;
        _worker = Task.Run(() => RunAsync(_stop.Token));
    }
    public string Status { get { lock (_gate) return _status; } }

    public bool TryGetState(out State state)
    {
        lock (_gate)
        {
            bool fresh = _hasState && Stopwatch.GetElapsedTime(_receivedAt) < TimeSpan.FromMilliseconds(250);
            state = fresh ? _latest : default;
            return fresh;
        }
    }

    public bool TryGetMotion(out MotionSample motion)
    {
        lock (_gate)
        {
            bool fresh = _hasMotion && Stopwatch.GetElapsedTime(_receivedAt) < TimeSpan.FromMilliseconds(250);
            motion = fresh ? _motion : default;
            return fresh;
        }
    }

    private void OnCalibrationChanged() => _calibrationChanged = true;

    /// <summary>Latest uncalibrated stick positions, for the calibration wizard.</summary>
    public bool TryGetRawSticks(out RawSticks raw)
    {
        lock (_gate)
        {
            bool fresh = _hasState && Stopwatch.GetElapsedTime(_receivedAt) < TimeSpan.FromMilliseconds(250);
            raw = _raw;
            return fresh;
        }
    }

    public void SetRumble(byte large, byte small) => _rumble = (large << 8) | small;

    private void SetStatus(string status, bool clear = true)
    {
        lock (_gate)
        {
            _status = status;
            if (clear) { _hasState = false; _latest = default; _hasMotion = false; }
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var paths = WindowsDevicePaths.Find(WindowsDevicePaths.Switch2Usb);
                    if (paths.Length > 1) throw new IOException(L.T("目前一次支援一支 NS2 Pro，請拔除其他 NS2 Pro。", "Only one NS2 Pro is supported at a time; unplug the others."));
                    if (paths.Length == 0) throw new IOException(L.T("等待 NS2 Pro USB 連線（不支援藍牙）。", "Waiting for an NS2 Pro over USB (Bluetooth is not supported)."));
                    SetStatus(L.T("初始化 NS2 Pro USB 與搖桿校準...", "Initializing NS2 Pro USB and stick calibration..."));
                    (Switch2StickCalibration Left, Switch2StickCalibration Right) calibration;
                    bool sharedMode = false, cachedCalibration = false;
                    try
                    {
                        using var usb = new Switch2UsbControl(paths[0]);
                        calibration = Switch2Protocol.Initialize(usb, token);
                        _calibrationCache.SaveFactory(calibration.Left, calibration.Right);
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode is 5 or 32)
                    {
                        // Steam can own USB control while HID reports remain shareable.
                        // Do not fight ownership or install a different driver.
                        sharedMode = true;
                        cachedCalibration = _calibrationCache.TryLoadFactory(out var left, out var right);
                        calibration = cachedCalibration ? (left, right) : (Switch2StickCalibration.Nominal, Switch2StickCalibration.Nominal);
                        SetStatus(L.T("NS2 Pro HID 共用模式：等待 Steam 初始化。", "NS2 Pro shared HID mode: waiting for Steam to initialize it."));
                    }

                    var hidPaths = WindowsDevicePaths.Find(WindowsDevicePaths.Hid);
                    if (hidPaths.Length != 1) throw new IOException(L.T("找不到唯一的 NS2 Pro HID 介面；請檢查 HidHide 允許路徑。", "Could not find exactly one NS2 Pro HID interface; check the HidHide allowed path."));
                    var (handle, writeHandle) = OpenHidPair(hidPaths[0]);
                    using var handleOwner = handle;
                    using var writeOwner = writeHandle;
                    using var stream = new FileStream(handle, FileAccess.Read, 64, isAsync: true);
                    // The user's own calibration wins; without any real calibration, learn the travel.
                    bool userCalibration = _calibrationCache.TryLoadUser(out var userLeft, out var userRight);
                    if (userCalibration) calibration = (userLeft, userRight);
                    AdaptiveStickCalibration? adaptiveLeft = null, adaptiveRight = null;
                    if (sharedMode && !cachedCalibration && !userCalibration) { adaptiveLeft = new(); adaptiveRight = new(); }
                    using var rumbleCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var rumbleTask = RunRumbleAsync(writeHandle, rumbleCts.Token);
                    try
                    {
                        byte[] report = new byte[64];
                        while (!token.IsCancellationRequested)
                        {
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                            timeout.CancelAfter(TimeSpan.FromSeconds(2));
                            int length = await stream.ReadAsync(report, timeout.Token).ConfigureAwait(false);
                            if (length == 0) throw new IOException(L.T("NS2 Pro 已斷線。", "NS2 Pro disconnected."));
                            if (_calibrationChanged)
                            {
                                // The wizard saved or cleared a calibration while mapping: apply it now.
                                _calibrationChanged = false;
                                userCalibration = _calibrationCache.TryLoadUser(out userLeft, out userRight);
                                if (userCalibration)
                                {
                                    calibration = (userLeft, userRight);
                                    adaptiveLeft = adaptiveRight = null;
                                }
                                else if (sharedMode && !cachedCalibration) { adaptiveLeft = new(); adaptiveRight = new(); }
                                else if (cachedCalibration || !sharedMode)
                                    calibration = _calibrationCache.TryLoadFactory(out var fl, out var fr) ? (fl, fr) : calibration;
                            }
                            ReportCapture.Shared.Record("NS2ProUsb", report.AsSpan(0, length));
                            if (length == 64 && report[0] == 0x05)
                            {
                                var raw = new RawSticks(
                                    Switch2StickCalibration.UnpackX(report.AsSpan(11)), Switch2StickCalibration.UnpackY(report.AsSpan(11)),
                                    Switch2StickCalibration.UnpackX(report.AsSpan(14)), Switch2StickCalibration.UnpackY(report.AsSpan(14)));
                                lock (_gate) _raw = raw;
                                if (adaptiveLeft is not null && adaptiveRight is not null)
                                    calibration = (adaptiveLeft.Observe(raw.LeftX, raw.LeftY), adaptiveRight.Observe(raw.RightX, raw.RightY));
                            }
                            if (!Switch2ReportParser.TryParse(report.AsSpan(0, length), calibration.Left, calibration.Right, out var state))
                                continue;
                            bool hasMotion = Switch2ReportParser.TryParseMotion(report.AsSpan(0, length), out var motion);
                            lock (_gate)
                            {
                                _latest = state;
                                _motion = motion;
                                _hasMotion = hasMotion;
                                _receivedAt = Stopwatch.GetTimestamp();
                                _hasState = true;
                                _status = userCalibration ? L.T("NS2 Pro（使用者校準）", "NS2 Pro (your calibration)")
                                    : !sharedMode ? L.T("NS2 Pro（USB，原廠校準）", "NS2 Pro (USB, factory calibration)")
                                    : cachedCalibration ? L.T("NS2 Pro（HID 共用，已記住的原廠校準）", "NS2 Pro (shared HID, remembered factory calibration)")
                                    : L.T("NS2 Pro（HID 共用，自動學習搖桿行程；建議用「校準搖桿」校準一次）", "NS2 Pro (shared HID, learning stick travel; use Calibrate sticks once for best results)");
                            }
                        }
                    }
                    finally
                    {
                        rumbleCts.Cancel();
                        await rumbleTask.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                { SetStatus(L.T("NS2 Pro 無輸入回報，正在重新連線...", "NS2 Pro stopped reporting; reconnecting...")); }
                catch (HidHideDeniedException)
                { SetStatus(L.T("NS2 Pro：HidHide 拒絕存取手把（已嘗試重新註冊與暫時取消隱藏）", "NS2 Pro: HidHide denied access to the controller (re-register and temporary unhide were tried)")); }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                { SetStatus(HidHideRecovery is not null
                    ? L.T("NS2 Pro：HidHide 拒絕存取手把", "NS2 Pro: HidHide denied access to the controller")
                    : L.T("NS2 Pro：存取被拒", "NS2 Pro: access denied")); }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 32)
                { SetStatus(L.T("NS2 Pro USB 被占用：請先完整退出 Steam／其他手把工具，再重新啟動映射。", "NS2 Pro USB is in use: fully exit Steam or other controller tools, then restart mapping.")); }
                catch (Exception ex) when (ex is IOException or Win32Exception)
                { SetStatus($"NS2 Pro：{ex.Message}"); }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { SetStatus(L.T($"NS2 Pro 輸入已停止：{ex.Message}", $"NS2 Pro input stopped: {ex.Message}")); }
        finally { lock (_gate) { _hasState = false; _latest = default; } }
    }

    /// <summary>Set by the mapping coordinator while it hides controllers; null otherwise.</summary>
    public static volatile IHidHideRecovery? HidHideRecovery;

    private sealed class HidHideDeniedException() : Win32Exception(5);

    private string? _deniedPath; // HID path whose denial was logged / whose recovery already failed

    private static SafeFileHandle OpenHid(string path, bool write) => write
        ? Switch2UsbControl.CreateFileW(path, 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)
        : Switch2UsbControl.CreateFileW(path, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);

    /// <summary>Opens the read handle (required) and the rumble write handle (optional, null when unsupported).</summary>
    private static (SafeFileHandle Read, SafeFileHandle? Write) TryOpenPair(string path, out int error)
    {
        error = 0;
        var read = OpenHid(path, false);
        if (read.IsInvalid) { error = Marshal.GetLastWin32Error(); read.Dispose(); return (read, null); }
        var write = OpenHid(path, true);
        if (write.IsInvalid) { error = Marshal.GetLastWin32Error(); write.Dispose(); return (read, null); }
        return (read, write);
    }

    /// <summary>
    /// Opens the HID interface for reading and rumble. If HidHide denies a device we hid ourselves (error 5), register
    /// the app again, then unhide that one device just long enough to open both handles; open handles stay valid once it is hidden again.
    /// </summary>
    private (SafeFileHandle Read, SafeFileHandle? Write) OpenHidPair(string path)
    {
        var pair = TryOpenPair(path, out int error);
        if (error == 0) { _deniedPath = null; return pair; }
        var recovery = HidHideRecovery;
        if (error != 5 || recovery is null || !recovery.IsHiddenByUs(path))
        {
            // Rumble is best-effort: only a failed read handle is an error.
            if (!pair.Read.IsInvalid) { _deniedPath = null; return pair; }
            throw new Win32Exception(error);
        }

        pair.Read.Dispose();
        // Recovery already failed for this path: do not hammer the CLI every second.
        if (string.Equals(_deniedPath, path, StringComparison.OrdinalIgnoreCase)) throw new HidHideDeniedException();
        AppLog.Warn($"NS2 Pro HID open denied (error {error}) while hidden by HidHide: {path}");
        try
        {
            recovery.ReRegisterApp();
            pair = TryOpenPair(path, out error);
            if (error == 0) { AppLog.Info("NS2 Pro HID open succeeded after re-registering the app with HidHide"); _deniedPath = null; return pair; }
            pair.Read.Dispose();
            pair = recovery.OpenWhileUnhidden(path, () =>
            {
                var p = TryOpenPair(path, out int e);
                if (p.Read.IsInvalid) throw new Win32Exception(e);
                return p;
            });
            AppLog.Info("NS2 Pro HID open succeeded after unhiding the device for a moment");
            _deniedPath = null;
            return pair;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or TimeoutException or InvalidOperationException)
        {
            AppLog.Warn($"NS2 Pro HID open still denied after HidHide recovery: {ex.Message}");
            _deniedPath = path;
            throw new HidHideDeniedException();
        }
    }

    /// <summary>Streams rumble packets while a motor is on, then one stop packet. Best-effort.</summary>
    private async Task RunRumbleAsync(SafeFileHandle? handle, CancellationToken token)
    {
        if (handle is null || handle.IsInvalid) return;
        try
        {
            using var output = new FileStream(handle, FileAccess.Write, 0, isAsync: false);
            int sequence = 0, sent = 0;
            using var timer = new PeriodicTimer(RumbleInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    int value = _rumble;
                    if (value == 0 && sent == 0) continue;
                    output.Write(Switch2Rumble.BuildReport((byte)(value >> 8), (byte)value, sequence++));
                    sent = value;
                }
            }
            finally
            {
                // Never leave the motors running when mapping stops or the controller reconnects.
                if (sent != 0) output.Write(Switch2Rumble.BuildReport(0, 0, sequence));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Rumble unsupported here. */ }
        finally
        {
            _rumble = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Switch2CalibrationCache.UserCalibrationChanged -= OnCalibrationChanged;
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _stop.Dispose();
    }
}
