using System.IO.Ports;
using ESD.Core;

namespace ESD.Device.Transport;

/// <summary>
/// Raw serial (RS-232 / RS-485) transport.
/// Collects bytes from DataReceived and raises them in chunks — the
/// Protocol layer is responsible for framing; we never process here.
///
/// USB-Serial notes (CH340/CP210x on Arduino Uno/Nano):
///   - Read/Write timeouts of 0 (infinite) avoid the
///     "semaphore timeout period has expired" failure seen with
///     short timeouts on USB-Serial adapters.
///   - Handshaking and DTR/RTS stay off — required by many
///     Arduino clones that reset on DTR toggle.
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
        int    readTimeoutMs  = 0,
        int    writeTimeoutMs = 0)
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
            // 0 = Infinite — USB-Serial friendly (CH340/CP210x)
            ReadTimeout  = _readTimeout == 0 ? SerialPort.InfiniteTimeout : _readTimeout,
            WriteTimeout = _writeTimeout == 0 ? SerialPort.InfiniteTimeout : _writeTimeout,

            // No handshaking, no DTR/RTS toggling (avoids Arduino resets
            // and semaphore timeouts on cheap USB-Serial adapters)
            Handshake   = Handshake.None,
            DtrEnable   = false,
            RtsEnable   = false,
            DiscardNull = false,
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
            int read = _port.Read(buf, 0, count);
            if (read > 0)
                DataReceived?.Invoke(this, buf[..read]);
        }
        catch (TimeoutException) { /* infinite-timeout sentinel */ }
        catch { /* swallow; upper layer logs */ }
    }

    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        if (_port?.IsOpen == true)
            _port.Write(data, 0, data.Length);
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
