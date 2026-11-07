using TSCC_WASD.Core.Models;

namespace TSCC_WASD.App.ViewModels;

public enum ProfileSaveState { Saved, Pending, Saving, Invalid, Failed }

/// <summary>
/// Debounced, serialized auto-save of the profile list. Edits call <see cref="Touch"/>; the write happens after a quiet
/// period (and never while a mouse button is held, so a slider drag writes once at its end). The snapshot is taken on the
/// owner's context (the UI thread); the file write runs on a worker thread. <see cref="Flush"/> writes pending edits
/// synchronously (app exit, hiding to the tray, reload).
/// </summary>
public sealed class ProfileAutoSaver : IDisposable
{
    private readonly Func<IReadOnlyList<MappingProfile>> _capture;
    private readonly Action<IReadOnlyList<MappingProfile>> _write;
    private readonly Func<bool> _canSave;
    private readonly Func<bool> _holdOff;
    private readonly Action<ProfileSaveState, string?> _report;
    private readonly SynchronizationContext? _context;
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private readonly object _writeLock = new();
    private volatile bool _dirty;
    private volatile bool _disposed;
    private long _sequence;
    private long _written;

    /// <param name="capture">Validated copy of every profile. Throws <see cref="ArgumentException"/> for invalid values.</param>
    /// <param name="write">Persists the copy (<c>ProfileStore.SaveProfiles</c>).</param>
    /// <param name="canSave">False while nothing may be written (profile file failed to load).</param>
    /// <param name="holdOff">True while saving should wait (a mouse button is down).</param>
    /// <param name="report">State changes, raised on <paramref name="context"/> (inline when it is null).</param>
    public ProfileAutoSaver(Func<IReadOnlyList<MappingProfile>> capture, Action<IReadOnlyList<MappingProfile>> write,
        Func<bool> canSave, Action<ProfileSaveState, string?> report, TimeSpan delay,
        SynchronizationContext? context = null, Func<bool>? holdOff = null)
    {
        _capture = capture;
        _write = write;
        _canSave = canSave;
        _report = report;
        _delay = delay;
        _context = context;
        _holdOff = holdOff ?? (() => false);
        _timer = new Timer(_ => OnTimer(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public ProfileSaveState State { get; private set; } = ProfileSaveState.Saved;

    /// <summary>An edit happened: (re)start the quiet period.</summary>
    public void Touch()
    {
        if (_disposed || !_canSave()) return;
        _dirty = true;
        Report(ProfileSaveState.Pending, null);
        Arm();
    }

    /// <summary>Forgets pending edits (the list was reloaded from disk).</summary>
    public void Reset()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _dirty = false;
        Report(ProfileSaveState.Saved, null);
    }

    /// <summary>Writes pending edits now. True when everything is on disk (or nothing may be written).</summary>
    public bool Flush()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        if (_disposed) return true;
        if (!_canSave()) { _dirty = false; return true; }
        lock (_writeLock) { } // let a write already in flight finish; a failed one leaves the edits dirty
        if (!_dirty) return State is not ProfileSaveState.Invalid;
        IReadOnlyList<MappingProfile> snapshot;
        try { snapshot = _capture(); }
        catch (ArgumentException ex) { _dirty = false; Report(ProfileSaveState.Invalid, ex.Message); return false; }
        _dirty = false;
        long sequence = Interlocked.Increment(ref _sequence);
        string? error = WriteSnapshot(snapshot, sequence);
        if (error is not null) { Report(ProfileSaveState.Failed, error); return false; }
        Report(ProfileSaveState.Saved, null);
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }

    private void Arm()
    {
        if (!_disposed) try { _timer.Change(_delay, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
    }

    private void OnTimer()
    {
        if (_disposed) return;
        if (_context is null) Run();
        else _context.Post(_ => Run(), null);
    }

    private void Run()
    {
        if (_disposed || !_dirty) return;
        if (!_canSave()) { _dirty = false; return; }
        bool held;
        try { held = _holdOff(); } catch { held = false; }
        if (held) { Arm(); return; }
        IReadOnlyList<MappingProfile> snapshot;
        try { snapshot = _capture(); }
        catch (ArgumentException ex) { _dirty = false; Report(ProfileSaveState.Invalid, ex.Message); return; }
        catch (InvalidOperationException) { Arm(); return; } // the list changed under the capture; try again shortly
        _dirty = false;
        long sequence = Interlocked.Increment(ref _sequence);
        Report(ProfileSaveState.Saving, null);
        if (_context is null) Finish(snapshot, sequence);
        else Task.Run(() => Finish(snapshot, sequence));
    }

    private void Finish(IReadOnlyList<MappingProfile> snapshot, long sequence)
    {
        string? error = WriteSnapshot(snapshot, sequence);
        void Done()
        {
            if (_disposed) return;
            if (error is not null) Report(ProfileSaveState.Failed, error);
            else if (!_dirty && sequence == Interlocked.Read(ref _sequence)) Report(ProfileSaveState.Saved, null);
        }
        if (_context is null) Done();
        else _context.Post(_ => Done(), null);
    }

    /// <summary>Serialized write; a snapshot older than one already written is dropped. Returns the error text, if any.</summary>
    private string? WriteSnapshot(IReadOnlyList<MappingProfile> snapshot, long sequence)
    {
        lock (_writeLock)
        {
            if (sequence < _written) return null;
            try { _write(snapshot); _written = sequence; return null; }
            catch (Exception ex) { _dirty = true; return ex.Message; }
        }
    }

    private void Report(ProfileSaveState state, string? message)
    {
        State = state;
        _report(state, message);
    }
}
