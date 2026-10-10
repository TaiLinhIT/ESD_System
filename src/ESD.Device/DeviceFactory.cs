using ESD.Core;
using ESD.Device.Transport;

namespace ESD.Device;

/// <summary>
/// Creates <see cref="IEsdDevice"/> instances from configuration.
///
/// Protocol selection (DeviceSetting.ProtocolVersion):
///   "Mock"       — legacy ESD-V1 software mock (no hardware)
///   "ESD-V1"     — legacy CRC-16 protocol over RS-232/485
///   "Arduino"    — Arduino ESD protocol V2 (HMAC, real hardware)
///   "ArduinoMock"— Arduino protocol V2 simulated in software
///
/// Everything downstream of the transport/protocol pair is
/// identical — the device facade normalizes frames into
/// domain <see cref="EsdEvent"/>s either way.
/// </summary>
public static class DeviceFactory
{
    public const string ProtocolMock       = "Mock";
    public const string ProtocolEsdV1      = "ESD-V1";
    public const string ProtocolArduino    = "Arduino";
    public const string ProtocolArduinoMock = "ArduinoMock";

    public static IEsdDevice Create(DeviceConfig cfg)
    {
        if (cfg.ProtocolVersion.Equals(ProtocolArduino, StringComparison.OrdinalIgnoreCase)
            || cfg.ProtocolVersion.Equals(ProtocolArduinoMock, StringComparison.OrdinalIgnoreCase))
        {
            var useMock = cfg.ProtocolVersion.Equals(
                ProtocolArduinoMock, StringComparison.OrdinalIgnoreCase)
                || cfg.PortName.Equals("MOCK", StringComparison.OrdinalIgnoreCase);

            ITransport transport = useMock
                ? new MockArduinoTransport(cfg.Name, cfg.DeviceId, cfg.HmacKeyBytes)
                : new SerialTransport(
                    cfg.PortName,
                    cfg.BaudRate,
                    cfg.Parity,
                    cfg.DataBits,
                    cfg.StopBits,
                    cfg.ReadTimeoutMs,
                    cfg.WriteTimeoutMs);

            return new ArduinoDevice(
                cfg.Name,
                cfg.DeviceId,
                transport,
                cfg.HmacKeyBytes,
                retryCount:          cfg.RetryCount,
                commandTimeoutMs:    cfg.CommandTimeoutMs,
                heartbeatIntervalMs: cfg.HeartbeatIntervalMs);
        }

        // Legacy ESD-V1 / Mock path — unchanged
        ITransport legacyTransport = cfg.ProtocolVersion.Equals(
            ProtocolMock, StringComparison.OrdinalIgnoreCase)
            ? new MockTransport(cfg.Name, address: 0x01, pollIntervalMs: 1_500)
            : new SerialTransport(
                cfg.PortName,
                cfg.BaudRate,
                cfg.Parity,
                cfg.DataBits,
                cfg.StopBits,
                cfg.ReadTimeoutMs,
                cfg.WriteTimeoutMs);

        return new EsdDevice(
            cfg.Name,
            address:              0x01,
            transport:            legacyTransport,
            retryCount:           cfg.RetryCount,
            commandTimeoutMs:     cfg.ReadTimeoutMs,
            heartbeatIntervalMs:  cfg.HeartbeatIntervalMs);
    }
}
