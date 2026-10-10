using ESD.Core;
using ESD.Device;

namespace ESD.Service;

/// <summary>
/// Owns all <see cref="IEsdDevice"/> instances.
/// Translates raw <see cref="EsdFrame"/> events into domain <see cref="EsdEvent"/>
/// and publishes them through <see cref="EventManager"/>.
/// </summary>
public sealed class DeviceManager : IDeviceManager
{
    private readonly List<IEsdDevice>  _devices = [];
    private readonly EventManager      _events;
    private readonly FileLogger        _log;

    public IReadOnlyCollection<IEsdDevice> Devices => _devices;

    public DeviceManager(AppSettings settings, EventManager events, FileLogger log)
    {
        _events = events;
        _log    = log;

        foreach (var s in settings.Devices)
        {
            var cfg = new DeviceConfig(
                Name:                s.Name,
                PortName:            s.PortName,
                BaudRate:            s.BaudRate,
                DataBits:            s.DataBits,
                Parity:              s.Parity,
                StopBits:            s.StopBits,
                ReadTimeoutMs:       s.ReadTimeoutMs,
                WriteTimeoutMs:      s.WriteTimeoutMs,
                RetryCount:          s.RetryCount,
                HeartbeatIntervalMs: s.HeartbeatIntervalMs,
                ProtocolVersion:     s.ProtocolVersion,
                DeviceId:            s.DeviceId,
                HmacKey:             s.HmacKey,
                CommandTimeoutMs:    s.CommandTimeoutMs);

            var device = DeviceFactory.Create(cfg);

            if (device is ArduinoDevice arduino)
                arduino.DomainEventReceived += (_, e) => _events.Publish(e);
            else
                device.EventReceived += OnDeviceEvent;

            _devices.Add(device);
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var d in _devices)
        {
            try
            {
                await d.OpenAsync(ct);
                _log.Info($"[DEVICE] Opened: {d.Name} (addr=0x{d.Address:X2})");
            }
            catch (Exception ex)
            {
                _log.Error($"[DEVICE] Failed to open {d.Name}: {ex.Message}");
            }
        }
    }

    private void OnDeviceEvent(object? sender, EsdFrame frame)
    {
        var deviceName = (sender as IEsdDevice)?.Name ?? "unknown";

        // Decode event payload based on frame command
        EsdEventType evtType;
        byte[] rawPayload = frame.Data;

        if (frame.Command == EsdCommand.Alarm)
        {
            // Alarm payload is ASCII string, not [EventType][EmployeeId][StrapStatus]
            // Just use the first byte as a type indicator, but mark it as Alarm
            evtType = EsdEventType.AlarmReset; // fallback
            rawPayload = frame.Data;
        }
        else if (frame.Data.Length > 0)
        {
            evtType = (EsdEventType)frame.Data[0];
        }
        else
        {
            evtType = EsdEventType.DeviceConnected;
        }

        var empId = rawPayload.Length > 1
            ? $"EMP{frame.Data[1]:0000}"
            : string.Empty;

        var strapStatus = rawPayload.Length > 2
            ? (WristStrapStatus)frame.Data[2]
            : WristStrapStatus.NotConnected;

        var rawHex = Convert.ToHexString(frame.Data);

        _log.Info(
            $"[EVENT] {deviceName} | CMD=0x{(byte)frame.Command:X2} " +
            $"| Type={evtType} | Emp={empId} | Strap={strapStatus} | HEX={rawHex}");

        var domainEvent = new EsdEvent(
            Timestamp:    DateTime.Now,
            DeviceName:   deviceName,
            DeviceAddress: frame.Address,
            EventType:    evtType,
            EmployeeId:   empId,
            StrapStatus:  strapStatus,
            RawHex:       rawHex,
            Message:      $"SEQ={frame.Sequence}");

        _events.Publish(domainEvent);
    }

    public async Task StopAsync()
    {
        foreach (var d in _devices)
        {
            await d.CloseAsync();
            await d.DisposeAsync();
        }
        _devices.Clear();
    }
}
