using System.Runtime.InteropServices;
using System.Text;
using ESD.Core;

namespace ESD.Device.Transport;
/// <summary>
/// Simulates an Arduino ESD station over the exact wire protocol:
/// answers every command with a correctly HMAC-signed frame and
/// pushes unsolicited StatusReport / AlarmTrigger frames.
///
/// Used for end-to-end testing without hardware — the receive
/// path exercises the real parser (SOF sync, length, EOF, HMAC).
///
/// Auto scenario (every ~2 s):
///   worker attach → StatusReport(worker, ok)
///   ESD check pass
///   worker detach → StatusReport(none)
///   occasional wristband alarm → AlarmTrigger → AlarmClear
/// </summary>
public sealed class MockArduinoTransport : ITransport
{
    private readonly string _name;
    private readonly uint _deviceId;
    private readonly byte[] _hmacKey;
    private readonly int _pollIntervalMs;

    private readonly Random _rng = new();
    private readonly List<byte> _rxBuf = new();
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    // Simulated station state
    private bool _locked;
    private bool _alarm;
    private string? _workerId;
    private int _step;

    public string Name => _name;
    public bool IsConnected => _cts is not null;

    public event EventHandler<byte[]>? DataReceived;

    public MockArduinoTransport(
        string name,
        uint deviceId,
        ReadOnlySpan<byte> hmacKey,
        int pollIntervalMs = 2000)
    {
        _name           = name;
        _deviceId       = deviceId;
        _hmacKey        = hmacKey.ToArray();
        _pollIntervalMs = pollIntervalMs;
    }

    public Task OpenAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => RunAsync(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Announce connection
        Push(ArduinoCommand.StatusReport);

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(_pollIntervalMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            _step++;
            switch (_step % 6)
            {
                case 1: // worker attaches
                    _workerId = $"W-{_rng.Next(1000, 9999):D4}";
                    _alarm = false;
                    Push(ArduinoCommand.StatusReport);
                    break;

                case 2: // ESD check pass
                    break;

                case 3: // occasional alarm
                    if (_rng.NextDouble() < 0.35)
                    {
                        _alarm = true;
                        Push(ArduinoCommand.StatusReport);
                        Push(ArduinoCommand.AlarmTrigger,
                            new[] { (byte)AlarmReason.EsdWristbandFail });
                    }
                    break;

                case 4: // alarm clears
                    if (_alarm)
                    {
                        _alarm = false;
                        Push(ArduinoCommand.AlarmClear);
                        Push(ArduinoCommand.StatusReport);
                    }
                    break;

                case 5: // worker detaches
                    _workerId = null;
                    Push(ArduinoCommand.StatusReport);
                    break;
            }
        }
    }

    // ── Inbound frames (commands from the gateway) ───────────────

    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        lock (_lock)
        {
            _rxBuf.AddRange(data);
            ProcessBuffer();
        }
        return Task.CompletedTask;
    }

    private void ProcessBuffer()
    {
        while (_rxBuf.Count > 0)
        {
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_rxBuf);
            int sof = -1;
            for (int i = 0; i < span.Length - 1; i++)
            {
                if (span[i] == ArduinoProtocolConstants.Sof1
                    && span[i + 1] == ArduinoProtocolConstants.Sof2)
                { sof = i; break; }
            }
            if (sof < 0) { _rxBuf.Clear(); return; }
            if (sof > 0) _rxBuf.RemoveRange(0, sof);

            if (!ArduinoFrameCodec.TryReadFrameLength(
                    CollectionsMarshal.AsSpan(_rxBuf), out int total))
            {
                if (_rxBuf.Count > ArduinoProtocolConstants.HeaderSize)
                    _rxBuf.RemoveAt(0);
                return;
            }

            if (_rxBuf.Count < total) return;

            var raw = _rxBuf.Take(total).ToArray();
            _rxBuf.RemoveRange(0, total);

            if (!ArduinoFrameCodec.TryDecode(raw, _hmacKey, out var frame, out _))
                continue;

            HandleCommand(frame!);
        }
    }

    private void HandleCommand(ArduinoFrame frame)
    {
        switch (frame.Command)
        {
            case ArduinoCommand.Heartbeat:
                Ack(frame);
                break;

            case ArduinoCommand.StatusReport:
                // Respond with current state, then keep pushing it
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.WorkerScan:
                Ack(frame);
                break;

            case ArduinoCommand.Attach:
                _workerId = Encoding.UTF8.GetString(frame.Payload);
                _alarm = false;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.Detach:
                _workerId = null;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.EsdCheck:
                // payload[0] = 0 pass / 1 fail
                Ack(frame, new byte[] { _alarm ? (byte)1 : (byte)0 });
                break;

            case ArduinoCommand.GetInfo:
                Ack(frame, Encoding.UTF8.GetBytes($"ESD-ARDUINO-MOCK dev{_deviceId} fw1.0"));
                break;

            case ArduinoCommand.SetConfig:
                Ack(frame);
                break;

            case ArduinoCommand.AlarmTrigger:
                _alarm = true;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.AlarmClear:
                _alarm = false;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.LockStation:
                _locked = true;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.UnlockStation:
                _locked = false;
                Ack(frame);
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.LogUpload:
                Ack(frame, Encoding.UTF8.GetBytes(
                    $"log: dev{_deviceId} uptime={_step * 2}s"));
                break;

            case ArduinoCommand.Reboot:
                Ack(frame);
                _workerId = null;
                _alarm = false;
                _step = 0;
                Push(ArduinoCommand.StatusReport);
                break;

            case ArduinoCommand.FirmwareUpdate:
                Ack(frame);
                break;

            default:
                Nack(frame, ArduinoErrorCode.UnknownCommand);
                break;
        }
    }

    // ── Outbound frame builders ──────────────────────────────────────

    private uint _outSeq;

    private void Ack(ArduinoFrame request, byte[]? payload = null)
        => Respond(ArduinoCommand.Ack, request.Sequence, payload);

    private void Nack(ArduinoFrame request, ArduinoErrorCode error)
        => Respond(ArduinoCommand.Nack, request.Sequence, new[] { (byte)error });

    /// <summary>Push an unsolicited frame (StatusReport / Alarm …).</summary>
    private void Push(ArduinoCommand command, byte[]? payload = null)
        => Respond(command, ++_outSeq, BuildPushPayload(command, payload));

    private byte[]? BuildPushPayload(ArduinoCommand command, byte[]? payload)
    {
        if (command is ArduinoCommand.StatusReport)
        {
            var id = _workerId is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(_workerId);
            var buf = new byte[3 + id.Length];
            buf[0] = (byte)(_locked ? 1 : 0);
            buf[1] = (byte)(_alarm ? 1 : 0);
            buf[2] = (byte)id.Length;
            id.CopyTo(buf, 3);
            return buf;
        }
        return payload;
    }

    private void Respond(ArduinoCommand command, uint sequence, byte[]? payload)
    {
        var frame = new ArduinoFrame(
            ArduinoProtocolConstants.Version,
            ArduinoFrameFlags.None,
            _deviceId,
            command,
            sequence,
            (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            payload ?? Array.Empty<byte>(),
            Array.Empty<byte>());

        var raw = ArduinoFrameCodec.Encode(frame, _hmacKey);
        DataReceived?.Invoke(this, raw);
    }

    public Task CloseAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        CloseAsync();
        return ValueTask.CompletedTask;
    }
}
