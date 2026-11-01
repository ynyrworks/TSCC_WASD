namespace TSCC_WASD.Core.Services.Setup;

/// <summary>
/// Keeps newly connected controllers hidden without launching HidHideCLI on a fast loop. When the device
/// watcher registers, <c>hide</c> runs once (debounced) after an arrival, plus a slow fallback timer. When
/// registration fails it falls back to a fast periodic scan. After <see cref="Dispose"/> returns, no
/// further <c>hide</c> call is running or will start.
/// </summary>
internal sealed class HidHideRescanner : IDisposable
{
    internal static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(700);
    internal static readonly TimeSpan DefaultFallback = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultPolled = TimeSpan.FromSeconds(5);

    private readonly Action _hide;
    private readonly Timer _debounceTimer;
    private readonly Timer _periodicTimer;
    private readonly TimeSpan _debounce;
    private readonly object _runLock = new();
    private readonly object _stateLock = new();
    private IDisposable? _watcher;
    private bool _pending, _stopped;

    /// <param name="startWatcher">Registers device notifications and returns the registration; throws on failure.</param>
    public HidHideRescanner(Action hide, Func<Action, IDisposable> startWatcher, TimeSpan? debounce = null,
        TimeSpan? fallbackInterval = null, TimeSpan? polledInterval = null)
    {
        _hide = hide;
        _debounce = debounce ?? DefaultDebounce;
        _debounceTimer = new Timer(_ => RunHide(), null, Timeout.Infinite, Timeout.Infinite);
        _periodicTimer = new Timer(_ => RunHide(), null, Timeout.Infinite, Timeout.Infinite);
        TimeSpan period;
        try
        {
            _watcher = startWatcher(OnArrival);
            period = fallbackInterval ?? DefaultFallback;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Device notifications unavailable ({ex.Message}); scanning for controllers every few seconds instead.");
            period = polledInterval ?? DefaultPolled;
        }
        lock (_stateLock)
        {
            if (!_stopped) _periodicTimer.Change(period, period);
        }
    }

    /// <summary>True when device notifications are registered (otherwise the fast scan is in use).</summary>
    public bool UsesNotifications => _watcher is not null;

    private void OnArrival()
    {
        lock (_stateLock)
        {
            if (_stopped || _pending) return; // Events during the debounce window collapse into one run.
            _pending = true;
            _debounceTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void RunHide()
    {
        lock (_stateLock) _pending = false;
        lock (_runLock)
        {
            if (_stopped) return;
            try { _hide(); }
            catch { /* Keep mapping; the next tick retries. */ }
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_stopped) return;
            _stopped = true;
        }
        try { _watcher?.Dispose(); } catch { /* Best effort. */ }
        _watcher = null;
        _debounceTimer.Dispose();
        _periodicTimer.Dispose();
        lock (_runLock) { } // Wait for a hide call already in flight.
    }
}
