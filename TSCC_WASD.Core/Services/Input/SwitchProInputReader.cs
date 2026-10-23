using System.ComponentModel;
using System.Diagnostics;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// The original Nintendo Switch Pro Controller over USB or Bluetooth. If Steam already set it up,
/// its full reports are simply read; otherwise TSCC_WASD sends the documented USB handshake and the
/// "full report" subcommand, and falls back to the simple Bluetooth report. Stick calibration is read
/// from SPI flash (the user's own, else the factory one); until it arrives, or on controllers without
/// it, stick travel is learned while playing. No rumble. Experimental.
/// </summary>
public sealed class SwitchProInputReader : IInputReader
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMilliseconds(250);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private State _latest;
    private bool _hasState;
    private long _receivedAt;
    private string _status = L.T("尋找 Switch Pro 手把...", "Looking for a Switch Pro Controller...");
    private bool _disposed;

    public SwitchProInputReader() => _worker = Task.Run(() => RunAsync(_stop.Token));

    public string Status { get { lock (_gate) return _status; } }

    public bool TryGetState(out State state)
    {
        lock (_gate)
        {
            bool fresh = _hasState && Stopwatch.GetElapsedTime(_receivedAt) < Fresh;
            state = fresh ? _latest : default;
            return fresh;
        }
    }

    private void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
            _hasState = false;
            _latest = default;
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
                    var found = HidDevice.Find(WindowsDevicePaths.NintendoVendor, [WindowsDevicePaths.SwitchProProduct]);
                    if (found.Count == 0)
                        throw new IOException(L.T("等待 Switch Pro 手把連線（USB 或藍牙）。", "Waiting for a Switch Pro Controller (USB or Bluetooth)."));
                    var device = found[0];
                    string name = L.T("Switch Pro 手把", "Switch Pro Controller") + (device.Bluetooth ? L.T("（藍牙）", " (Bluetooth)") : " (USB)");
                    using var stream = HidDevice.OpenRead(device.Path, out int inputLength);
                    using var output = HidDevice.TryOpenWrite(device.Path, out int outputLength);
                    var adaptiveLeft = new AdaptiveStickCalibration();
                    var adaptiveRight = new AdaptiveStickCalibration();
                    var report = new byte[Math.Max(64, inputLength)];
                    byte counter = 0;
                    bool full = false, setupSent = false;
                    var started = Stopwatch.GetTimestamp();
                    // Stick calibration read from SPI flash; until it arrives (or if it never does) the travel is learned.
                    byte[]? factory = null, user = null;
                    Switch2StickCalibration? calibratedLeft = null, calibratedRight = null;
                    int calibrationRequests = 0;
                    long lastRequest = 0;
                    while (!token.IsCancellationRequested)
                    {
                        // Not in full-report mode a moment after opening: ask for it (USB also needs the handshake).
                        if (!full && !setupSent && output is not null && Stopwatch.GetElapsedTime(started) > TimeSpan.FromMilliseconds(500))
                        {
                            setupSent = true;
                            TrySetUp(output, outputLength, device.Bluetooth, ref counter);
                        }
                        if (full && calibratedLeft is null && output is not null && calibrationRequests < 3
                            && (calibrationRequests == 0 || Stopwatch.GetElapsedTime(lastRequest) > TimeSpan.FromSeconds(1)))
                        {
                            calibrationRequests++;
                            lastRequest = Stopwatch.GetTimestamp();
                            TryWrite(output, outputLength, SwitchProReport.SpiRead(counter++, SwitchProReport.FactoryStickCalibration, 18));
                            TryWrite(output, outputLength, SwitchProReport.SpiRead(counter++, SwitchProReport.UserStickCalibration, 22));
                        }
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(device.Bluetooth ? 2 : 1));
                        int length;
                        try { length = await stream.ReadAsync(report, timeout.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested && !setupSent && output is not null)
                        {
                            continue; // An un-initialised USB controller is silent until the handshake.
                        }
                        if (length == 0) throw new IOException(L.T($"{name} 已斷線。", $"{name} disconnected."));
                        var data = report.AsSpan(0, length);
                        if (SwitchProReport.TryParseSpiReply(data, out uint address, out var flash))
                        {
                            if (address == SwitchProReport.FactoryStickCalibration) factory = flash.ToArray();
                            else if (address == SwitchProReport.UserStickCalibration) user = flash.ToArray();
                            if (factory is not null && user is not null
                                && SwitchProReport.TryDecodeCalibration(factory, user, out var calLeft, out var calRight))
                            {
                                (calibratedLeft, calibratedRight) = (calLeft, calRight);
                                AppLog.Info($"Switch Pro stick calibration: left {calLeft}, right {calRight}");
                            }
                            continue;
                        }
                        if (data[0] == 0x30 && length >= 12)
                        {
                            full = true;
                            var left = adaptiveLeft.Observe(Switch2StickCalibration.UnpackX(data[6..]), Switch2StickCalibration.UnpackY(data[6..]));
                            var right = adaptiveRight.Observe(Switch2StickCalibration.UnpackX(data[9..]), Switch2StickCalibration.UnpackY(data[9..]));
                            if (calibratedLeft is { } l && calibratedRight is { } r) (left, right) = (l, r);
                            if (!SwitchProReport.TryParse(data, left, right, out var state, out _)) continue;
                            Publish(state, calibratedLeft is null ? name : name + L.T("，原廠校準", ", factory calibration"));
                        }
                        else if (SwitchProReport.TryParse(data, Switch2StickCalibration.Nominal, Switch2StickCalibration.Nominal, out var simple, out _))
                            Publish(simple, name + L.T("（簡易模式）", " (simple mode)"));
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                { SetStatus(L.T("Switch Pro 手把沒有回報輸入，正在重新連線...", "The Switch Pro Controller stopped reporting; reconnecting...")); }
                catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
                { SetStatus(ex.Message); }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { SetStatus(L.T($"Switch Pro 手把輸入已停止：{ex.Message}", $"Switch Pro Controller input stopped: {ex.Message}")); }
        finally { lock (_gate) { _hasState = false; _latest = default; } }
    }

    private void Publish(State state, string status)
    {
        lock (_gate)
        {
            _latest = state;
            _receivedAt = Stopwatch.GetTimestamp();
            _hasState = true;
            _status = status;
        }
    }

    /// <summary>Best-effort: a controller another program already configured simply keeps its mode.</summary>
    private static void TrySetUp(FileStream output, int outputLength, bool bluetooth, ref byte counter)
    {
        try
        {
            if (!bluetooth)
                foreach (var command in SwitchProReport.UsbHandshake) Write(output, command, outputLength);
            Write(output, SwitchProReport.SetFullReportMode(counter++), outputLength);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void TryWrite(FileStream output, int outputLength, byte[] report)
    {
        try { Write(output, report, outputLength); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void Write(FileStream output, byte[] report, int outputLength)
    {
        // HID writes must be exactly the interface's output report size.
        var buffer = new byte[Math.Max(report.Length, outputLength)];
        report.CopyTo(buffer, 0);
        output.Write(buffer);
        Thread.Sleep(30);
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
