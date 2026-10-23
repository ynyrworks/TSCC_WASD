using System.Buffers.Binary;

namespace TSCC_WASD.Core.Services.Input;

/// <summary>
/// NS2 Pro over Bluetooth LE: advertisement, GATT command framing and report conversion.
/// Read-only apart from volatile settings (player LED, sensor streaming); nothing is paired or
/// written to the controller's memory, so it keeps its Switch 2 pairing.
/// </summary>
/// <remarks>
/// GATT UUIDs, command framing and memory addresses follow the community reverse engineering in
/// Nadeflore/switch2-controllers and CareyScott/switch2controllerpc (MIT); this implementation is
/// our own. Verified on a Pro Controller 2 (057E:2069) on 2026-10-23.
/// </remarks>
public static class Switch2BleProtocol
{
    /// <summary>Bluetooth SIG company ID in Nintendo's advertisements.</summary>
    public const ushort NintendoCompanyId = 0x0553;
    public const ushort NintendoVendor = 0x057E, ProController2 = 0x2069;

    public static readonly Guid InputReport = Guid.Parse("ab7de9be-89fe-49ad-828f-118f09df7fd2");
    public static readonly Guid CommandWrite = Guid.Parse("649d4ac9-8eb7-4e6c-af44-1ea54fe5f005");
    public static readonly Guid CommandResponse = Guid.Parse("c765a961-d9d8-4d36-a20a-5315b111836a");
    public static readonly Guid Vibration = Guid.Parse("cc483f51-9258-427d-a939-630c31f72b05");

    /// <summary>Stick calibration: the user's own (when set) and the factory values, 11 bytes each.</summary>
    public const uint LeftUserCalibration = 0x1FC042, LeftFactoryCalibration = 0x130A8;
    public const uint RightUserCalibration = 0x1FC062, RightFactoryCalibration = 0x130E8;

    /// <summary>Feature flags enabled after connecting; 0x27 includes the IMU, as over USB.</summary>
    public const byte Features = 0x27;

    /// <summary>
    /// Reads Nintendo manufacturer data (company ID already removed): vendor at 3, product at 5,
    /// and at 10 the 48-bit address of the host it will reconnect to (zero while Sync is held).
    /// </summary>
    public static bool TryParseAdvertisement(ReadOnlySpan<byte> data, out ushort vendorId, out ushort productId, out ulong host)
    {
        vendorId = productId = 0;
        host = 0;
        if (data.Length < 16) return false;
        vendorId = BinaryPrimitives.ReadUInt16LittleEndian(data[3..]);
        productId = BinaryPrimitives.ReadUInt16LittleEndian(data[5..]);
        for (int i = 15; i >= 10; i--) host = (host << 8) | data[i];
        return true;
    }

    /// <summary>Header: command, 0x91, transport 0x01 (BLE; USB uses 0x00), subcommand, 0, length, 0, 0.</summary>
    public static byte[] BuildCommand(byte command, byte subcommand, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[8 + payload.Length];
        buffer[0] = command;
        buffer[1] = 0x91;
        buffer[2] = 0x01;
        buffer[3] = subcommand;
        buffer[5] = (byte)payload.Length;
        payload.CopyTo(buffer.AsSpan(8));
        return buffer;
    }

    /// <summary>Replies echo the command and carry 0x01 in byte 1; the payload starts at 8.</summary>
    public static bool TryParseReply(ReadOnlySpan<byte> reply, byte command, out ReadOnlySpan<byte> payload)
    {
        payload = default;
        if (reply.Length < 8 || reply[0] != command || reply[1] != 0x01) return false;
        payload = reply[8..];
        return true;
    }

    public static byte[] BuildReadMemory(byte length, uint address)
    {
        Span<byte> payload = stackalloc byte[8];
        payload.Clear();
        payload[0] = length;
        payload[1] = 0x7E;
        BinaryPrimitives.WriteUInt32LittleEndian(payload[4..], address);
        return BuildCommand(0x02, 0x04, payload);
    }

    /// <summary>The read reply repeats the length and address before the data.</summary>
    public static bool TryParseMemory(ReadOnlySpan<byte> payload, byte length, uint address, out ReadOnlySpan<byte> data)
    {
        data = default;
        if (payload.Length < 8 + length || payload[0] != length || BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]) != address)
            return false;
        data = payload.Slice(8, length);
        return true;
    }

    /// <summary>Unset user calibration reads as FF FF FF.</summary>
    public static bool IsUnset(ReadOnlySpan<byte> calibration) =>
        calibration.Length < 3 || (calibration[0] == 0xFF && calibration[1] == 0xFF && calibration[2] == 0xFF);

    public static byte[] FeatureInit(byte features) => BuildCommand(0x0C, 0x02, [features, 0, 0, 0]);
    public static byte[] FeatureEnable(byte features) => BuildCommand(0x0C, 0x04, [features, 0, 0, 0]);
    public static byte[] PlayerLed(byte pattern) => BuildCommand(0x09, 0x07, [pattern, 0, 0, 0]);

    /// <summary>
    /// The 63-byte BLE input report is USB report 0x05 without its report ID, so the USB parser
    /// reads it once the ID is put back.
    /// </summary>
    public static bool TryConvertReport(ReadOnlySpan<byte> notification, Span<byte> usbReport)
    {
        if (notification.Length < 63 || usbReport.Length != 64) return false;
        usbReport[0] = 0x05;
        notification[..63].CopyTo(usbReport[1..]);
        return true;
    }

    /// <summary>Same motor blocks as the USB rumble report, written without a report ID.</summary>
    public static byte[] BuildRumble(byte large, byte small, int sequence)
    {
        var report = Switch2Rumble.BuildReport(large, small, sequence);
        report[0] = 0x00;
        return report[..0x21];
    }
}
