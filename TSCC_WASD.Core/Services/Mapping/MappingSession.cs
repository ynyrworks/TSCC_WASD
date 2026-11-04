using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.VirtualControllers;

namespace TSCC_WASD.Core.Services.Mapping;

/// <summary>Owns one input/output pair. Lifecycle methods are called serially by the coordinator.</summary>
public sealed class MappingSession : IAsyncDisposable
{
    private volatile MappingProfile _profile;
    private volatile bool _paused;
    private readonly IInputReader _input;
    private readonly IVirtualController _output;
    private readonly Action<string>? _statusCallback;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;
    private int _inputReleased;
    private volatile InputSnapshot? _lastInput;

    // The loop only stores the latest reading; the text is built when someone reads InputSummary.
    public sealed record InputSnapshot(bool Paused, bool Available, State State);

    public MappingSession(MappingProfile profile, IInputReader input, IVirtualController output,
        Action<string>? statusCallback = null)
    {
        _profile = profile.Snapshot();
        _input = input;
        _output = output;
        _statusCallback = statusCallback;
        _output.RumbleRequested += OnRumble;
        ApplyInputOptions(_profile);
    }

    public static string OutputName(OutputControllerType type) =>
        type == OutputControllerType.Xbox360 ? "Xbox 360" : "DualShock 4";

    /// <summary>A↔B and X↔Y: the right-hand button becomes confirm (Cross / Xbox A).</summary>
    public static GamepadButtonFlags SwapFaceButtons(GamepadButtonFlags buttons)
    {
        const GamepadButtonFlags face = GamepadButtonFlags.A | GamepadButtonFlags.B | GamepadButtonFlags.X | GamepadButtonFlags.Y;
        var result = buttons & ~face;
        if (buttons.HasFlag(GamepadButtonFlags.A)) result |= GamepadButtonFlags.B;
        if (buttons.HasFlag(GamepadButtonFlags.B)) result |= GamepadButtonFlags.A;
        if (buttons.HasFlag(GamepadButtonFlags.X)) result |= GamepadButtonFlags.Y;
        if (buttons.HasFlag(GamepadButtonFlags.Y)) result |= GamepadButtonFlags.X;
        return result;
    }

    private void ApplyInputOptions(MappingProfile profile)
    {
        if (_input is IXboxBackButtonOption option) option.BackAsTouchpad = profile.XboxBackAsTouchpad;
    }

    private void OnRumble(byte large, byte small)
    {
        if (!_paused && _profile.ForwardRumble && _input is IRumbleTarget target) target.SetRumble(large, small);
    }

    /// <summary>
    /// Applies dead zone, polling rate and rumble changes on the next loop pass. The input and output
    /// types are fixed for a session; the coordinator rebuilds the session when they change.
    /// </summary>
    public void UpdateProfile(MappingProfile profile)
    {
        var snapshot = profile.Snapshot();
        bool rumbleTurnedOff = _profile.ForwardRumble && !snapshot.ForwardRumble;
        _profile = snapshot;
        ApplyInputOptions(snapshot);
        if (rumbleTurnedOff) StopRumble();
    }

    /// <summary>
    /// Paused: the virtual DS4 stays connected but receives neutral input, and rumble stops.
    /// The physical controller is still read so resuming is instant.
    /// </summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value) StopRumble();
        }
    }

    private void StopRumble()
    {
        try { (_input as IRumbleTarget)?.SetRumble(0, 0); }
        catch { /* The next game rumble report retries. */ }
    }

    private static readonly TimeSpan OutputRetryDelay = TimeSpan.FromSeconds(1);
    public bool IsRunning => _loopTask is { IsCompleted: false };
    public string? LastError { get; private set; }
    public string InputSummary => Describe(_lastInput);
    /// <summary>Latest reading from the loop (read-only), or null before the first one.</summary>
    public InputSnapshot? LatestInput => _lastInput;
    /// <summary>The input reader's own status text, e.g. which controller is being read.</summary>
    public string InputStatus => _input.Status;

    private bool IsXInputSource() =>
        _input is XInputReader || (_input is AutoInputReader auto && auto.ActiveSource?.Contains("XInput") == true);

    private static string Describe(InputSnapshot? snapshot)
    {
        if (snapshot is null) return L.T("尚未收到輸入", "No input yet");
        var state = snapshot.State;
        return snapshot.Paused
            ? L.T("已暫停：遊戲收到的是放開所有按鍵的虛擬手把", "Paused: games see a virtual controller with nothing pressed")
            : snapshot.Available
            ? L.T($"按鍵：{state.Gamepad.Buttons} · 左搖桿 ({state.Gamepad.LeftThumbX}, {state.Gamepad.LeftThumbY}) · 右搖桿 ({state.Gamepad.RightThumbX}, {state.Gamepad.RightThumbY}) · L2/R2 {state.Gamepad.LeftTrigger}/{state.Gamepad.RightTrigger}",
                $"Buttons: {state.Gamepad.Buttons} · Left stick ({state.Gamepad.LeftThumbX}, {state.Gamepad.LeftThumbY}) · Right stick ({state.Gamepad.RightThumbX}, {state.Gamepad.RightThumbY}) · L2/R2 {state.Gamepad.LeftTrigger}/{state.Gamepad.RightTrigger}")
            : L.T("尚未收到有效輸入（輸出歸零）", "No valid input (output neutral)");
    }

    public bool TryStart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cts is not null) throw new InvalidOperationException("A session can only be started once.");
        if (!_output.TryConnect(out var error))
        {
            LastError = error ?? L.T("無法建立虛擬手把。", "Could not create the virtual controller.");
            return false;
        }
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
        return true;
    }

    private async Task RunAsync(CancellationToken token)
    {
        string? previousStatus = null;
        try
        {
            // Runs until stopped: input loss sends neutral, driver errors reconnect the virtual pad.
            while (!token.IsCancellationRequested)
            {
                bool available;
                State state;
                if (_output is IXInputSlotOwner owner && _input is IXInputSlotFilter filter)
                    filter.ExcludedSlot = owner.XInputSlot;
                try { available = _input.TryGetState(out state); }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    available = false;
                    state = default;
                }
                bool paused = _paused;
                _lastInput = new InputSnapshot(paused, available, state);
                if (available && ReportCapture.Shared.IsRecording && IsXInputSource())
                    ReportCapture.Shared.RecordText("XInput", $"parsed buttons={state.Gamepad.Buttons} LT={state.Gamepad.LeftTrigger} RT={state.Gamepad.RightTrigger} L=({state.Gamepad.LeftThumbX},{state.Gamepad.LeftThumbY}) R=({state.Gamepad.RightThumbX},{state.Gamepad.RightThumbY})");
                string status;
                try
                {
                    if (!_output.IsConnected && !_output.TryConnect(out var connectError))
                        throw new InvalidOperationException(connectError ?? L.T("無法重新建立虛擬手把。", "Could not recreate the virtual controller."));
                    // Always send neutral input on disconnect; never leave buttons held.
                    if (paused) available = false;
                    if (available && _profile.SwapFaceButtons) state.Gamepad.Buttons = SwapFaceButtons(state.Gamepad.Buttons);
                    MotionSample? motion = available && _input is IMotionSource source && source.TryGetMotion(out var sample)
                        ? sample : null;
                    _output.PushState(available ? state : default, _profile.DeadZone, motion);
                    string pad = OutputName(_profile.OutputType);
                    status = paused
                        ? L.T($"映射已暫停（虛擬 {pad} 保持連線，實體手把仍隱藏）。", $"Mapping paused (virtual {pad} stays connected, physical controllers stay hidden).")
                        : available ? L.T($"映射運作中：{_input.Status} → {pad}", $"Mapping: {_input.Status} → {pad}")
                        : L.T($"等待手把（虛擬 {pad} 已連線）：{_input.Status}", $"Waiting for a controller (virtual {pad} connected): {_input.Status}");
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    try { _output.Disconnect(); }
                    catch { /* Reconnect will be attempted on the next pass. */ }
                    status = L.T($"虛擬手把發生錯誤，稍後自動重新連線：{ex.Message}", $"Virtual controller error, reconnecting shortly: {ex.Message}");
                    if (previousStatus != status) { previousStatus = status; Report(status); }
                    await Task.Delay(OutputRetryDelay, token).ConfigureAwait(false);
                    continue;
                }
                if (previousStatus != status)
                {
                    previousStatus = status;
                    Report(status);
                }
                await Task.Delay(_profile.PollingIntervalMs, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            try
            {
                if (_output.IsConnected) _output.PushState(default, _profile.DeadZone);
            }
            catch (Exception ex) { LastError ??= ex.Message; }
            try { _output.Disconnect(); }
            catch (Exception ex) { LastError ??= ex.Message; }
            try { ReleaseInput(); }
            catch (Exception ex) { LastError ??= ex.Message; }
        }
    }

    private void Report(string message)
    {
        // A presentation callback must not prevent driver cleanup.
        try { _statusCallback?.Invoke(message); }
        catch { /* Presentation owner may already be shutting down. */ }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loopTask is not null) await _loopTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            _cts?.Dispose();
            try { ReleaseInput(); }
            finally { _output.Dispose(); }
        }
    }

    private void ReleaseInput()
    {
        if (Interlocked.Exchange(ref _inputReleased, 1) != 0) return;
        _output.RumbleRequested -= OnRumble;
        try { (_input as IRumbleTarget)?.SetRumble(0, 0); }
        finally { _input.Dispose(); }
    }
}
