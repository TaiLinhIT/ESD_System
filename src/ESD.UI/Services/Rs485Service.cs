using System.IO.Ports;

namespace ESD.UI.Services;

public class Rs485Service
{
    private SerialPort? _port;
    public bool IsConnected => _port?.IsOpen == true;

    public void Connect(string portName, int baudRate)
    {
        if (IsConnected) return;
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _port.Open();
    }

    public void Disconnect()
    {
        if (_port == null) return;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _port = null;
    }

    public byte[] Read(byte[] request)
    {
        if (!IsConnected) return Array.Empty<byte>();
        _port!.DiscardInBuffer();
        _port.Write(request, 0, request.Length);
        return Array.Empty<byte>(); // Hardware read implementation goes here.
    }
}
