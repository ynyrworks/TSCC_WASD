using System.Text.Json;
using TSCC_WASD.Core.Services.Input;

namespace TSCC_WASD.Core.Services.Calibration;

/// <summary>
/// Generic stick calibrations keyed by controller (see <see cref="ControllerId"/>), in one JSON file.
/// Held in memory: lookups never touch the disk, and a missing or bad file is ignored (logged once).
/// </summary>
public sealed class StickCalibrationStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly Lazy<StickCalibrationStore> SharedStore = new(() => new StickCalibrationStore());

    /// <summary>The store the app uses; the wizard and the mapping sessions share it.</summary>
    public static StickCalibrationStore Shared => SharedStore.Value;

    private readonly string _path;
    private readonly Switch2CalibrationCache _ns2Cache;
    private readonly object _gate = new();
    private volatile Dictionary<string, StickCalibrationSet> _entries = [];
    private int _version;
    private bool _badFileLogged;

    public StickCalibrationStore(string? path = null, Switch2CalibrationCache? ns2Cache = null)
    {
        _path = Path.GetFullPath(path ?? AppPaths.File("stick-calibration.json"));
        _ns2Cache = ns2Cache ?? new Switch2CalibrationCache();
        Reload(raiseEvent: false);
    }

    /// <summary>Raised after a calibration is saved or cleared (or the file is reloaded).</summary>
    public event Action? Changed;

    /// <summary>Increases with every change; a running session compares it to notice a new calibration.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Calibration for the controller: the per-device entry first, then the model entry.</summary>
    public bool TryGet(ControllerId identity, out StickCalibrationSet calibration)
    {
        var entries = _entries;
        foreach (string key in identity.LookupKeys())
            if (entries.TryGetValue(key, out calibration!)) return true;
        calibration = null!;
        return false;
    }

    public bool TryGet(string key, out StickCalibrationSet calibration)
    {
        if (_entries.TryGetValue(key, out calibration!)) return true;
        calibration = null!;
        return false;
    }

    /// <summary>
    /// Saves a calibration under <paramref name="key"/> (usually <see cref="ControllerId.PreferredKey"/>).
    /// For an NS2 Pro the older user calibration in <c>ns2-calibration.json</c> is cleared so the two never stack.
    /// Returns false (and changes nothing) when the calibration is invalid or the file cannot be written.
    /// </summary>
    public bool Save(string key, StickCalibrationSet calibration)
    {
        if (string.IsNullOrWhiteSpace(key) || calibration is null || !calibration.IsValid) return false;
        lock (_gate)
        {
            var next = new Dictionary<string, StickCalibrationSet>(_entries) { [key] = calibration };
            if (!Write(next)) return false;
            _entries = next;
            if (ControllerId.ModelOfKey(key) == ControllerId.Ns2Pro) ClearOldNs2();
            Interlocked.Increment(ref _version);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes one controller's calibration. Returns false when there was none or the file cannot be written.</summary>
    public bool Clear(string key)
    {
        lock (_gate)
        {
            if (!_entries.ContainsKey(key)) return false;
            var next = new Dictionary<string, StickCalibrationSet>(_entries);
            next.Remove(key);
            if (!Write(next)) return false;
            _entries = next;
            Interlocked.Increment(ref _version);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Reads the file again (for example after another process changed it).</summary>
    public void Reload() => Reload(raiseEvent: true);

    private void Reload(bool raiseEvent)
    {
        lock (_gate)
        {
            _entries = Read();
            Interlocked.Increment(ref _version);
        }
        if (raiseEvent) Changed?.Invoke();
    }

    private void ClearOldNs2()
    {
        try
        {
            if (_ns2Cache.TryLoadUser(out _, out _)) _ns2Cache.ClearUser();
        }
        catch (Exception ex) { AppLog.Warn($"Could not clear the old NS2 Pro calibration: {ex.Message}"); }
    }

    private Dictionary<string, StickCalibrationSet> Read()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            var file = JsonSerializer.Deserialize<FileData>(File.ReadAllText(_path), Options);
            var result = new Dictionary<string, StickCalibrationSet>();
            if (file?.Controllers is not null)
                foreach (var (key, value) in file.Controllers)
                    if (!string.IsNullOrWhiteSpace(key) && value is { IsValid: true }) result[key] = value;
            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (!_badFileLogged)
            {
                _badFileLogged = true;
                AppLog.Warn($"Ignoring the stick calibration file ({ex.GetType().Name}): {_path}");
            }
            return [];
        }
    }

    private bool Write(Dictionary<string, StickCalibrationSet> entries)
    {
        string temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(new FileData(1, entries), Options);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temp, _path, _path + ".bak");
            else File.Move(temp, _path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Could not save the stick calibration: {ex.Message}");
            return false;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private sealed record FileData(int Version, Dictionary<string, StickCalibrationSet>? Controllers);
}
