using System.IO.Ports;
using ESD.Core;

namespace ESD.Device.Transport;

/// <summary>
/// Raw serial (RS-232 / RS-485) transport.
/// Collects bytes from DataReceived and raises them in chunks — the
/// Protocol layer is responsible for framing; we never process here.
/// </summary>
public sealed class SerialTransport : ITransport
{
    private readonly string _portName;
    private readonly int    _baudRate;
    private readonly Parity _parity;
    private readonly int    _dataBits;
    private readonly StopBits _stopBits;
    private readonly int    _readTimeout;
    private readonly int    _writeTimeout;

    private SerialPort? _port;

    public string  Name        => _portName;
    public bool    IsConnected => _port?.IsOpen == true;

    public event EventHandler<byte[]>? DataReceived;

    public SerialTransport(
        string portName,
        int    baudRate      = 9600,
        string parity        = "None",
        int    dataBits      = 8,
        int    stopBits      = 1,
        int    readTimeoutMs  = 500,
        int    writeTimeoutMs = 500)
    {
        _portName     = portName;
        _baudRate     = baudRate;
        _parity       = Enum.TryParse<Parity>(parity, true, out var p) ? p : Parity.None;
        _dataBits     = dataBits;
        _stopBits     = stopBits switch { 2 => StopBits.Two, _ => StopBits.One };
        _readTimeout  = readTimeoutMs;
        _writeTimeout = writeTimeoutMs;
    }

    public Task OpenAsync(CancellationToken ct)
    {
        _port = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout  = _readTimeout,
            WriteTimeout = _writeTimeout,
        };
        _port.DataReceived += OnPortDataReceived;
        _port.Open();
        return Task.CompletedTask;
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            int count = _port!.BytesToRead;
            if (count <= 0) return;
            var buf = new byte[count];
            _port.Read(buf, 0, count);
            DataReceived?.Invoke(this, buf);
        }
        catch { /* swallow; upper layer logs */ }
    }

    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        _port?.Write(data, 0, data.Length);
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (_port is not null)
        {
            _port.DataReceived -= OnPortDataReceived;
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
            _port = null;
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { CloseAsync(); return ValueTask.CompletedTask; }
}
