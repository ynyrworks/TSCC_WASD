using System.Runtime.Versioning;
using TSCC_WASD.Core.Models;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Setup;
using TSCC_WASD.Core.Services.VirtualControllers;

namespace TSCC_WASD.Core.Services.Mapping;

[SupportedOSPlatform("windows")]
public sealed class MappingCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan HideRescanInterval = TimeSpan.FromSeconds(5);
    private readonly ProfileStore _profileStore;
    private readonly Func<HidHideGuard?> _hidHideFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MappingSession? _session;
    private MappingProfile? _sessionProfile;
    private Action<string>? _statusCallback;
    private HidHideGuard? _hider;
    private volatile InputDeviceType _hideInputType;
    private CancellationTokenSource? _rescanCts;
    private Task? _rescanTask;
    private bool _disposed;

    public MappingCoordinator(ProfileStore? profileStore = null, Func<HidHideGuard?>? hidHideFactory = null)
    {
        _profileStore = profileStore ?? new ProfileStore();
        _hidHideFactory = hidHideFactory ?? HidHideGuard.TryCreate;
    }

    /// <summary>Undo HidHide changes left behind by a crash or forced exit.</summary>
    public string? RecoverHiddenControllers()
    {
        try
        {
            var guard = _hidHideFactory();
            if (guard is null || !guard.HasPendingRestore) return null;
            guard.Restore();
            return L.T("已還原上次未正常結束時隱藏的實體手把。", "Restored controllers left hidden by an unexpected exit.");
        }
        catch (Exception ex) { return L.T($"無法還原 HidHide 設定：{ex.Message}", $"Could not restore HidHide settings: {ex.Message}"); }
    }
    public IReadOnlyList<MappingProfile> LoadProfiles() => _profileStore.LoadProfiles();
    public void SaveProfiles(IEnumerable<MappingProfile> profiles) => _profileStore.SaveProfiles(profiles);
    public bool IsRunning => _session?.IsRunning == true;
    public string? LastError => _session?.LastError;
    public string InputSummary => _session?.InputSummary ?? L.T("尚未啟動映射", "Mapping not started");

    public async Task<(bool started, string message)> StartAsync(MappingProfile profile, Action<string>? statusCallback = null)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning) return (false, L.T("已有映射在運作，請先停止。", "Mapping is already running; stop it first."));
            await ClearSessionAsync().ConfigureAwait(false);
            var snapshot = profile.Snapshot();
            _statusCallback = statusCallback;
            if (await StartSessionAsync(snapshot).ConfigureAwait(false) is { } error) return (false, error);
            // Hide only after the virtual DS4 exists, so a failed start never leaves controllers hidden.
            string hideMessage = snapshot.HidePhysicalControllers ? StartHiding(snapshot.InputType) : "";
            return (true, L.T($"映射已啟動。{hideMessage}", $"Mapping started. {hideMessage}"));
        }
        catch (Exception ex)
        {
            if (_session is not null)
            {
                try { await _session.DisposeAsync().ConfigureAwait(false); }
                catch { /* Preserve the original startup error. */ }
                _session = null;
            }
            await StopHidingAsync().ConfigureAwait(false);
            return (false, L.T($"無法啟動：{ex.Message}", $"Could not start: {ex.Message}"));
        }
        finally { _gate.Release(); }
    }

    /// <summary>Creates and starts a session for <paramref name="snapshot"/>; returns an error message on failure.</summary>
    private async Task<string?> StartSessionAsync(MappingProfile snapshot)
    {
        var output = VirtualControllerFactory.Create(snapshot.OutputType);
        IInputReader input;
        try
        {
            input = snapshot.InputType switch
            {
                InputDeviceType.Switch2ProUsb => new Switch2InputReader(),
                InputDeviceType.Switch2ProBluetooth => new Switch2BleInputReader(),
                InputDeviceType.XInput => new XInputReader(),
                InputDeviceType.PlayStation => new PlayStationInputReader(),
                InputDeviceType.SwitchPro => new SwitchProInputReader(),
                _ => new AutoInputReader()
            };
        }
        catch { output.Dispose(); throw; }
        var session = new MappingSession(snapshot, input, output, _statusCallback);
        if (!session.TryStart())
        {
            string message = session.LastError ?? L.T("無法啟動映射。", "Could not start mapping.");
            await session.DisposeAsync().ConfigureAwait(false);
            return message;
        }
        _session = session;
        _sessionProfile = snapshot;
        return null;
    }

    /// <summary>
    /// Applies profile edits or a different profile to the running mapping without stopping it, so the
    /// controllers stay hidden and Steam never gets them back. Dead zone, polling rate and rumble change in
    /// place; a different input or output type rebuilds the session but keeps hiding. Returns a message
    /// worth showing, or null. Invalid half-typed values are ignored until they become valid.
    /// </summary>
    public async Task<string?> ApplyProfileAsync(MappingProfile profile)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || _session is null || _sessionProfile is null || !_session.IsRunning) return null;
            MappingProfile snapshot;
            try { snapshot = profile.Snapshot(); }
            catch (ArgumentException) { return null; }

            string? message = null;
            var previous = _sessionProfile;
            if (snapshot.InputType != previous.InputType || snapshot.OutputType != previous.OutputType)
            {
                bool paused = _session.Paused;
                var old = _session;
                _session = null;
                await old.DisposeAsync().ConfigureAwait(false);
                if (await StartSessionAsync(snapshot).ConfigureAwait(false) is { } error)
                {
                    await StopHidingAsync().ConfigureAwait(false);
                    return L.T($"無法切換手把，映射已停止：{error}", $"Could not switch controllers; mapping stopped: {error}");
                }
                _session!.Paused = paused;
                message = L.T("已套用新的輸入手把設定。", "The new input controller setting is in use.");
            }
            else
            {
                _session.UpdateProfile(snapshot);
                _sessionProfile = snapshot;
            }

            if (snapshot.HidePhysicalControllers && _hider is null)
                message = StartHiding(snapshot.InputType);
            else if (!snapshot.HidePhysicalControllers && _hider is not null)
            {
                await StopHidingAsync().ConfigureAwait(false);
                message = L.T("已停止隱藏實體手把。", "Physical controllers are no longer hidden.");
            }
            else if (_hider is not null && snapshot.InputType != previous.InputType)
            {
                // Widen hiding to the new controller type; never unhide mid-session.
                _hideInputType = InputDeviceType.Auto;
                try { _hider.Hide(Environment.ProcessPath!, InputDeviceType.Auto); }
                catch { /* The rescan timer retries. */ }
            }
            return message;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Pausing keeps the virtual DS4 and the hiding; games just see nothing pressed.</summary>
    public bool Paused
    {
        get => _session?.Paused == true;
        set { if (_session is { } session) session.Paused = value; }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await ClearSessionAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    /// <summary>When physical controllers were hidden for the current session, or null.</summary>
    public DateTime? HidingSince { get; private set; }

    /// <summary>
    /// Steam opened the controllers before they were hidden, so Steam Input still forwards them
    /// to games and prompts flip between Xbox and PS until Steam restarts.
    /// </summary>
    public bool SteamSeesPhysicalControllers => HidingSince is { } since && SteamHelper.StartedBefore(since);

    private string StartHiding(InputDeviceType inputType)
    {
        try
        {
            _hider = _hidHideFactory();
            if (_hider is null) return L.T("未安裝 HidHide：遊戲可能同時看到實體手把，按鍵圖示可能在 Xbox／PS 間跳動。", "HidHide is not installed: games may also see the physical controller and prompts may flip between Xbox and PS.");
            string appPath = Environment.ProcessPath ?? throw new InvalidOperationException(L.T("無法取得程式路徑。", "Could not determine the program path."));
            _hideInputType = inputType;
            string message = _hider.Hide(appPath, inputType);
            HidingSince = DateTime.Now;
            var hider = _hider;
            _rescanCts = new CancellationTokenSource();
            var token = _rescanCts.Token;
            // Controllers turned on or plugged into a new port after start are hidden too.
            _rescanTask = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(HideRescanInterval);
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    try { hider.Hide(appPath, _hideInputType); }
                    catch { /* Keep mapping; the next tick retries. */ }
                }
            }, token);
            return message;
        }
        catch (Exception ex) { return L.T($"無法自動隱藏實體手把：{ex.Message}", $"Could not hide physical controllers: {ex.Message}"); }
    }

    private async Task StopHidingAsync()
    {
        _rescanCts?.Cancel();
        if (_rescanTask is not null)
        {
            try { await _rescanTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _rescanCts?.Dispose();
        _rescanCts = null;
        _rescanTask = null;
        var hider = _hider;
        _hider = null;
        HidingSince = null;
        hider?.Restore();
    }

    private async Task ClearSessionAsync()
    {
        var session = _session;
        _session = null;
        _sessionProfile = null;
        try
        {
            if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
        }
        finally { await StopHidingAsync().ConfigureAwait(false); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await ClearSessionAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
