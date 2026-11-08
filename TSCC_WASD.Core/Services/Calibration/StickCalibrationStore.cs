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
    private readonly Func<string, string?> _parentOf;
    private readonly object _gate = new();
    private volatile Dictionary<string, StickCalibrationSet> _entries = [];
    private int _version;
    private bool _badFileLogged;

    public StickCalibrationStore(string? path = null, Switch2CalibrationCache? ns2Cache = null,
        Func<string, string?>? parentOf = null)
    {
        _parentOf = parentOf ?? WindowsDevicePaths.ParentOf;
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
    public bool Save(string key, StickCalibrationSet calibration) => Save(key, calibration, null);

    /// <summary>Saves under <see cref="ControllerId.PreferredKey"/> and removes the identity's legacy key in the same write.</summary>
    public bool Save(ControllerId identity, StickCalibrationSet calibration) =>
        Save(identity.PreferredKey, calibration, identity.LegacyDeviceKey);

    private bool Save(string key, StickCalibrationSet calibration, string? legacyKey)
    {
        if (string.IsNullOrWhiteSpace(key) || calibration is null || !calibration.IsValid) return false;
        lock (_gate)
        {
            var next = new Dictionary<string, StickCalibrationSet>(_entries) { [key] = calibration };
            if (legacyKey is not null && legacyKey != key) next.Remove(legacyKey);
            if (!Write(next)) return false;
            _entries = next;
            if (ControllerId.ModelOfKey(key) == ControllerId.Ns2Pro) ClearOldNs2();
            Interlocked.Increment(ref _version);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// The wizard calls this when calibration starts. For an NS2 Pro it clears the old user calibration in
    /// <c>ns2-calibration.json</c> first, so the reader hands out factory-calibrated samples from then on and the
    /// samples are measured in the same domain the saved calibration is applied in (Save no longer changes it).
    /// Other controllers have nothing to prepare.
    /// </summary>
    public void BeginCalibration(ControllerId identity)
    {
        if (identity.Model == ControllerId.Ns2Pro) ClearOldNs2();
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

    /// <summary>Removes several entries in one write. Returns true when at least one existed and the file was written.</summary>
    public bool Clear(IEnumerable<string> keys)
    {
        lock (_gate)
        {
            var next = new Dictionary<string, StickCalibrationSet>(_entries);
            bool any = false;
            foreach (string key in keys) any |= next.Remove(key);
            if (!any || !Write(next)) return false;
            _entries = next;
            Interlocked.Increment(ref _version);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Call once when a controller connects (not per lookup). If the controller is keyed by its Bluetooth address, old
    /// entries of the same model keyed by a HID instance id whose parent has that same address (left behind by earlier
    /// pairings) are merged into the address key: the one for the current instance id if there is one, otherwise the
    /// last one in the file is copied there unless the address key already has an entry. All of them are then removed,
    /// in one write. Entries of other addresses, or whose address cannot be resolved, are left alone.
    /// Returns true when the file changed.
    /// </summary>
    public bool AdoptLegacy(ControllerId identity)
    {
        if (identity.DeviceKey is not { } newKey || identity.LegacyDeviceId is null || string.IsNullOrWhiteSpace(identity.DeviceId)) return false;
        string address = identity.DeviceId.Trim().ToLowerInvariant();
        string prefix = identity.Model + "#";
        string? currentLegacy = identity.LegacyDeviceKey;
        lock (_gate)
        {
            var old = new List<string>();
            foreach (string key in _entries.Keys)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal) || key == newKey) continue;
                string instance = key[prefix.Length..];
                if (!instance.StartsWith(@"hid\", StringComparison.OrdinalIgnoreCase)) continue;
                if (key == currentLegacy || HidDevice.BluetoothAddress(instance, _parentOf) == address) old.Add(key);
            }
            if (old.Count == 0) return false;
            var next = new Dictionary<string, StickCalibrationSet>(_entries);
            if (!next.ContainsKey(newKey))
            {
                string source = currentLegacy is not null && old.Contains(currentLegacy) ? currentLegacy : old[^1];
                next[newKey] = _entries[source];
            }
            foreach (string key in old) next.Remove(key);
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
