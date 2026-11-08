namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// Reads every supported controller (NS2 Pro over USB or Bluetooth, DualShock 4 / DualSense, Switch Pro, Xbox / XInput)
/// together and follows whichever one the player is using.
/// The active source only changes when it disconnects or the other one receives deliberate input,
/// so an idle controller that still streams neutral reports never steals control.
/// </summary>
public sealed class AutoInputReader : IInputReader, IMotionSource, IBatterySource, IControllerIdentity, IRumbleTarget, IXboxBackButtonOption, IXInputSlotFilter
{
    public int? ExcludedSlot
    {
        get => _sources.OfType<IXInputSlotFilter>().FirstOrDefault()?.ExcludedSlot;
        set { foreach (var source in _sources.OfType<IXInputSlotFilter>()) source.ExcludedSlot = value; }
    }

    /// <summary>Forwarded to the XInput source; the NS2 Pro has its own touchpad (Capture) button.</summary>
    public bool BackAsTouchpad
    {
        get => _sources.OfType<IXboxBackButtonOption>().FirstOrDefault()?.BackAsTouchpad ?? false;
        set { foreach (var source in _sources.OfType<IXboxBackButtonOption>()) source.BackAsTouchpad = value; }
    }

    private const int TriggerThreshold = 30;
    private const int StickThreshold = 12000;
    private readonly IInputReader[] _sources;
    private readonly string[] _names;
    private volatile int _active = -1;
    private volatile string _lastError = "";
    private volatile int _rumble; // large << 8 | small, replayed onto whichever source becomes active
    private bool _disposed;

    public AutoInputReader() : this(
        [new Switch2InputReader(), new Switch2BleInputReader(), new PlayStationInputReader(), new SwitchProInputReader(), new XInputReader()],
        ["NS2 Pro", L.T("NS2 Pro（藍牙）", "NS2 Pro (Bluetooth)"), "DualShock 4／DualSense", "Switch Pro", "Xbox／XInput"]) { }

    public AutoInputReader(IInputReader switch2, IInputReader xinput) : this([switch2, xinput], ["NS2 Pro", "Xbox／XInput"]) { }

    public AutoInputReader(IInputReader[] sources, string[] names)
    {
        _sources = sources;
        _names = names;
    }

    public string Status
    {
        get
        {
            int active = _active;
            if (active >= 0)
                return L.T($"自動偵測 → {SourceStatus(active)}", $"Auto-detect → {SourceStatus(active)}");
            return SearchingStatus(_sources, _names, _lastError, L.Chinese);
        }
    }

    /// <summary>
    /// One short line while searching: the reader names, plus only the readers that report a real problem
    /// (plain waiting / not connected texts are left out).
    /// </summary>
    internal static string SearchingStatus(IInputReader[] sources, string[] names, string lastError, bool chinese)
    {
        var shown = new List<string>();
        var problems = new List<string>();
        for (int i = 0; i < sources.Length; i++)
        {
            bool xbox = sources[i] is XInputReader || names[i] == XboxName;
            string label = xbox ? "Xbox" : ListName(names[i]);
            if (!shown.Contains(label)) shown.Add(label);
            if (xbox) continue; // Its status is only ever "not connected" while searching.
            string text = TrimEnd(sources[i].Status);
            string baseName = BaseName(names[i]);
            string bare = StripNamePrefix(text, names[i], baseName);
            if (bare.Length == 0 || IsWaiting(bare)) continue;
            if (!text.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)) text = $"{names[i]}{(chinese ? "：" : ": ")}{text}";
            if (!problems.Contains(text)) problems.Add(text);
        }
        string error = TrimEnd(lastError);
        if (error.Length > 0 && !problems.Contains(error)) problems.Add(error);

        string list = string.Join(chinese ? "、" : ", ", shown);
        string head = chinese ? $"自動偵測：尋找手把中（{list}）" : $"Auto-detect: looking for a controller ({list})";
        if (problems.Count == 0) return head;
        return head + (chinese ? "：" : ": ") + string.Join(chinese ? "；" : "; ", problems);
    }

    private const string XboxName = "Xbox／XInput";

    private static readonly string[] WaitingPrefixes =
    [
        "尋找", "Looking for", "等待", "Waiting for", "未連線", "not connected", "裝置沒有連線", "device not connected",
        "初始化", "Initializing", "正在連線", "Connecting",
    ];

    /// <summary>Removes a leading "NS2 Pro：" / "NS2 Pro: " style reader-name prefix (the separator must follow the name directly).</summary>
    private static string StripNamePrefix(string text, string name, string baseName)
    {
        foreach (var candidate in new[] { name, baseName })
        {
            if (candidate.Length == 0 || !text.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)) continue;
            string rest = text[candidate.Length..];
            if (rest.StartsWith('：') || rest.StartsWith(':')) return rest[1..].Trim();
        }
        return text;
    }

    private static bool IsWaiting(string text) => WaitingPrefixes.Any(p => text.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Name up to the first "（", " (" or "／" qualifier, e.g. "NS2 Pro（藍牙）" becomes "NS2 Pro".</summary>
    private static string BaseName(string name)
    {
        int cut = name.IndexOfAny(['（', '／', '(']);
        return (cut > 0 ? name[..cut] : name).Trim();
    }

    /// <summary>Name for the list: cut at "（" or " (" only, so "DualShock 4／DualSense" stays whole.</summary>
    private static string ListName(string name)
    {
        int cut = name.IndexOfAny(['（', '(']);
        return (cut > 0 ? name[..cut] : name).Trim();
    }

    private static string TrimEnd(string text) => text.Trim().TrimEnd('。', '；', '.', ';', '，', ',', ' ');

    private string SourceStatus(int index) => _sources[index] is XInputReader ? L.T("Xbox／XInput", "Xbox / XInput") : _sources[index].Status;

    /// <summary>Name of the source currently being mapped, or null while none is available.</summary>
    public string? ActiveSource => _active < 0 ? null : _names[_active];

    /// <summary>The reader currently in use, or null while none is available. A snapshot; may change right after.</summary>
    public IInputReader? ActiveReader
    {
        get { int active = _active; return active < 0 ? null : _sources[active]; }
    }

    public bool TryGetState(out State state)
    {
        Span<bool> available = stackalloc bool[_sources.Length];
        var states = new State[_sources.Length];
        for (int i = 0; i < _sources.Length; i++) available[i] = SafeRead(_sources[i], out states[i]);

        int active = _active;
        if (active < 0 || !available[active])
        {
            // Current source vanished: prefer one in use, then the first available.
            active = -1;
            for (int i = 0; i < _sources.Length && active < 0; i++)
                if (available[i] && IsInUse(states[i])) active = i;
            for (int i = 0; i < _sources.Length && active < 0; i++)
                if (available[i]) active = i;
        }
        else if (!IsInUse(states[active]))
        {
            for (int i = 0; i < _sources.Length; i++)
                if (i != active && available[i] && IsInUse(states[i])) { active = i; break; }
        }
        if (active != _active) MoveRumble(_active, active);
        _active = active;
        state = active < 0 ? default : states[active];
        return active >= 0;
    }

    public bool TryGetMotion(out MotionSample motion)
    {
        motion = default;
        int active = _active;
        return active >= 0 && _sources[active] is IMotionSource source && source.TryGetMotion(out motion);
    }

    /// <summary>Battery of the source currently in use; unknown while none is active.</summary>
    public BatteryStatus Battery
    {
        get
        {
            int active = _active;
            if (active < 0 || _sources[active] is not IBatterySource source) return BatteryStatus.Unknown;
            try { return source.Battery; }
            catch { return BatteryStatus.Unknown; }
        }
    }

    /// <summary>Identity of the source currently in use; null while none is active or it cannot say.</summary>
    public ControllerId? Identity
    {
        get
        {
            int active = _active;
            if (active < 0 || _sources[active] is not IControllerIdentity source) return null;
            try { return source.Identity; }
            catch { return null; }
        }
    }

    public void SetRumble(byte large, byte small)
    {
        _rumble = (large << 8) | small;
        int active = _active;
        if (active >= 0) Rumble(active, large, small);
    }

    private void MoveRumble(int from, int to)
    {
        if (from >= 0) Rumble(from, 0, 0);
        int value = _rumble;
        if (to >= 0 && value != 0) Rumble(to, (byte)(value >> 8), (byte)value);
    }

    private void Rumble(int index, byte large, byte small)
    {
        try { (_sources[index] as IRumbleTarget)?.SetRumble(large, small); }
        catch { /* Rumble is best-effort. */ }
    }

    private bool SafeRead(IInputReader source, out State state)
    {
        try { return source.TryGetState(out state); }
        catch (Exception ex)
        {
            // One failing backend must not stop the other from being mapped.
            _lastError = ex.Message;
            state = default;
            return false;
        }
    }

    public static bool IsInUse(State state)
    {
        var gp = state.Gamepad;
        return gp.Buttons != GamepadButtonFlags.None
            || gp.LeftTrigger > TriggerThreshold || gp.RightTrigger > TriggerThreshold
            || Math.Abs((int)gp.LeftThumbX) > StickThreshold || Math.Abs((int)gp.LeftThumbY) > StickThreshold
            || Math.Abs((int)gp.RightThumbX) > StickThreshold || Math.Abs((int)gp.RightThumbY) > StickThreshold;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? errors = null;
        foreach (var source in _sources)
        {
            try { source.Dispose(); }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (errors is not null) throw new AggregateException(errors);
    }
}
