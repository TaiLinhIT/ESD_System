using ESD.Core;
using ESD.Device;

namespace ESD.Service;

public sealed class DeviceManager : IDeviceManager
{
    private readonly List<IDevice> _devices = [];
    private readonly AppSettings _settings;
    private readonly EventManager _events;
    private readonly FileLogger _log;
    private readonly EsdStateMachine _stateMachine = new();

    public IReadOnlyCollection<IDevice> Devices => _devices;

    public DeviceManager(AppSettings settings, EventManager events, FileLogger log)
    {
        _settings = settings;
        _events = events;
        _log = log;

        foreach (var d in settings.Devices)
        {
            var cfg = new DeviceConfig(
                d.Name, d.PortName, d.BaudRate, d.DataBits, d.Parity, d.StopBits,
                Enum.TryParse<DeviceTransport>(d.Transport, true, out var t) ? t : DeviceTransport.Mock,
                Enum.TryParse<DeviceProtocol>(d.Protocol, true, out var p) ? p : DeviceProtocol.Raw,
                d.SlaveId, d.PollIntervalMs);

            IDevice device = cfg.Transport == DeviceTransport.Serial
                ? new SerialDevice(cfg)
                : new MockDevice(cfg);

            device.DataReceived += OnDataReceived;
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
                _log.Info($"Device opened: {d.Name}");
            }
            catch (Exception ex)
            {
                _log.Error($"Device open failed: {d.Name} - {ex.Message}");
            }
        }
    }

    private void OnDataReceived(object? sender, DeviceMessage msg)
    {
        var state = _stateMachine.Process(msg);
        var text = msg.RawText.Trim();
        var employee = "";
        var type = "DEVICE_DATA";

        if (text.StartsWith("REMOVE:", StringComparison.OrdinalIgnoreCase))
        {
            type = "ESD_REMOVE";
            employee = text[7..].Trim();
        }
        else if (text.StartsWith("INSTALL:", StringComparison.OrdinalIgnoreCase))
        {
            type = "ESD_INSTALL";
            employee = text[8..].Trim();
        }
        else if (text.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            type = "DEVICE_CONNECTED";
        }

        _log.Info($"{msg.Device}: RX {Convert.ToHexString(msg.Data)} | {text}");

        _events.Publish(new EsdEvent(
            msg.Timestamp, msg.Device, employee, type, "OK",
            Convert.ToHexString(msg.Data), $"State={state}"));
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
