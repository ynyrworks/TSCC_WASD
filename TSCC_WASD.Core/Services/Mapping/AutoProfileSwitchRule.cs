using TSCC_WASD.Core.Models;

namespace TSCC_WASD.Core.Services.Mapping;

/// <summary>Foreground process as seen by the detector; Name may be a bare name, with or without ".exe", or a path.</summary>
public readonly record struct ForegroundProcess(string Name, int Pid);

/// <summary>
/// Pure rule for switching profiles by game while mapping runs. A matching game coming to the foreground
/// selects its profile; Alt-Tab away does nothing; the profile the user had comes back only when every
/// tracked game process has ended. A manual selection wins until the next game comes to the foreground.
/// No Windows API here: the caller supplies the foreground process and a liveness check.
/// </summary>
public sealed class AutoProfileSwitchRule
{
    public const string SelfProcessName = "TSCC_WASD";

    private readonly HashSet<int> _games = new();
    private string? _originalId;
    private bool _manualOverride;

    public bool IsTracking => _games.Count > 0;

    /// <summary>True when at least one profile has a usable match name; otherwise nothing needs to be polled.</summary>
    public static bool HasAnyMatch(IEnumerable<MappingProfile> profiles) => profiles.Any(p => Normalize(p.MatchProcessName) is not null);

    /// <summary>Lower-cased file name without ".exe"; null for blank values.</summary>
    public static string? Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        int slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0) name = name[(slash + 1)..];
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        name = name.Trim();
        return name.Length == 0 ? null : name.ToLowerInvariant();
    }

    /// <summary>The profile to select now, or null for no change. foreground is null when unknown or unreadable.</summary>
    public string? Evaluate(IReadOnlyList<MappingProfile> profiles, string? currentId, ForegroundProcess? foreground, Func<int, bool> isAlive)
    {
        _games.RemoveWhere(pid => !isAlive(pid));

        string? target = null;
        if (foreground is { } fg)
        {
            string? name = Normalize(fg.Name);
            if (name is not null && name != Normalize(SelfProcessName))
            {
                target = profiles.FirstOrDefault(p => Normalize(p.MatchProcessName) == name)?.Id;
                if (target is not null)
                {
                    // A game process not tracked yet coming to the foreground; Alt-Tab back to a tracked one is not.
                    bool entered = _games.Add(fg.Pid);
                    if (_games.Count == 1 && entered)
                    {
                        // First tracked game: a manual choice made after the last game ended is the new baseline;
                        // otherwise keep a pending original (game A ended while B is already in front).
                        if (_originalId is null || _manualOverride) _originalId = currentId;
                    }
                    if (entered) _manualOverride = false;
                    if (!_manualOverride && target != currentId) return target;
                    return null;
                }
            }
        }

        if (_games.Count == 0 && (_originalId is not null || _manualOverride)) return Finish(profiles, currentId);
        return null;
    }

    /// <summary>True when an automatic switch changes the virtual pad type, so a game that already scanned for pads may need a restart.</summary>
    public static bool ShouldNotifyOutputChange(OutputControllerType oldType, OutputControllerType newType, bool automatic)
        => automatic && oldType != newType;

    /// <summary>The user picked a profile; the rule stops fighting it until a game comes to the foreground again.</summary>
    public void NotifyManualSelection()
    {
        if (IsTracking) _manualOverride = true;
    }

    /// <summary>Mapping stopped: the profile to restore (or null) and all state cleared.</summary>
    public string? Stop(IReadOnlyList<MappingProfile> profiles, string? currentId)
    {
        string? restore = _manualOverride ? null : Finish(profiles, currentId);
        Reset();
        return restore;
    }

    public void Reset()
    {
        _games.Clear();
        _originalId = null;
        _manualOverride = false;
    }

    private string? Finish(IReadOnlyList<MappingProfile> profiles, string? currentId)
    {
        string? restore = _manualOverride ? null : _originalId;
        _originalId = null;
        _manualOverride = false;
        if (restore is null || restore == currentId || !profiles.Any(p => p.Id == restore)) return null;
        return restore;
    }
}
