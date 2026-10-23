using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation.Metadata;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// One NS2 Pro over Bluetooth LE through the PC's own Bluetooth adapter (experimental). Windows does
/// not expose it as a HID controller, so TSCC_WASD connects over GATT itself; Steam and games never
/// see it, and nothing has to be hidden. The controller is not paired: it connects while Sync is
/// held and keeps its Switch 2 pairing.
/// </summary>
public sealed class Switch2BleInputReader : IInputReader, IMotionSource, IRumbleTarget
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RumbleInterval = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly Switch2CalibrationCache _calibrationCache;
    private State _latest;
    private MotionSample _motion;
    private bool _hasState, _hasMotion;
    private long _receivedAt;
    private volatile int _rumble; // large << 8 | small
    private volatile bool _calibrationChanged;
    private string _status = L.T("等待 NS2 Pro 藍牙：按住手把上的 Sync 鍵（實驗性）", "Waiting for an NS2 Pro over Bluetooth: hold its Sync button (experimental)");
    private bool _disposed;

    public Switch2BleInputReader(Switch2CalibrationCache? calibrationCache = null)
    {
        _calibrationCache = calibrationCache ?? new Switch2CalibrationCache();
        Switch2CalibrationCache.UserCalibrationChanged += OnCalibrationChanged;
        _worker = Task.Run(() => RunAsync(_stop.Token));
    }

    public string Status { get { lock (_gate) return _status; } }

    public bool TryGetState(out State state)
    {
        lock (_gate)
        {
            bool fresh = _hasState && Stopwatch.GetElapsedTime(_receivedAt) < Fresh;
            state = fresh ? _latest : default;
            return fresh;
        }
    }

    public bool TryGetMotion(out MotionSample motion)
    {
        lock (_gate)
        {
            bool fresh = _hasMotion && Stopwatch.GetElapsedTime(_receivedAt) < Fresh;
            motion = fresh ? _motion : default;
            return fresh;
        }
    }

    public void SetRumble(byte large, byte small) => _rumble = (large << 8) | small;

    private void OnCalibrationChanged() => _calibrationChanged = true;

    private void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
            _hasState = _hasMotion = false;
            _latest = default;
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(token).ConfigureAwait(false);
                    if (adapter is null || !adapter.IsLowEnergySupported || !adapter.IsCentralRoleSupported)
                        throw new IOException(L.T("這台電腦沒有支援藍牙 LE 的藍牙介面卡（或藍牙已關閉）。", "This PC has no Bluetooth LE adapter, or Bluetooth is off."));
                    var (address, type) = await FindAsync(adapter.BluetoothAddress, token).ConfigureAwait(false);
                    AppLog.Info($"NS2 Pro Bluetooth: connecting to {address:X12}");
                    SetStatus(L.T("正在連線 NS2 Pro（藍牙）...", "Connecting to the NS2 Pro (Bluetooth)..."));
                    await RunConnectionAsync(address, type, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // WinRT reports unreachable devices and GATT failures as COMException.
                    AppLog.Warn($"NS2 Pro Bluetooth: {ex.GetType().Name}: {ex.Message}");
                    SetStatus(L.T($"NS2 Pro 藍牙：{ex.Message}", $"NS2 Pro Bluetooth: {ex.Message}"));
                }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { SetStatus(L.T($"NS2 Pro 藍牙輸入已停止：{ex.Message}", $"NS2 Pro Bluetooth input stopped: {ex.Message}")); }
        finally { lock (_gate) { _hasState = _hasMotion = false; _latest = default; } }
    }

    /// <summary>Waits for an NS2 Pro advertising in sync mode (or one already paired with this PC by another tool).</summary>
    private static async Task<(ulong Address, BluetoothAddressType Type)> FindAsync(ulong host, CancellationToken token)
    {
        var found = new TaskCompletionSource<(ulong, BluetoothAddressType)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        var logged = new HashSet<(ulong, ulong)>();
        watcher.Received += (_, e) =>
        {
            foreach (var data in e.Advertisement.ManufacturerData)
            {
                if (data.CompanyId != Switch2BleProtocol.NintendoCompanyId
                    || !Switch2BleProtocol.TryParseAdvertisement(data.Data.ToArray(), out ushort vid, out ushort pid, out ulong target)
                    || vid != Switch2BleProtocol.NintendoVendor || pid != Switch2BleProtocol.ProController2) continue;
                lock (logged)
                    if (logged.Add((e.BluetoothAddress, target)))
                        AppLog.Info($"NS2 Pro Bluetooth advertisement from {e.BluetoothAddress:X12}, host {(target == 0 ? "none (Sync held)" : target.ToString("X12"))}");
                if (target == 0 || target == host) found.TrySetResult((e.BluetoothAddress, e.BluetoothAddressType));
            }
        };
        watcher.Stopped += (_, e) => { if (e.Error != BluetoothError.Success) found.TrySetException(new IOException(L.T($"藍牙掃描停止（{e.Error}）。", $"Bluetooth scanning stopped ({e.Error})."))); };
        watcher.Start();
        AppLog.Info($"NS2 Pro Bluetooth: scanning (watcher {watcher.Status})");
        try
        {
            using var registration = token.Register(() => found.TrySetCanceled(token));
            return await found.Task.ConfigureAwait(false);
        }
        finally { watcher.Stop(); }
    }

    private async Task RunConnectionAsync(ulong address, BluetoothAddressType type, CancellationToken token)
    {
        using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address, type).AsTask(token).ConfigureAwait(false)
            ?? throw new IOException(L.T("找不到 NS2 Pro 藍牙裝置。", "The NS2 Pro Bluetooth device was not found."));
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        device.ConnectionStatusChanged += (d, _) => { if (d.ConnectionStatus == BluetoothConnectionStatus.Disconnected) disconnected.TrySetResult(); };
        // Windows 11 can shorten the connection interval: about 67 reports/s instead of the default rate.
        if (ApiInformation.IsTypePresent("Windows.Devices.Bluetooth.BluetoothLEPreferredConnectionParameters"))
        {
            try { device.RequestPreferredConnectionParameters(BluetoothLEPreferredConnectionParameters.ThroughputOptimized); }
            catch { /* Optional; the default parameters still work. */ }
        }

        var result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(token).ConfigureAwait(false);
        if (result.Status != GattCommunicationStatus.Success)
            throw new IOException(L.T($"無法讀取 NS2 Pro 的 GATT 服務（{result.Status}）。", $"Could not read the NS2 Pro's GATT services ({result.Status})."));
        var services = result.Services;
        try
        {
            var characteristics = new Dictionary<Guid, GattCharacteristic>();
            foreach (var service in services)
            {
                var found = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(token).ConfigureAwait(false);
                if (found.Status == GattCommunicationStatus.Success)
                    foreach (var characteristic in found.Characteristics) characteristics.TryAdd(characteristic.Uuid, characteristic);
            }
            GattCharacteristic Required(Guid uuid) => characteristics.TryGetValue(uuid, out var c) ? c
                : throw new IOException(L.T("這支手把沒有 NS2 Pro 的藍牙介面。", "This controller does not offer the NS2 Pro Bluetooth interface."));
            var input = Required(Switch2BleProtocol.InputReport);
            var commands = new CommandChannel(Required(Switch2BleProtocol.CommandWrite), Required(Switch2BleProtocol.CommandResponse));
            var vibration = Required(Switch2BleProtocol.Vibration);
            await commands.StartAsync(token).ConfigureAwait(false);

            SetStatus(L.T("讀取 NS2 Pro 搖桿校準（藍牙）...", "Reading NS2 Pro stick calibration (Bluetooth)..."));
            var left = await ReadCalibrationAsync(commands, Switch2BleProtocol.LeftUserCalibration, Switch2BleProtocol.LeftFactoryCalibration, token).ConfigureAwait(false);
            var right = await ReadCalibrationAsync(commands, Switch2BleProtocol.RightUserCalibration, Switch2BleProtocol.RightFactoryCalibration, token).ConfigureAwait(false);
            if (!left.IsValid || !right.IsValid)
                throw new IOException(L.T("NS2 Pro 搖桿校準資料無效。", "NS2 Pro stick calibration data is invalid."));
            var factory = (left, right);
            bool userCalibration = _calibrationCache.TryLoadUser(out var userLeft, out var userRight);
            var calibration = userCalibration ? (userLeft, userRight) : factory;
            string name = userCalibration ? L.T("NS2 Pro（藍牙，使用者校準）", "NS2 Pro (Bluetooth, your calibration)")
                : L.T("NS2 Pro（藍牙，原廠校準）", "NS2 Pro (Bluetooth, factory calibration)");

            var report = new byte[64];
            input.ValueChanged += (_, e) =>
            {
                if (_calibrationChanged)
                {
                    // The calibration wizard saved or cleared a calibration while mapping.
                    _calibrationChanged = false;
                    userCalibration = _calibrationCache.TryLoadUser(out userLeft, out userRight);
                    calibration = userCalibration ? (userLeft, userRight) : factory;
                }
                var data = e.CharacteristicValue.ToArray();
                lock (_gate)
                {
                    if (!Switch2BleProtocol.TryConvertReport(data, report)
                        || !Switch2ReportParser.TryParse(report, calibration.Item1, calibration.Item2, out var state)) return;
                    _hasMotion = Switch2ReportParser.TryParseMotion(report, out _motion);
                    _latest = state;
                    _receivedAt = Stopwatch.GetTimestamp();
                    _hasState = true;
                    _status = name;
                }
            };
            await Subscribe(input, GattClientCharacteristicConfigurationDescriptorValue.Notify, token).ConfigureAwait(false);
            await commands.SendAsync(Switch2BleProtocol.FeatureInit(Switch2BleProtocol.Features), token).ConfigureAwait(false);
            await commands.SendAsync(Switch2BleProtocol.FeatureEnable(Switch2BleProtocol.Features), token).ConfigureAwait(false);
            try { await commands.SendAsync(Switch2BleProtocol.PlayerLed(0x01), token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or TimeoutException) { /* The LED is cosmetic. */ }

            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var rumbleTask = RunRumbleAsync(vibration, connectionCts.Token);
            try
            {
                long started = Stopwatch.GetTimestamp();
                while (!disconnected.Task.IsCompleted)
                {
                    await Task.WhenAny(disconnected.Task, Task.Delay(500, token)).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    long last;
                    lock (_gate) last = _hasState ? _receivedAt : started;
                    if (Stopwatch.GetElapsedTime(last) > TimeSpan.FromSeconds(2)) break;
                }
            }
            finally
            {
                connectionCts.Cancel();
                await rumbleTask.ConfigureAwait(false);
                try { await Subscribe(input, GattClientCharacteristicConfigurationDescriptorValue.None, CancellationToken.None).ConfigureAwait(false); }
                catch { /* Already disconnected. */ }
            }
            throw new IOException(L.T("NS2 Pro 藍牙已斷線；再按住 Sync 鍵即可重新連線。", "The NS2 Pro Bluetooth connection ended; hold Sync again to reconnect."));
        }
        finally
        {
            foreach (var service in services) service.Dispose();
        }
    }

    private static async Task<Switch2StickCalibration> ReadCalibrationAsync(CommandChannel commands, uint user, uint factory, CancellationToken token)
    {
        var data = await commands.ReadMemoryAsync(0x0B, user, token).ConfigureAwait(false);
        if (!Switch2BleProtocol.IsUnset(data))
        {
            var calibration = Switch2StickCalibration.Parse(data);
            if (calibration.IsValid) return calibration;
        }
        return Switch2StickCalibration.Parse(await commands.ReadMemoryAsync(0x0B, factory, token).ConfigureAwait(false));
    }

    private static async Task Subscribe(GattCharacteristic characteristic, GattClientCharacteristicConfigurationDescriptorValue value, CancellationToken token)
    {
        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(value).AsTask(token).ConfigureAwait(false);
        if (status != GattCommunicationStatus.Success)
            throw new IOException(L.T($"無法訂閱 NS2 Pro 藍牙通知（{status}）。", $"Could not subscribe to NS2 Pro Bluetooth notifications ({status})."));
    }

    /// <summary>Streams rumble packets while a motor is on, then one stop packet. Best-effort.</summary>
    private async Task RunRumbleAsync(GattCharacteristic vibration, CancellationToken token)
    {
        int sequence = 0, sent = 0;
        try
        {
            using var timer = new PeriodicTimer(RumbleInterval);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                int value = _rumble;
                if (value == 0 && sent == 0) continue;
                await vibration.WriteValueWithResultAsync(Switch2BleProtocol.BuildRumble((byte)(value >> 8), (byte)value, sequence++).AsBuffer(),
                    GattWriteOption.WriteWithoutResponse).AsTask(token).ConfigureAwait(false);
                sent = value;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Rumble is best-effort; a lost connection is handled by the caller. */ }
        finally
        {
            try
            {
                // Never leave the motors running when mapping stops or the controller disconnects.
                if (sent != 0)
                    await vibration.WriteValueWithResultAsync(Switch2BleProtocol.BuildRumble(0, 0, sequence).AsBuffer(),
                        GattWriteOption.WriteWithoutResponse).AsTask().ConfigureAwait(false);
            }
            catch { /* Already disconnected. */ }
            _rumble = 0;
        }
    }

    /// <summary>Commands are written to one characteristic and answered by a notification on another, one at a time.</summary>
    private sealed class CommandChannel(GattCharacteristic write, GattCharacteristic response)
    {
        private readonly SemaphoreSlim _one = new(1, 1);
        private TaskCompletionSource<byte[]>? _pending;

        public async Task StartAsync(CancellationToken token)
        {
            response.ValueChanged += (_, e) => _pending?.TrySetResult(e.CharacteristicValue.ToArray());
            await Subscribe(response, GattClientCharacteristicConfigurationDescriptorValue.Notify, token).ConfigureAwait(false);
        }

        public async Task<byte[]> SendAsync(byte[] command, CancellationToken token)
        {
            await _one.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var pending = _pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                var status = await write.WriteValueWithResultAsync(command.AsBuffer(), GattWriteOption.WriteWithoutResponse).AsTask(token).ConfigureAwait(false);
                if (status.Status != GattCommunicationStatus.Success)
                    throw new IOException(L.T($"NS2 Pro 藍牙指令失敗（{status.Status}）。", $"An NS2 Pro Bluetooth command failed ({status.Status})."));
                var reply = await pending.Task.WaitAsync(CommandTimeout, token).ConfigureAwait(false);
                if (!Switch2BleProtocol.TryParseReply(reply, command[0], out var payload))
                    throw new IOException(L.T("NS2 Pro 藍牙回應格式不符。", "Unexpected NS2 Pro Bluetooth reply."));
                return payload.ToArray();
            }
            finally
            {
                _pending = null;
                _one.Release();
            }
        }

        public async Task<byte[]> ReadMemoryAsync(byte length, uint address, CancellationToken token)
        {
            var payload = await SendAsync(Switch2BleProtocol.BuildReadMemory(length, address), token).ConfigureAwait(false);
            if (!Switch2BleProtocol.TryParseMemory(payload, length, address, out var data))
                throw new IOException(L.T("NS2 Pro 藍牙記憶體讀取回應不符。", "Unexpected NS2 Pro Bluetooth memory reply."));
            return data.ToArray();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Switch2CalibrationCache.UserCalibrationChanged -= OnCalibrationChanged;
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _stop.Dispose();
    }
}
