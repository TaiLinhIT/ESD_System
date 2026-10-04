using System.IO.Ports;
using ESD.Core;

namespace ESD.Simulator;

/// <summary>
/// Simulates a physical ESD wrist-strap device connected via serial port.
/// Sends valid ESD V1 protocol frames — identical to what real hardware would send.
///
/// Flow:
///   ESD App (COM10) ←[virtual cable]→ HardwareSimulator (COM11)
/// </summary>
internal sealed class HardwareSimulator : IDisposable
{
    private readonly SerialPort _port;
    private readonly byte       _address;
    private readonly List<byte> _rxBuf = new(256);
    private readonly object     _lock  = new();

    private byte _seq;

    public string PortName => _port.PortName;
    public bool   IsOpen   => _port.IsOpen;

    public HardwareSimulator(string portName, int baudRate = 9600, byte address = 0x01)
    {
        _address = address;
        _port    = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout  = 500,
            WriteTimeout = 500,
        };
    }

    public void Open()
    {
        _port.DataReceived += OnDataReceived;
        _port.Open();
    }

    // ── Receive path — parse commands from ESD app ───────────────────────────
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var buf = new byte[_port.BytesToRead];
            _port.Read(buf, 0, buf.Length);
            lock (_lock)
            {
                _rxBuf.AddRange(buf);
                ProcessBuffer();
            }
        }
        catch { }
    }

    private void ProcessBuffer()
    {
        while (_rxBuf.Count > 0)
        {
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rxBuf);
            if (!SimProtocol.TryParse(span, out var frame, out int consumed))
            {
                if (consumed > 0) _rxBuf.RemoveRange(0, consumed);
                break;
            }
            _rxBuf.RemoveRange(0, consumed);
            if (frame is not null) HandleCommand(frame);
        }
    }

    private void HandleCommand(ParsedFrame frame)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ← [RX CMD] {frame.Command,-16} SEQ={frame.Sequence:X2} " +
                          $"DATA={Convert.ToHexString(frame.Data)}");
        Console.ResetColor();

        switch (frame.Command)
        {
            case EsdCommand.Ping:
                SendFrame(EsdCommand.Ack, frame.Sequence, []);
                break;

            case EsdCommand.GetStatus:
                SendFrame(EsdCommand.Ack, frame.Sequence,
                    [(byte)WristStrapStatus.Ok]);
                break;

            case EsdCommand.GetDeviceInfo:
                SendFrame(EsdCommand.Ack, frame.Sequence,
                    System.Text.Encoding.ASCII.GetBytes("ESD-HW-SIM-V1"));
                break;

            case EsdCommand.GetVersion:
                SendFrame(EsdCommand.Ack, frame.Sequence,
                    System.Text.Encoding.ASCII.GetBytes("1.0.0"));
                break;

            default:
                SendFrame(EsdCommand.Nack, frame.Sequence,
                    [(byte)NackError.UnknownCommand]);
                break;
        }
    }

    // ── Send helpers ─────────────────────────────────────────────────────────
    private void SendFrame(EsdCommand cmd, byte seq, byte[] data)
    {
        var frame = SimProtocol.BuildFrame(_address, cmd, seq, data);
        LogTx(cmd, seq, data);
        _port.Write(frame, 0, frame.Length);
    }

    /// <summary>Push an unsolicited event (no SEQ from host).</summary>
    public void PushEvent(EsdEventType evtType, byte employeeId, WristStrapStatus strapStatus)
    {
        byte[] data = [(byte)evtType, employeeId, (byte)strapStatus];
        var frame = SimProtocol.BuildFrame(_address, EsdCommand.Event, NextSeq(), data);
        LogTx(EsdCommand.Event, frame[6], data);
        _port.Write(frame, 0, frame.Length);
    }

    public void PushAlarm(string message)
    {
        var data = System.Text.Encoding.ASCII.GetBytes(message[..Math.Min(message.Length, 32)]);
        var frame = SimProtocol.BuildFrame(_address, EsdCommand.Alarm, NextSeq(), data);
        LogTx(EsdCommand.Alarm, frame[6], data);
        _port.Write(frame, 0, frame.Length);
    }

    private byte NextSeq() => _seq++;

    private static void LogTx(EsdCommand cmd, byte seq, byte[] data)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  → [TX EVT] {cmd,-16} SEQ={seq:X2} " +
                          $"DATA={Convert.ToHexString(data)}");
        Console.ResetColor();
    }

    public void Dispose()
    {
        _port.DataReceived -= OnDataReceived;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
    }
}
