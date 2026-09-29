using System.IO.Ports;
using ESD.Core;

namespace ESD.Device;

public sealed class SerialDevice : IDevice
{
    private readonly DeviceConfig _cfg;
    private SerialPort? _port;

    public string Name => _cfg.Name;
    public bool IsConnected => _port?.IsOpen == true;
    public event EventHandler<DeviceMessage>? DataReceived;

    public SerialDevice(DeviceConfig cfg) => _cfg = cfg;

    public Task OpenAsync(CancellationToken ct)
    {
        _port = new SerialPort(_cfg.PortName, _cfg.BaudRate,
            Enum.TryParse<Parity>(_cfg.Parity, true, out var p) ? p : Parity.None,
            _cfg.DataBits,
            Enum.TryParse<StopBits>(_cfg.StopBits.ToString(), true, out var s) ? s : StopBits.One);

        _port.DataReceived += OnDataReceived;
        _port.Open();
        return Task.CompletedTask;
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var bytes = new byte[_port!.BytesToRead];
            _port.Read(bytes, 0, bytes.Length);
            if (bytes.Length == 0) return;

            var text = System.Text.Encoding.ASCII.GetString(bytes);
            DataReceived?.Invoke(this, new DeviceMessage(DateTime.Now, Name, bytes, text));
        }
        catch { /* Service logger handles device errors */ }
    }

    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        _port?.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
            _port = null;
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { CloseAsync(); return ValueTask.CompletedTask; }
}
