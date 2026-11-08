using System.Diagnostics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.VirtualControllers;

public sealed class DualShock4VirtualPad : IVirtualController, IBatterySink
{
    private readonly ViGEmClient _client;
    private readonly IDualShock4Controller _controller;
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private volatile bool _connected;
    private byte _counter;
    private BatteryStatus _battery;
    private Thread? _feedbackThread;

    public DualShock4VirtualPad()
    {
        _client = new ViGEmClient();
        try
        {
            _controller = _client.CreateDualShock4Controller();
            _controller.AutoSubmitReport = false;
        }
        catch
        {
            _client.Dispose();
            throw;
        }
    }

    /// <summary>Physical battery shown in the next reports; written by the mapping loop.</summary>
    public BatteryStatus Battery { set => _battery = value; }

    public bool IsConnected => _connected;
    public string? LastError { get; private set; }
    public event Action<byte, byte>? RumbleRequested;

    public bool TryConnect(out string? error)
    {
        try
        {
            _controller.Connect();
            _connected = true;
            _feedbackThread = new Thread(ReadFeedback) { IsBackground = true, Name = "DS4 feedback" };
            _feedbackThread.Start();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            LastError = ex.ToString();
            _connected = false;
            return false;
        }
    }

    public void Disconnect()
    {
        if (_connected)
        {
            _connected = false;
            _controller.Disconnect();
            _feedbackThread?.Join(TimeSpan.FromSeconds(1));
            _feedbackThread = null;
        }
    }

    public void PushState(State state, double deadZone, MotionSample? motion = null)
    {
        if (!_connected) return;
        // DS4 sensor clock ticks every 16/3 µs (5.33 µs).
        double micros = Stopwatch.GetElapsedTime(_startTicks).TotalMicroseconds;
        ushort timestamp = (ushort)((long)(micros * 3 / 16) & 0xFFFF);
        _counter = (byte)((_counter + 1) & 0x3F);
        _controller.SubmitRawReport(DualShock4Report.Build(state, deadZone, motion, _counter, timestamp, _battery));
    }

    /// <summary>Waits for output reports the game writes to the virtual DS4 and forwards rumble.</summary>
    private void ReadFeedback()
    {
        while (_connected)
        {
            try
            {
                var report = _controller.AwaitRawOutputReport(250, out bool timedOut);
                if (timedOut || !_connected) continue;
                if (DualShock4Report.TryParseRumble(report.ToArray(), out byte large, out byte small))
                    RumbleRequested?.Invoke(large, small);
            }
            catch
            {
                // Rumble is best-effort; back off instead of spinning on a failing driver call.
                Thread.Sleep(250);
            }
        }
    }

    public void Dispose()
    {
        try { Disconnect(); }
        finally
        {
            try { _controller.Dispose(); }
            finally { _client.Dispose(); }
        }
    }
}
