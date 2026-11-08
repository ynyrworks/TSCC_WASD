using System.Runtime.InteropServices;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>Readers whose Xbox View (Back) button can be sent as the DS4 touchpad click.</summary>
public interface IXboxBackButtonOption
{
    bool BackAsTouchpad { get; set; }
}

/// <summary>Readers that must skip the XInput slot of TSCC_WASD's own virtual Xbox 360 pad.</summary>
public interface IXInputSlotFilter
{
    int? ExcludedSlot { get; set; }
}

/// <summary>Reads the first available XInput slot (0–3) using the Windows inbox API.</summary>
public sealed class XInputReader : IInputReader, IRumbleTarget, IXboxBackButtonOption, IXInputSlotFilter, IBatterySource, IControllerIdentity
{
    private volatile int _excludedSlot = -1;

    /// <summary>Never read our own virtual Xbox 360 output back as input.</summary>
    public int? ExcludedSlot
    {
        get => _excludedSlot < 0 ? null : _excludedSlot;
        set => _excludedSlot = value ?? -1;
    }

    private volatile bool _backAsTouchpad = true;

    /// <summary>
    /// Xbox controllers have no touchpad button, so View (Back) stands in for it by default;
    /// otherwise View stays Back and becomes DS4 Share.
    /// </summary>
    public bool BackAsTouchpad { get => _backAsTouchpad; set => _backAsTouchpad = value; }

    public static GamepadButtonFlags MapBack(GamepadButtonFlags buttons, bool backAsTouchpad) =>
        backAsTouchpad && buttons.HasFlag(GamepadButtonFlags.Back)
            ? (buttons & ~GamepadButtonFlags.Back) | GamepadButtonFlags.Touchpad
            : buttons;

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputGetState(uint dwUserIndex, out State state);

    // Undocumented but long-stable export that also reports the Guide button (0x0400).
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputGetStateEx(uint dwUserIndex, out State state);

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputSetState(uint dwUserIndex, ref Vibration vibration);

    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, out BatteryInformation information);

    [StructLayout(LayoutKind.Sequential)]
    internal struct BatteryInformation
    {
        public byte BatteryType;  // 0 disconnected, 1 wired, 2 alkaline, 3 NiMH, 0xFF unknown
        public byte BatteryLevel; // 0 empty, 1 low, 2 medium, 3 full
    }

    private static bool _getStateExUnavailable;
    private volatile int _slot = -1;
    private int _rumble;

    internal delegate uint GetStateFunc(uint index, out State state);
    internal delegate void SetVibrationFunc(int slot, byte large, byte small);
    internal delegate uint GetBatteryFunc(uint index, out BatteryInformation information);

    private static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private readonly GetStateFunc _getState;
    private readonly SetVibrationFunc _setVibration;
    private readonly Func<long> _clockMs;
    private readonly GetBatteryFunc _getBattery;
    private readonly object _batteryGate = new();
    private BatteryStatus _battery;
    private int _batterySlot = -1;
    private long _nextBatteryRead;
    private long _nextFullScan;   // no pad in use: earliest time to look at all slots again
    private long _nextLowerScan;  // pad in use: earliest time to look for a lower-numbered pad
    private int _scannedExcluded = -1;

    public XInputReader() : this(GetState, Apply, () => Environment.TickCount64) { }

    internal XInputReader(GetStateFunc getState, SetVibrationFunc setVibration, Func<long> clockMs, GetBatteryFunc? getBattery = null)
    {
        _getBattery = getBattery ?? GetBattery;
        _getState = getState;
        _setVibration = setVibration;
        _clockMs = clockMs;
    }

    public bool TryGetState(out State state)
    {
        long now = _clockMs();
        int excluded = _excludedSlot;
        if (excluded != _scannedExcluded)
        {
            // The filter changed: look at every slot again right away.
            _scannedExcluded = excluded;
            _nextFullScan = 0;
            _nextLowerScan = 0;
            if (_slot == excluded) _slot = -1;
        }

        int active = _slot;
        if (active >= 0)
        {
            // Lowest usable slot wins, so an occupied slot only needs a periodic look at lower empty ones.
            if (now >= _nextLowerScan)
            {
                _nextLowerScan = now + (long)ScanInterval.TotalMilliseconds;
                if (ScanSlots(0, active, excluded, now, out state)) return true;
            }
            if (_getState((uint)active, out state) == 0)
            {
                state.Gamepad.Buttons = MapBack(state.Gamepad.Buttons, _backAsTouchpad);
                RefreshBattery(active, now);
                return true;
            }
            // The pad in use is gone: report no input now and look for another one on the next pass.
            _slot = -1;
            ClearBattery();
            _nextFullScan = 0;
            state = default;
            return false;
        }

        if (now >= _nextFullScan)
        {
            if (ScanSlots(0, 4, excluded, now, out state)) return true;
            _nextFullScan = now + (long)ScanInterval.TotalMilliseconds;
        }
        _slot = -1;
        state = default;
        return false;
    }

    private bool ScanSlots(int from, int to, int excluded, long now, out State state)
    {
        for (int index = from; index < to; index++)
        {
            if (index == excluded) continue;
            // 1167 = not connected. Other codes are transient driver states; treat the slot as empty
            // so a long-running mapping keeps going instead of ending on one bad read.
            if (_getState((uint)index, out state) == 0)
            {
                state.Gamepad.Buttons = MapBack(state.Gamepad.Buttons, _backAsTouchpad);
                if (_slot != index) MoveRumble(index);
                _nextLowerScan = now + (long)ScanInterval.TotalMilliseconds;
                RefreshBattery(index, now);
                return true;
            }
        }
        state = default;
        return false;
    }

    public BatteryStatus Battery { get { lock (_batteryGate) return _battery; } }

    /// <summary>XInput has no serial and the slot is not stable, so only the model identifies it.</summary>
    public ControllerId? Identity { get { lock (_batteryGate) return _batterySlot >= 0 ? XInputId : null; } }
    private static readonly ControllerId XInputId = new(ControllerId.XInput, null);

    private void ClearBattery()
    {
        lock (_batteryGate) { _battery = default; _batterySlot = -1; }
    }

    /// <summary>Asks for the battery when the pad changed or the last answer is older than the interval; never throws.</summary>
    private void RefreshBattery(int slot, long now)
    {
        lock (_batteryGate)
        {
            if (slot == _batterySlot && now < _nextBatteryRead) return;
            _batterySlot = slot;
            _nextBatteryRead = now + (long)BatteryInterval.TotalMilliseconds;
        }
        BatteryStatus battery;
        try { battery = MapBattery(_getBattery((uint)slot, out var information), information); }
        catch { battery = BatteryStatus.Unknown; }
        lock (_batteryGate) _battery = battery;
    }

    /// <summary>XInput result and battery info to a status. Wired is unsupported; disconnected or odd data is unknown; XInput has no charging flag.</summary>
    internal static BatteryStatus MapBattery(uint result, BatteryInformation information)
    {
        if (result != 0) return BatteryStatus.Unknown;
        return information.BatteryType switch
        {
            1 => BatteryStatus.Unsupported,
            2 or 3 => information.BatteryLevel switch
            {
                0 => BatteryStatus.Of(BatteryLevel.Empty, false),
                1 => BatteryStatus.Of(BatteryLevel.Low, false),
                2 => BatteryStatus.Of(BatteryLevel.Medium, false),
                3 => BatteryStatus.Of(BatteryLevel.Full, false),
                _ => BatteryStatus.Unknown
            },
            _ => BatteryStatus.Unknown
        };
    }

    private static uint GetBattery(uint index, out BatteryInformation information) =>
        XInputGetBatteryInformation(index, 0 /* BATTERY_DEVTYPE_GAMEPAD */, out information);

    private static uint GetState(uint index, out State state)
    {
        if (!_getStateExUnavailable)
        {
            try { return XInputGetStateEx(index, out state); }
            catch (EntryPointNotFoundException) { _getStateExUnavailable = true; }
        }
        return XInputGetState(index, out state);
    }

    public void SetRumble(byte large, byte small)
    {
        Interlocked.Exchange(ref _rumble, (large << 8) | small);
        int slot = _slot;
        if (slot >= 0) _setVibration(slot, large, small);
    }

    private void MoveRumble(int index)
    {
        int previous = _slot;
        _slot = index;
        if (previous >= 0) _setVibration(previous, 0, 0);
        int value = Volatile.Read(ref _rumble);
        if (value != 0) _setVibration(index, (byte)(value >> 8), (byte)value);
    }

    private static void Apply(int slot, byte large, byte small)
    {
        var vibration = new Vibration { LeftMotorSpeed = (ushort)(large * 257), RightMotorSpeed = (ushort)(small * 257) };
        XInputSetState((uint)slot, ref vibration); // Best-effort; a missing pad simply ignores it.
    }

    public void Dispose()
    {
        int slot = _slot;
        if (slot >= 0) _setVibration(slot, 0, 0);
    }
}
