namespace ESD.Core;

// ─────────────────────────────────────────────
// Device configuration (from appsettings.json)
// ─────────────────────────────────────────────
public sealed record DeviceConfig(
    string Name,
    string PortName,
    int BaudRate,
    int DataBits,
    string Parity,
    int StopBits,
    int ReadTimeoutMs,
    int WriteTimeoutMs,
    int RetryCount,
    int HeartbeatIntervalMs,
    string ProtocolVersion,
    uint DeviceId = 1,
    string HmacKey = "00112233445566778899AABBCCDDEEFF",
    int CommandTimeoutMs = 3000)
{
    /// <summary>Decoded HMAC key bytes for the Arduino protocol.</summary>
    public byte[] HmacKeyBytes =>
        ArduinoFrameCodec.FromHex(string.IsNullOrWhiteSpace(HmacKey)
            ? "00112233445566778899AABBCCDDEEFF"
            : HmacKey);
};

// ─────────────────────────────────────────────
// ESD Protocol V1 — Command codes
// ─────────────────────────────────────────────
public enum EsdCommand : byte
{
    Ping           = 0x01,
    GetDeviceInfo  = 0x02,
    GetStatus      = 0x03,
    GetVersion     = 0x04,
    GetConfig      = 0x05,

    Read           = 0x10,
    ReadAll        = 0x11,

    Write          = 0x20,
    WriteConfig    = 0x21,

    Start          = 0x30,
    Stop           = 0x31,
    Reset          = 0x32,

    Event          = 0x40,
    Alarm          = 0x41,

    Ack            = 0xF0,
    Nack           = 0xF1,
    Error          = 0xFF,
}

// ─────────────────────────────────────────────
// ESD Protocol V1 — Event type codes
// ─────────────────────────────────────────────
public enum EsdEventType : byte
{
    DeviceConnected        = 0x01,
    DeviceDisconnected     = 0x02,

    WorkerDetected         = 0x10,
    WorkerRemoved          = 0x11,

    WristStrapConnected    = 0x20,
    WristStrapDisconnected = 0x21,

    EsdTestStarted         = 0x30,
    EsdTestPass            = 0x31,
    EsdTestFail            = 0x32,

    Alarm                  = 0x40,
    AlarmReset             = 0x41,
}

// ─────────────────────────────────────────────
// ESD Protocol V1 — Wrist strap status codes
// ─────────────────────────────────────────────
public enum WristStrapStatus : byte
{
    NotConnected = 0x00,
    Ok           = 0x01,
    Ng           = 0x02,
    Warning      = 0x03,
    Error        = 0x04,
}

// ─────────────────────────────────────────────
// ESD Protocol V1 — NACK error codes
// ─────────────────────────────────────────────
public enum NackError : byte
{
    UnknownCommand    = 0x01,
    InvalidLength     = 0x02,
    CrcError          = 0x03,
    InvalidParameter  = 0x04,
    DeviceBusy        = 0x05,
    NotReady          = 0x06,
    HardwareError     = 0x07,
    PermissionError   = 0x08,
    Timeout           = 0x09,
    InvalidState      = 0x0A,
}

// ─────────────────────────────────────────────
// ESD Protocol V1 — Parsed frame
// ─────────────────────────────────────────────
/// <summary>
/// Decoded ESD V1 frame: STX|VER|LEN|ADDR|CMD|SEQ|DATA|CRC16|ETX
/// </summary>
public sealed record EsdFrame(
    byte Version,
    byte Address,
    EsdCommand Command,
    byte Sequence,
    byte[] Data);

// ─────────────────────────────────────────────
// Domain events flowing up to Service / UI
// ─────────────────────────────────────────────
public sealed record EsdEvent(
    DateTime   Timestamp,
    string     DeviceName,
    byte       DeviceAddress,
    EsdEventType EventType,
    string     EmployeeId,
    WristStrapStatus StrapStatus,
    string     RawHex,
    string     Message);

public sealed record DbEvent(
    DateTime Timestamp,
    string   Device,
    string   EmployeeId,
    string   EventType,
    string   Status,
    string   RawData,
    string   Message);

// ─────────────────────────────────────────────
// Command result returned to callers
// ─────────────────────────────────────────────
public enum CommandResultStatus { Ok, Nack, Timeout, Error }

public sealed record CommandResult(
    CommandResultStatus Status,
    EsdFrame?           Frame,
    NackError?          NackCode   = null,
    string              Message    = "");
