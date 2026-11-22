using System.ComponentModel;
using System.Diagnostics;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// One DualShock 4 or DualSense over USB or Bluetooth, read through its HID interface (shared, so
/// Steam can keep it too). Experimental: written from the published report layouts and unit-tested
/// with sample reports, not yet confirmed on hardware.
/// </summary>
public sealed class PlayStationInputReader : IInputReader, IMotionSource, IRumbleTarget, IBatterySource, IControllerIdentity
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMilliseconds(250);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private State _latest;
    private MotionSample _motion;
    private bool _hasState, _hasMotion;
    private BatteryStatus _battery;
    private volatile ControllerId? _identity;
    private long _receivedAt;
    private volatile int _rumble; // large << 8 | small
    private string _status = L.T("尋找 DualShock 4／DualSense...", "Looking for a DualShock 4 / DualSense...");
    private bool _disposed;
    private readonly HidHideOpener _opener = new("PlayStation");

    public PlayStationInputReader() => _worker = Task.Run(() => RunAsync(_stop.Token));

    public string Status { get { lock (_gate) return _status; } }

    public BatteryStatus Battery { get { lock (_gate) return _battery; } }

    public ControllerId? Identity => _identity;

    public bool TryGetState(out State state)
    {
        lock (_gate)
        {
            bool fresh = _hasState && Stopwatch.GetElapsedTime(_receivedAt) < Fresh;
            state = fresh ? _latest : default;
            return fresh;
        }
    }

    public bool TryGetMotion(out MotionSample motion)
    {
        lock (_gate)
        {
            bool fresh = _hasMotion && Stopwatch.GetElapsedTime(_receivedAt) < Fresh;
            motion = fresh ? _motion : default;
            return fresh;
        }
    }

    public void SetRumble(byte large, byte small) => _rumble = (large << 8) | small;

    private void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
            _hasState = _hasMotion = false;
            _latest = default;
            _battery = default; _identity = null;
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
                    var found = HidDevice.Find(WindowsDevicePaths.SonyVendor, WindowsDevicePaths.PlayStationProducts);
                    if (found.Count == 0)
                        throw new IOException(L.T("等待 DualShock 4／DualSense 連線（USB 或藍牙）。", "Waiting for a DualShock 4 / DualSense (USB or Bluetooth)."));
                    var device = found[0];
                    bool dualSense = PlayStationReport.IsDualSense(device.ProductId);
                    _identity = ControllerId.ForHid(dualSense ? ControllerId.DualSense : ControllerId.DualShock4, device.Path, device.Bluetooth);
                    string name = (dualSense ? "DualSense" : "DualShock 4") + (device.Bluetooth ? L.T("（藍牙）", " (Bluetooth)") : " (USB)");

                    // Read and rumble handles are opened together so one HidHide unhide covers both.
                    var (stream, inputLength, rumbleOutput, outputLength) = _opener.Open(device.Path, () =>
                    {
                        var s = HidDevice.OpenRead(device.Path, out int inLen);
                        return (s, inLen, HidDevice.TryOpenWrite(device.Path, out int outLen), outLen);
                    });
                    using var streamLease = stream;
                    using var rumbleCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var rumbleTask = RunRumbleAsync(device, dualSense, rumbleOutput, outputLength, rumbleCts.Token);
                    try
                    {
                        var report = new byte[Math.Max(64, inputLength)];
                        while (!token.IsCancellationRequested)
                        {
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                            timeout.CancelAfter(TimeSpan.FromSeconds(2));
                            int length = await stream.ReadAsync(report, timeout.Token).ConfigureAwait(false);
                            if (length == 0) throw new IOException(L.T($"{name} 已斷線。", $"{name} disconnected."));
                            ReportCapture.Shared.Record("PlayStation", report.AsSpan(0, length));
                            if (!PlayStationReport.TryParse(report.AsSpan(0, length), dualSense, out var state, out var motion)) continue;
                            lock (_gate)
                            {
                                _latest = state;
                                _hasMotion = motion is not null;
                                _motion = motion ?? default;
                                _receivedAt = Stopwatch.GetTimestamp();
                                _hasState = true;
                                _status = name;
                                if (PlayStationReport.TryParseBattery(report.AsSpan(0, length), dualSense, out var battery)) _battery = battery;
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
                { SetStatus(L.T("PlayStation 手把沒有回報輸入，正在重新連線...", "The PlayStation controller stopped reporting; reconnecting...")); }
                catch (HidHideDeniedException)
                { SetStatus(L.T("PlayStation：HidHide 拒絕存取手把（已嘗試重新註冊與暫時取消隱藏）", "PlayStation: HidHide denied access to the controller (re-register and temporary unhide were tried)")); }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                { SetStatus(_opener.HasRecovery
                    ? L.T("PlayStation：HidHide 拒絕存取手把", "PlayStation: HidHide denied access to the controller")
                    : L.T("PlayStation：存取被拒", "PlayStation: access denied")); }
                catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
                { SetStatus(ex.Message); }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { SetStatus(L.T($"PlayStation 手把輸入已停止：{ex.Message}", $"PlayStation controller input stopped: {ex.Message}")); }
        finally { lock (_gate) { _hasState = _hasMotion = false; _latest = default; _battery = default; _identity = null; } }
    }

    /// <summary>Sends a rumble report whenever the requested strength changes; stops the motors at the end.</summary>
    private async Task RunRumbleAsync(HidControllerPath device, bool dualSense, FileStream? output, int outputLength, CancellationToken token)
    {
        int sent = 0;
        try
        {
            if (output is null) return;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                int value = _rumble;
                if (value == sent) continue;
                output.Write(PlayStationReport.BuildRumble(dualSense, device.Bluetooth, (byte)(value >> 8), (byte)value, outputLength));
                sent = value;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Rumble is best-effort. */ }
        finally
        {
            try
            {
                // Never leave the motors running when mapping stops or the controller reconnects.
                if (sent != 0 && output is not null)
                    output.Write(PlayStationReport.BuildRumble(dualSense, device.Bluetooth, 0, 0, outputLength));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            output?.Dispose();
            _rumble = 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _stop.Dispose();
    }
}
