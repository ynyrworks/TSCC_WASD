using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Short, opt-in recording of raw controller reports for bug reports. Off by default: while off,
/// <see cref="Record"/> costs one volatile read. It stops by itself after a time limit or an entry
/// limit (checked on each write, so no timer or thread is needed). Nothing is sent anywhere.
/// </summary>
public sealed class ReportCapture
{
    public const int DefaultMaxEntries = 2000;
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    /// <summary>Capture shared by the input readers and the controller test window.</summary>
    public static ReportCapture Shared { get; } = new();

    public readonly record struct Entry(long Milliseconds, string Source, byte[] Data, string? Text);

    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private volatile bool _active;
    private long _startedAt;
    private long _durationTicks;
    private int _maxEntries;

    /// <summary>True while recording. Cheap enough for the read loops.</summary>
    public bool IsRecording
    {
        get
        {
            if (!_active) return false;
            lock (_gate) { ExpireIfDue(); return _active; }
        }
    }

    public void Start(TimeSpan? duration = null, int maxEntries = DefaultMaxEntries)
    {
        lock (_gate)
        {
            _entries.Clear();
            _startedAt = Stopwatch.GetTimestamp();
            _durationTicks = (long)((duration ?? DefaultDuration).TotalSeconds * Stopwatch.Frequency);
            _maxEntries = Math.Max(1, maxEntries);
            _active = true;
        }
    }

    public void Stop()
    {
        lock (_gate) _active = false;
    }

    /// <summary>Records a copy of a raw report. Never throws; recording problems must not affect mapping.</summary>
    public void Record(string source, ReadOnlySpan<byte> report)
    {
        if (!_active) return;
        try { Add(source, report.ToArray(), null); }
        catch { /* Recording is best effort. */ }
    }

    /// <summary>Records a text line (used for XInput, which has no raw report).</summary>
    public void RecordText(string source, string text)
    {
        if (!_active) return;
        try { Add(source, [], text); }
        catch { /* Recording is best effort. */ }
    }

    private void Add(string source, byte[] data, string? text)
    {
        lock (_gate)
        {
            if (!_active) return;
            ExpireIfDue();
            if (!_active) return;
            long ms = (Stopwatch.GetTimestamp() - _startedAt) * 1000 / Stopwatch.Frequency;
            _entries.Add(new Entry(ms, source, data, text));
            if (_entries.Count >= _maxEntries) _active = false;
        }
    }

    private void ExpireIfDue()
    {
        if (_active && Stopwatch.GetTimestamp() - _startedAt >= _durationTicks) _active = false;
    }

    public IReadOnlyList<Entry> Snapshot()
    {
        lock (_gate) return _entries.ToArray();
    }

    /// <summary>Plain text: the diagnostic report first, then one line per entry: "ms source hex".</summary>
    public string Export(string diagnostics)
    {
        var entries = Snapshot();
        var text = new StringBuilder(diagnostics);
        if (text.Length > 0 && text[^1] != '\n') text.AppendLine();
        text.AppendLine();
        text.AppendLine("Raw report capture (milliseconds, source, hex bytes)");
        text.AppendLine("XInput has no raw report: its lines hold the parsed state instead.");
        text.AppendLine($"Entries: {entries.Count}");
        foreach (var e in entries)
            text.Append(e.Milliseconds.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(e.Source).Append(' ')
                .AppendLine(e.Text ?? Convert.ToHexString(e.Data));
        return text.ToString();
    }
}
