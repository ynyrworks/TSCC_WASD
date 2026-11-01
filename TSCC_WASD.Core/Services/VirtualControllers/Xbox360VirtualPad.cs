using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.Core.Services.VirtualControllers;

/// <summary>Virtual pads that occupy an XInput slot, which XInput readers must then skip.</summary>
public interface IXInputSlotOwner
{
    /// <summary>The XInput slot (0–3) Windows gave the virtual pad, or null before it is known.</summary>
    int? XInputSlot { get; }
}

/// <summary>Converts the shared XInput-shaped state into a virtual Xbox 360 report.</summary>
public static class Xbox360Report
{
    /// <summary>
    /// XInput and Xbox 360 use the same button bits. The touchpad bit (NS2 Capture, or Xbox View when
    /// it is set to act as the touchpad) has no Xbox equivalent and becomes Back.
    /// </summary>
    public static ushort Buttons(GamepadButtonFlags buttons)
    {
        var result = buttons & ~GamepadButtonFlags.Touchpad;
        if (buttons.HasFlag(GamepadButtonFlags.Touchpad)) result |= GamepadButtonFlags.Back;
        return (ushort)result;
    }

    /// <summary>Applies the radial dead zone to one stick, keeping XInput's signed 16-bit precision.</summary>
    public static (short X, short Y) Stick(short x, short y, double deadZone)
        => AxisConverter.ApplyRadial(x, y, deadZone);
}

/// <summary>
/// Virtual Xbox 360 controller through ViGEmBus, for players who prefer Xbox prompts (for example
/// with an NS2 Pro). It has no PS button, touchpad or motion; Guide stays Guide.
/// </summary>
public sealed class Xbox360VirtualPad : IVirtualController, IXInputSlotOwner
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
    private volatile bool _connected;

    public Xbox360VirtualPad()
    {
        _client = new ViGEmClient();
        try
        {
            _controller = _client.CreateXbox360Controller();
            _controller.AutoSubmitReport = false;
            _controller.FeedbackReceived += (_, e) => RumbleRequested?.Invoke(e.LargeMotor, e.SmallMotor);
        }
        catch
        {
            _client.Dispose();
            throw;
        }
    }

    public bool IsConnected => _connected;
    public string? LastError { get; private set; }
    public event Action<byte, byte>? RumbleRequested;

    public int? XInputSlot
    {
        get
        {
            if (!_connected) return null;
            try { return _controller.UserIndex; }
            catch { return null; } // Windows assigns the slot shortly after connecting.
        }
    }

    public bool TryConnect(out string? error)
    {
        try
        {
            _controller.Connect();
            _connected = true;
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
        if (!_connected) return;
        _connected = false;
        _controller.Disconnect();
    }

    public void PushState(State state, double deadZone, MotionSample? motion = null)
    {
        if (!_connected) return;
        var gp = state.Gamepad;
        _controller.SetButtonsFull(Xbox360Report.Buttons(gp.Buttons));
        var left = Xbox360Report.Stick(gp.LeftThumbX, gp.LeftThumbY, deadZone);
        var right = Xbox360Report.Stick(gp.RightThumbX, gp.RightThumbY, deadZone);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, left.X);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, left.Y);
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, right.X);
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, right.Y);
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, gp.LeftTrigger);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, gp.RightTrigger);
        _controller.SubmitReport();
    }

    public void Dispose()
    {
        try { Disconnect(); }
        finally
        {
            try { (_controller as IDisposable)?.Dispose(); }
            finally { _client.Dispose(); }
        }
    }
}
