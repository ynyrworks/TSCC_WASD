using System.Diagnostics;
using TSCC_WASD.Core.Services.Input;
using TSCC_WASD.Core.Services.Mapping;

namespace TSCC_WASD.Core.Services.Calibration;

/// <summary>
/// Logic of the stick calibration wizard for one controller (no UI): release the sticks, circle them, save.
/// Starting it calls <see cref="StickCalibrationStore.BeginCalibration"/>; disposing it before a successful save
/// puts the previous calibration back exactly (including an old NS2 Pro user calibration in <c>ns2-calibration.json</c>).
/// </summary>
public sealed class CalibrationWizardModel : IDisposable
{
    public enum Step { Center, MeasuringCenter, Range, Saved }

    private readonly StickCalibrationStore _store;
    private readonly Switch2CalibrationCache _ns2Cache;
    private readonly Func<MappingSession.InputSnapshot?> _source;
    private GenericStickCalibrator _calibrator = new();
    private Switch2StickCalibration? _oldNs2Left, _oldNs2Right;
    private bool _restoreNs2, _disposed;
    private readonly Func<long> _nowMs;
    private long _lastAvailableMs;
    /// <summary>Input unavailable for this long during a step counts as "the controller is not sending data".</summary>
    public const int NoInputMs = 2000;

    public CalibrationWizardModel(ControllerId identity, string controllerName, Func<MappingSession.InputSnapshot?> source,
        StickCalibrationStore store, Switch2CalibrationCache ns2Cache, Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
        _lastAvailableMs = _nowMs();
        Identity = identity;
        ControllerName = controllerName;
        _source = source;
        _store = store;
        _ns2Cache = ns2Cache;
        if (identity.Model == ControllerId.Ns2Pro && ns2Cache.TryLoadUser(out var left, out var right))
        {
            (_oldNs2Left, _oldNs2Right, _restoreNs2) = (left, right, true);
        }
        store.BeginCalibration(identity);
    }

    public ControllerId Identity { get; }
    public string ControllerName { get; }

    private Step _step = Step.Center;
    public Step State => _step;

    /// <summary>True while the latest reading is usable (mapping runs and the controller answers).</summary>
    public bool InputAvailable { get; private set; }

    /// <summary>Latest uncalibrated sticks for display, valid while <see cref="InputAvailable"/>.</summary>
    public Gamepad Sticks { get; private set; }

    /// <summary>Why the centre step last restarted (cleared when a centre was accepted or the step is left).</summary>
    public CenterCheck CenterProblem { get; private set; }

    /// <summary>True while measuring or circling and the reader reported no data for about 2 seconds.</summary>
    public bool NoReports { get; private set; }

    public IReadOnlyList<double> Progress => _calibrator.Progress;
    public bool CanAdvance => _step switch
    {
        Step.Center => InputAvailable,
        Step.Range => _calibrator.RangeReady,
        _ => false
    };

    /// <summary>True when a calibration is stored for this controller (device or model entry) or an old NS2 Pro one exists.</summary>
    public bool HasStoredCalibration => _store.TryGet(Identity, out _) || (_restoreNs2 && !_disposed);

    /// <summary>Set when the save failed (invalid result or the file could not be written).</summary>
    public bool SaveFailed { get; private set; }

    /// <summary>Reads one input sample and feeds the current step.</summary>
    public void Tick()
    {
        if (_disposed || _step == Step.Saved) return;
        var snapshot = _source();
        InputAvailable = snapshot is { Available: true };
        long now = _nowMs();
        if (InputAvailable) _lastAvailableMs = now;
        NoReports = _step is Step.MeasuringCenter or Step.Range && now - _lastAvailableMs >= NoInputMs;
        if (!InputAvailable) return;
        var pad = snapshot!.State.Gamepad;
        Sticks = pad;
        switch (_step)
        {
            case Step.MeasuringCenter:
                _calibrator.AddCenterSample(pad);
                if (_calibrator.CenterReady)
                {
                    CenterProblem = _calibrator.CheckCenter();
                    if (CenterProblem == CenterCheck.Ok) _step = Step.Range;
                    else _calibrator = new GenericStickCalibrator();
                }
                break;
            case Step.Range:
                _calibrator.AddRangeSample(pad);
                break;
        }
    }

    /// <summary>Next button: start measuring the centre, or save once the range is complete. Returns true when saved.</summary>
    public bool Advance()
    {
        if (_disposed || !CanAdvance) return false;
        if (_step == Step.Center)
        {
            _calibrator = new GenericStickCalibrator();
            _step = Step.MeasuringCenter;
            CenterProblem = CenterCheck.Ok;
            _lastAvailableMs = _nowMs();
            return false;
        }
        SaveFailed = false;
        if (!_store.Save(Identity.PreferredKey, _calibrator.Result()))
        {
            SaveFailed = true;
            return false;
        }
        _restoreNs2 = false;
        _step = Step.Saved;
        return true;
    }

    /// <summary>
    /// Removes the stored calibration of this controller: its own device entry when it has one (other controllers of the
    /// same model keep theirs), otherwise the model entry; plus an old NS2 Pro user calibration.
    /// Takes effect at once; a later cancel does not bring it back. Returns true when something was removed.
    /// </summary>
    public bool Clear()
    {
        bool removed = false;
        if (Identity.DeviceKey is { } deviceKey && _store.TryGet(deviceKey, out _)) removed = _store.Clear(deviceKey);
        else removed = _store.Clear(Identity.ModelKey);
        if (Identity.Model == ControllerId.Ns2Pro && (_restoreNs2 || _ns2Cache.TryLoadUser(out _, out _)))
        {
            _ns2Cache.ClearUser();
            removed = true;
        }
        _restoreNs2 = false;
        return removed;
    }

    /// <summary>Cancel or close: puts back what <see cref="StickCalibrationStore.BeginCalibration"/> cleared. Safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_restoreNs2 && _oldNs2Left is { } left && _oldNs2Right is { } right) _ns2Cache.SaveUser(left, right);
        _restoreNs2 = false;
    }
}
