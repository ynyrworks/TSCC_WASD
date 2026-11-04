namespace TSCC_WASD.Core.Services.Diagnostics;

/// <summary>
/// Decides whether a status text is written to the log: each distinct text at most once per window,
/// so a reader that alternates between two messages cannot flood the file.
/// </summary>
public sealed class StatusLogFilter(TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(60);
    private readonly Dictionary<string, DateTime> _lastLogged = [];

    public bool ShouldLog(string text, DateTime now)
    {
        if (_lastLogged.TryGetValue(text, out var last) && now - last < _window) return false;
        // Forget texts whose window has passed so the table stays small.
        foreach (var stale in _lastLogged.Where(p => now - p.Value >= _window).Select(p => p.Key).ToList())
            _lastLogged.Remove(stale);
        _lastLogged[text] = now;
        return true;
    }
}
