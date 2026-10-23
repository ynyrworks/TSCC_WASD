// NS2 Pro Bluetooth LE probe: finds the controller, reads its info and stick calibration over GATT,
// subscribes to input reports and prints what arrives. Protocol facts (GATT UUIDs, command framing,
// memory addresses, report layout) follow the community reverse engineering credited in
// Nadeflore/switch2-controllers and CareyScott/switch2controllerpc (MIT); this code is our own.
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using TSCC_WASD.Core.Services.Input;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation.Metadata;
using Windows.Storage.Streams;

const ushort NintendoCompany = 0x0553, NintendoVendor = 0x057E, ProController2 = 0x2069;
var inputUuid = Guid.Parse("ab7de9be-89fe-49ad-828f-118f09df7fd2");
var commandUuid = Guid.Parse("649d4ac9-8eb7-4e6c-af44-1ea54fe5f005");
var responseUuid = Guid.Parse("c765a961-d9d8-4d36-a20a-5315b111836a");
var vibrationUuid = Guid.Parse("cc483f51-9258-427d-a939-630c31f72b05");

if (args.Contains("--reader"))
{
    // Exercise one of TSCC_WASD's own readers the way the mapping loop polls it.
    string kind = Array.IndexOf(args, "--reader") + 1 < args.Length ? args[Array.IndexOf(args, "--reader") + 1] : "ns2ble";
    using IInputReader reader = kind switch
    {
        "switchpro" => new SwitchProInputReader(),
        "auto" => new AutoInputReader(),
        _ => new Switch2BleInputReader()
    };
    Log($"Reader: {reader.GetType().Name}");
    string? lastStatus = null;
    var lastButtons = GamepadButtonFlags.None;
    State lastState = default;
    int polls = 0, fresh = 0;
    var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency * ArgInt("--seconds", 60);
    while (Stopwatch.GetTimestamp() < until)
    {
        bool ok = reader.TryGetState(out var s);
        polls++;
        if (ok) fresh++;
        if (reader.Status != lastStatus) Log($"Status: {lastStatus = reader.Status}");
        if (ok && (s.Gamepad.Buttons != lastButtons || Math.Abs(s.Gamepad.LeftThumbX - lastState.Gamepad.LeftThumbX) > 8000
            || Math.Abs(s.Gamepad.LeftThumbY - lastState.Gamepad.LeftThumbY) > 8000 || Math.Abs(s.Gamepad.RightThumbX - lastState.Gamepad.RightThumbX) > 8000
            || Math.Abs(s.Gamepad.RightThumbY - lastState.Gamepad.RightThumbY) > 8000 || s.Gamepad.LeftTrigger != lastState.Gamepad.LeftTrigger
            || s.Gamepad.RightTrigger != lastState.Gamepad.RightTrigger))
        {
            lastButtons = s.Gamepad.Buttons;
            lastState = s;
            bool motion = reader is IMotionSource source && source.TryGetMotion(out _);
            Log($"  {s.Gamepad.Buttons} L({s.Gamepad.LeftThumbX},{s.Gamepad.LeftThumbY}) R({s.Gamepad.RightThumbX},{s.Gamepad.RightThumbY}) L2/R2 {s.Gamepad.LeftTrigger}/{s.Gamepad.RightTrigger} motion={motion}");
            if (reader is IRumbleTarget target) target.SetRumble(s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y) ? (byte)200 : (byte)0, s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y) ? (byte)200 : (byte)0);
        }
        await Task.Delay(4); // 250 Hz, the default polling rate.
    }
    Log($"Polls {polls}, fresh {fresh} ({100.0 * fresh / polls:F1}%)");
    return 0;
}

bool pair = args.Contains("--pair");
bool rumble = args.Contains("--rumble");
int seconds = ArgInt("--seconds", 20);
int scanSeconds = ArgInt("--scan", 60);
byte features = (byte)ArgInt("--features", 0x27);

var adapter = await BluetoothAdapter.GetDefaultAsync() ?? throw new InvalidOperationException("No Bluetooth adapter.");
ulong host = adapter.BluetoothAddress;
Log($"Adapter {Mac(host)} LE={adapter.IsLowEnergySupported} central={adapter.IsCentralRoleSupported} OS {Environment.OSVersion.Version}");

// 1. Scan for a Pro Controller 2 advertisement (Sync held, or a button pressed when already paired here).
var found = new TaskCompletionSource<(ulong Address, BluetoothAddressType Type, bool Paired)>();
var seen = new HashSet<ulong>();
var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
watcher.Received += (_, e) =>
{
    foreach (var md in e.Advertisement.ManufacturerData)
    {
        if (md.CompanyId != NintendoCompany) continue;
        var data = md.Data.ToArray();
        if (data.Length < 16) { if (seen.Add(e.BluetoothAddress)) Log($"Nintendo ad from {Mac(e.BluetoothAddress)} too short: {Hex(data)}"); continue; }
        ushort vid = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(3)), pid = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5));
        ulong reconnect = ReadUInt48(data.AsSpan(10));
        if (seen.Add(e.BluetoothAddress))
            Log($"Ad {Mac(e.BluetoothAddress)} ({e.BluetoothAddressType}) rssi {e.RawSignalStrengthInDBm} vid {vid:X4} pid {pid:X4} host {(reconnect == 0 ? "none (sync mode)" : Mac(reconnect))} data {Hex(data)}");
        if (vid != NintendoVendor || pid != ProController2) continue;
        if (reconnect == 0 || reconnect == host) found.TrySetResult((e.BluetoothAddress, e.BluetoothAddressType, reconnect == host));
    }
};
watcher.Start();
Log($"Scanning {scanSeconds}s: hold the NS2 Pro's Sync button (or press a button if it was paired with this PC)...");
var winner = await Task.WhenAny(found.Task, Task.Delay(TimeSpan.FromSeconds(scanSeconds)));
watcher.Stop();
if (winner != found.Task) { Log("No NS2 Pro in sync mode found."); return 2; }
var (address, addressType, alreadyPaired) = found.Task.Result;
Log($"Using {Mac(address)}; already paired with this PC: {alreadyPaired}");

// 2. Connect and find the characteristics.
using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address, addressType)
    ?? throw new InvalidOperationException("FromBluetoothAddressAsync returned null.");
device.ConnectionStatusChanged += (d, _) => Log($"Connection status: {d.ConnectionStatus}");
if (ApiInformation.IsTypePresent("Windows.Devices.Bluetooth.BluetoothLEPreferredConnectionParameters"))
{
    try
    {
        var request = device.RequestPreferredConnectionParameters(BluetoothLEPreferredConnectionParameters.ThroughputOptimized);
        Log($"ThroughputOptimized request: {request.Status}");
    }
    catch (Exception ex) { Log($"ThroughputOptimized failed: {ex.Message}"); }
}
var services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
Log($"GATT services: {services.Status}, {services.Services.Count}");
var chars = new Dictionary<Guid, GattCharacteristic>();
foreach (var service in services.Services)
{
    var result = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
    Log($"  service {service.Uuid} ({result.Status})");
    foreach (var c in result.Characteristics)
    {
        Log($"    char {c.Uuid} {c.CharacteristicProperties}");
        chars.TryAdd(c.Uuid, c);
    }
}
foreach (var uuid in new[] { inputUuid, commandUuid, responseUuid, vibrationUuid })
    if (!chars.ContainsKey(uuid)) { Log($"Missing characteristic {uuid}"); return 3; }

// 3. Command channel: write on 649d…, the reply arrives as a notification on c765….
TaskCompletionSource<byte[]>? pending = null;
chars[responseUuid].ValueChanged += (_, e) =>
{
    var data = e.CharacteristicValue.ToArray();
    if (pending is null || !pending.TrySetResult(data)) Log($"Unsolicited response {Hex(data)}");
};
Log($"Response notify: {await chars[responseUuid].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify)}");

async Task<byte[]> Command(byte command, byte subcommand, byte[] payload)
{
    var buffer = new byte[8 + payload.Length];
    buffer[0] = command; buffer[1] = 0x91; buffer[2] = 0x01; buffer[3] = subcommand; buffer[5] = (byte)payload.Length;
    payload.CopyTo(buffer, 8);
    pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
    var c = chars[commandUuid];
    var option = c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
        ? GattWriteOption.WriteWithoutResponse : GattWriteOption.WriteWithResponse;
    var status = await c.WriteValueWithResultAsync(buffer.AsBuffer(), option);
    if (status.Status != GattCommunicationStatus.Success) throw new IOException($"Write {command:X2}/{subcommand:X2}: {status.Status}");
    var done = await Task.WhenAny(pending.Task, Task.Delay(2000));
    if (done != pending.Task) throw new TimeoutException($"No reply to {command:X2}/{subcommand:X2} ({Hex(buffer)})");
    var reply = pending.Task.Result;
    if (reply.Length < 8 || reply[0] != command || reply[1] != 0x01) throw new IOException($"Unexpected reply {Hex(reply)}");
    return reply[8..];
}

async Task<byte[]> ReadMemory(byte length, uint address)
{
    var payload = new byte[8];
    payload[0] = length; payload[1] = 0x7E;
    BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), address);
    var data = await Command(0x02, 0x04, payload);
    if (data.Length < 8 || data[0] != length || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != address)
        throw new IOException($"Unexpected memory reply {Hex(data)}");
    return data[8..];
}

var info = await ReadMemory(0x40, 0x13000);
Log($"Info: serial '{System.Text.Encoding.ASCII.GetString(info, 2, 14).TrimEnd('\0')}' vid {BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(18)):X4} pid {BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(20)):X4}");

async Task<Switch2StickCalibration> Calibration(uint user, uint factory, string name)
{
    var data = await ReadMemory(0x0B, user);
    bool userValid = !(data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF);
    if (!userValid) data = await ReadMemory(0x0B, factory);
    var calibration = Switch2StickCalibration.Parse(data);
    Log($"{name} stick ({(userValid ? "user" : "factory")}): {calibration} valid={calibration.IsValid}  raw {Hex(data)}");
    return calibration;
}
var left = await Calibration(0x1FC042, 0x130A8, "Left");
var right = await Calibration(0x1FC062, 0x130E8, "Right");
if (!left.IsValid) left = Switch2StickCalibration.Nominal;
if (!right.IsValid) right = Switch2StickCalibration.Nominal;

if (pair && !alreadyPaired)
{
    // Stores this PC as the controller's host so it reconnects here when a button is pressed.
    var mac = new byte[14];
    mac[1] = 0x02;
    WriteUInt48(mac.AsSpan(2), host);
    WriteUInt48(mac.AsSpan(8), host);
    await Command(0x15, 0x01, mac);
    await Command(0x15, 0x04, Convert.FromHexString("00EABD4713893542C679EE07F2532C6C31"));
    await Command(0x15, 0x02, Convert.FromHexString("0040B08A5FCD1F9B41125CACC63F38A073"));
    await Command(0x15, 0x03, [0]);
    Log("Paired: the controller now reconnects to this PC.");
}

// 4. Input reports.
int count = 0, lastCount = 0;
var lengths = new SortedDictionary<int, int>();
long first = 0, last = 0;
State previous = default;
uint previousRaw = 0;
double maxGapMs = 0;
chars[inputUuid].ValueChanged += (_, e) =>
{
    var data = e.CharacteristicValue.ToArray();
    long now = Stopwatch.GetTimestamp();
    if (count == 0) first = now; else maxGapMs = Math.Max(maxGapMs, Stopwatch.GetElapsedTime(last, now).TotalMilliseconds);
    last = now;
    count++;
    lock (lengths) lengths[data.Length] = lengths.GetValueOrDefault(data.Length) + 1;
    if (count <= 3) Log($"Report #{count} ({data.Length} bytes): {Hex(data)}");
    // Theory: the BLE report is USB report 0x05 without its report ID.
    var usb = new byte[64];
    usb[0] = 0x05;
    data.AsSpan(0, Math.Min(63, data.Length)).CopyTo(usb.AsSpan(1));
    uint rawButtons = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
    bool rawChanged = rawButtons != previousRaw;
    previousRaw = rawButtons;
    if (Switch2ReportParser.TryParse(usb, left, right, out var state) && (rawChanged || state.Gamepad.Buttons != previous.Gamepad.Buttons
        || Math.Abs(state.Gamepad.LeftThumbX - previous.Gamepad.LeftThumbX) > 8000 || Math.Abs(state.Gamepad.LeftThumbY - previous.Gamepad.LeftThumbY) > 8000
        || Math.Abs(state.Gamepad.RightThumbX - previous.Gamepad.RightThumbX) > 8000 || Math.Abs(state.Gamepad.RightThumbY - previous.Gamepad.RightThumbY) > 8000
        || state.Gamepad.LeftTrigger != previous.Gamepad.LeftTrigger || state.Gamepad.RightTrigger != previous.Gamepad.RightTrigger))
    {
        previous = state;
        bool motion = Switch2ReportParser.TryParseMotion(usb, out var m);
        Log($"  raw {Hex(data.AsSpan(4, 4))} buttons {state.Gamepad.Buttons} L({state.Gamepad.LeftThumbX},{state.Gamepad.LeftThumbY}) R({state.Gamepad.RightThumbX},{state.Gamepad.RightThumbY}) L2/R2 {state.Gamepad.LeftTrigger}/{state.Gamepad.RightTrigger}" +
            (motion ? $" gyro({m.GyroX},{m.GyroY},{m.GyroZ}) accel({m.AccelX},{m.AccelY},{m.AccelZ})" : " no motion"));
    }
};
Log($"Input notify: {await chars[inputUuid].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify)}");

try
{
    await Command(0x0C, 0x02, [features, 0, 0, 0]);
    await Command(0x0C, 0x04, [features, 0, 0, 0]);
    Log($"Features 0x{features:X2} enabled");
}
catch (Exception ex) { Log($"Feature enable failed: {ex.Message}"); }
try { await Command(0x09, 0x07, [0x01, 0, 0, 0]); Log("Player 1 LED set"); }
catch (Exception ex) { Log($"LED failed: {ex.Message}"); }

if (rumble)
{
    var vibration = chars[vibrationUuid];
    for (int i = 0; i < 20; i++)
    {
        var report = Switch2Rumble.BuildReport(i < 19 ? (byte)160 : (byte)0, i < 19 ? (byte)160 : (byte)0, i);
        report[0] = 0x00; // BLE: no report ID, same motor blocks at 1 and 0x11.
        await vibration.WriteValueWithResultAsync(report.AsSpan(0, 0x21).ToArray().AsBuffer(), GattWriteOption.WriteWithoutResponse);
        await Task.Delay(15);
    }
    Log("Rumble pulse sent (~0.3 s)");
}

Log($"Reading input for {seconds}s: press buttons, move both sticks, pull ZL/ZR, tilt the controller...");
for (int s = 0; s < seconds; s++)
{
    await Task.Delay(1000);
    int now = count;
    Log($"[{s + 1,2}s] {now - lastCount} reports/s, status {device.ConnectionStatus}");
    lastCount = now;
}
double span = count > 1 ? Stopwatch.GetElapsedTime(first, last).TotalSeconds : 0;
lock (lengths) Log($"Total {count} reports, average {(span > 0 ? (count - 1) / span : 0):F1}/s, max gap {maxGapMs:F0} ms, lengths {string.Join(", ", lengths.Select(kv => $"{kv.Key}B×{kv.Value}"))}");
return 0;

int ArgInt(string name, int fallback)
{
    int i = Array.IndexOf(args, name);
    if (i < 0 || i + 1 >= args.Length) return fallback;
    string v = args[i + 1];
    return v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(v[2..], 16) : int.Parse(v);
}
static void Log(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
static string Hex(ReadOnlySpan<byte> data) => Convert.ToHexString(data);
static string Mac(ulong value) => string.Join(":", Enumerable.Range(0, 6).Select(i => ((value >> (8 * (5 - i))) & 0xFF).ToString("X2")));
static ulong ReadUInt48(ReadOnlySpan<byte> data) { ulong v = 0; for (int i = 5; i >= 0; i--) v = (v << 8) | data[i]; return v; }
static void WriteUInt48(Span<byte> data, ulong value) { for (int i = 0; i < 6; i++) data[i] = (byte)(value >> (8 * i)); }
