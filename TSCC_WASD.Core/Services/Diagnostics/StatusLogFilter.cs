namespace TSCC_WASD.Core.Services.Diagnostics;

/// <summary>
/// Decides whether a status text is written to the log. Only the repeating "waiting for a controller" status
/// is limited to once per window, so a reader that alternates between messages cannot flood the file;
/// every other text (start, stop, errors, reconnects, updates) is always logged.
/// </summary>
public sealed class StatusLogFilter(TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(60);
    private readonly Dictionary<string, DateTime> _lastLogged = [];

    /// <summary>True for the waiting text MappingSession shows while no controller is available (either language).</summary>
    public static bool IsWaitingStatus(string text) =>
        text.StartsWith("等待手把", StringComparison.Ordinal) || text.StartsWith("Waiting for a controller", StringComparison.Ordinal);

    public bool ShouldLog(string text, DateTime now)
    {
        if (!IsWaitingStatus(text)) return true;
        if (_lastLogged.TryGetValue(text, out var last) && now - last < _window) return false;
        // Forget texts whose window has passed so the table stays small.
        foreach (var stale in _lastLogged.Where(p => now - p.Value >= _window).Select(p => p.Key).ToList())
            _lastLogged.Remove(stale);
        _lastLogged[text] = now;
        return true;
    }
}
