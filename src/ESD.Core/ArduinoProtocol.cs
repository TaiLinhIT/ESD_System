using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ESD.Core;

// ═══════════════════════════════════════════════════════════════
//  ESD Arduino Protocol V2
//  Wire format (big-endian, HMAC-SHA256 authenticated):
//
//  SOF(2) VER(1) FLAGS(1) DEVICE_ID(4) CMD(2) SEQ(4) TS(4) LEN(2) PAYLOAD(0..256) HMAC16 EOF(2)
//
//  Total frame length = 38 + PayloadLength.
//  HMAC = HMAC-SHA256(key, frame[2 .. end-16]) truncated to the first 16 bytes.
// ═══════════════════════════════════════════════════════════════

/// <summary>Command codes carried in the 2-byte command field.</summary>
public enum ArduinoCommand : ushort
{
    Heartbeat       = 0x0001,
    StatusReport    = 0x0002,
    WorkerScan      = 0x0010,
    Attach          = 0x0011,
    Detach          = 0x0012,
    EsdCheck        = 0x0013,
    GetInfo         = 0x0020,
    SetConfig       = 0x0021,
    AlarmTrigger    = 0x0030,
    AlarmClear      = 0x0031,
    LockStation     = 0x0032,
    UnlockStation   = 0x0033,
    LogUpload       = 0x0040,
    FirmwareUpdate  = 0x0050,
    Reboot          = 0x0051,
    Ack             = 0x00F0,
    Nack            = 0x00F1,
    Error           = 0x00FF,
}

/// <summary>Frame flag bits.</summary>
[Flags]
public enum ArduinoFrameFlags : byte
{
    None         = 0,
    AckRequested = 1,
    Encrypted    = 2,
    Error        = 4,
}

/// <summary>Error codes returned in Nack payload[0].</summary>
public enum ArduinoErrorCode : byte
{
    Ok                 = 0,
    BadVersion         = 1,
    BadLength          = 2,
    BadCrc             = 3,
    BadMac             = 4,
    Replay             = 5,
    UnknownCommand     = 6,
    DeviceNotAuthorized = 7,
    Busy               = 8,
    InvalidPayload     = 9,
}

/// <summary>Alarm reason codes carried in AlarmTrigger payload[0].</summary>
public enum AlarmReason : byte
{
    EsdWristbandFail   = 0x01,
    EsdFootwearFail    = 0x02,
    UnauthorizedWorker = 0x03,
    StationTimeout     = 0x04,
    ManualTrigger      = 0xFF,
}

/// <summary>A fully decoded Arduino protocol frame.</summary>
public sealed record ArduinoFrame(
    byte            Version,
    ArduinoFrameFlags Flags,
    uint            DeviceId,
    ArduinoCommand  Command,
    uint            Sequence,
    uint            Timestamp,
    byte[]          Payload,
    byte[]          AuthTag);

/// <summary>Result of a command/response exchange with an Arduino device.</summary>
public enum ArduinoCommandStatus { Ok, Nack, Timeout, Error }

public sealed record ArduinoCommandResult(
    ArduinoCommandStatus Status,
    ArduinoFrame?        Frame     = null,
    ArduinoErrorCode?    ErrorCode = null,
    string               Message   = "");

public static class ArduinoProtocolConstants
{
    public const byte  Sof1        = 0xAA;
    public const byte  Sof2        = 0x55;
    public const byte  Eof1        = 0x0D;
    public const byte  Eof2        = 0x0A;
    public const byte  Version     = 1;
    public const int   AuthTagSize = 16;
    public const int   MaxPayload  = 256;

    /// <summary>Fixed header size: SOF(2)+VER(1)+FLAGS(1)+DEV(4)+CMD(2)+SEQ(4)+TS(4)+LEN(2).</summary>
    public const int   HeaderSize  = 20;

    /// <summary>Minimum frame size with empty payload: header + tag(16) + eof(2).</summary>
    public const int   MinFrameSize = 38;
}

/// <summary>
/// Pure codec for the Arduino protocol: encode/decode frames with
/// HMAC-SHA256 authentication. No I/O — reusable from any layer
/// (device, gateway, tests, tools).
/// </summary>
public static class ArduinoFrameCodec
{
    /// <summary>Encode a frame. The AuthTag is computed over the frame body.</summary>
    public static byte[] Encode(ArduinoFrame frame, ReadOnlySpan<byte> key)
    {
        if (frame.Payload.Length > ArduinoProtocolConstants.MaxPayload)
            throw new ArgumentOutOfRangeException(nameof(frame),
                $"Payload exceeds {ArduinoProtocolConstants.MaxPayload} bytes.");

        int total = ArduinoProtocolConstants.MinFrameSize + frame.Payload.Length;
        var buffer = new byte[total];
        int p = 0;

        buffer[p++] = ArduinoProtocolConstants.Sof1;
        buffer[p++] = ArduinoProtocolConstants.Sof2;
        buffer[p++] = frame.Version;
        buffer[p++] = (byte)frame.Flags;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(p, 4), frame.DeviceId);   p += 4;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(p, 2), (ushort)frame.Command); p += 2;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(p, 4), frame.Sequence);   p += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(p, 4), frame.Timestamp);  p += 4;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(p, 2), (ushort)frame.Payload.Length); p += 2;

        frame.Payload.CopyTo(buffer, p); p += frame.Payload.Length;

        // HMAC-SHA256 over frame[2 .. p) — everything between SOF and the tag
        var tag = ComputeTag(buffer, 2, p - 2, key);
        tag.CopyTo(buffer, p); p += ArduinoProtocolConstants.AuthTagSize;

        buffer[p++] = ArduinoProtocolConstants.Eof1;
        buffer[p]   = ArduinoProtocolConstants.Eof2;

        return buffer;
    }

    /// <summary>
    /// Try to decode one complete frame from <paramref name="data"/>.
    /// The buffer must contain exactly one full frame (use
    /// <see cref="TryReadFrameLength"/> to locate frame boundaries first).
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key,
        out ArduinoFrame? frame, out ArduinoErrorCode error)
    {
        frame = null;
        error = ArduinoErrorCode.Ok;

        int min = ArduinoProtocolConstants.MinFrameSize;
        if (data.Length < min
            || data[0] != ArduinoProtocolConstants.Sof1
            || data[1] != ArduinoProtocolConstants.Sof2
            || data[^2] != ArduinoProtocolConstants.Eof1
            || data[^1] != ArduinoProtocolConstants.Eof2)
        {
            error = ArduinoErrorCode.BadLength;
            return false;
        }

        int p = 2;
        byte version = data[p++];
        if (version != ArduinoProtocolConstants.Version)
        {
            error = ArduinoErrorCode.BadVersion;
            return false;
        }

        var flags = (ArduinoFrameFlags)data[p++];
        uint deviceId  = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(p, 4)); p += 4;
        var command    = (ArduinoCommand)BinaryPrimitives.ReadUInt16BigEndian(data.Slice(p, 2)); p += 2;
        uint sequence  = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(p, 4)); p += 4;
        uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(p, 4)); p += 4;
        ushort payloadLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(p, 2)); p += 2;

        if (payloadLen > ArduinoProtocolConstants.MaxPayload || data.Length != min + payloadLen)
        {
            error = ArduinoErrorCode.BadLength;
            return false;
        }

        var payload = data.Slice(p, payloadLen).ToArray(); p += payloadLen;
        var tag     = data.Slice(p, ArduinoProtocolConstants.AuthTagSize).ToArray();

        var expected = ComputeTag(data, 2, p - 2, key);
        if (!CryptographicOperations.FixedTimeEquals(tag, expected))
        {
            error = ArduinoErrorCode.BadMac;
            return false;
        }

        frame = new ArduinoFrame(version, flags, deviceId, command,
            sequence, timestamp, payload, tag);
        return true;
    }

    /// <summary>
    /// Given a buffer positioned at SOF, read the payload length field
    /// and return the total frame size (38 + payloadLen).
    /// Returns false when fewer than 20 header bytes are available
    /// or the length field is out of range.
    /// </summary>
    public static bool TryReadFrameLength(ReadOnlySpan<byte> data, out int totalLength)
    {
        totalLength = 0;
        if (data.Length < ArduinoProtocolConstants.HeaderSize) return false;

        ushort payloadLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(18, 2));
        if (payloadLen > ArduinoProtocolConstants.MaxPayload) return false;

        totalLength = ArduinoProtocolConstants.MinFrameSize + payloadLen;
        return true;
    }

    /// <summary>Compute the 16-byte truncated HMAC-SHA256 tag.</summary>
    public static byte[] ComputeTag(ReadOnlySpan<byte> frameBody, int offset, int count, ReadOnlySpan<byte> key)
    {
        var body = frameBody.Slice(offset, count).ToArray();
        using var hmac = new HMACSHA256(key.ToArray());
        return hmac.ComputeHash(body)[..ArduinoProtocolConstants.AuthTagSize];
    }

    public static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);

    public static byte[] FromHex(string hex) =>
        Convert.FromHexString(new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray()));
}
