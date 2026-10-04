using ESD.Core;
using ESD.Device.Transport;

namespace ESD.Device;

/// <summary>
/// Creates <see cref="EsdDevice"/> instances from configuration.
/// Choosing Mock vs Serial transport is the only decision here —
/// the rest of the stack is identical.
/// </summary>
public static class DeviceFactory
{
    public static EsdDevice Create(DeviceConfig cfg)
    {
        ITransport transport = cfg.ProtocolVersion.Equals("Mock", StringComparison.OrdinalIgnoreCase)
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
            transport:            transport,
            retryCount:           cfg.RetryCount,
            commandTimeoutMs:     cfg.ReadTimeoutMs,
            heartbeatIntervalMs:  cfg.HeartbeatIntervalMs);
    }
}
