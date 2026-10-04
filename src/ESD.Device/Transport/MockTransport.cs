using ESD.Core;
using ESD.Device.Protocol;

namespace ESD.Device.Transport;

/// <summary>
/// Software mock that simulates an ESD device over a virtual COM port.
/// Generates realistic ESD V1 protocol frames:
///  - Responds to PING / GET_STATUS / GET_DEVICE_INFO commands.
///  - Autonomously pushes unsolicited EVENT frames (worker install/remove).
/// </summary>
public sealed class MockTransport : ITransport
{
    private readonly string         _name;
    private readonly byte           _address;
    private readonly int            _pollIntervalMs;
    private readonly EsdProtocol    _proto = new();
    private readonly Random         _rng   = new();

    private CancellationTokenSource? _cts;
    private Task?                    _eventLoop;
    private int                      _empCounter;

    public string  Name        => _name;
    public bool    IsConnected => _cts is not null;

    public event EventHandler<byte[]>? DataReceived;

    public MockTransport(string name, byte address = 0x01, int pollIntervalMs = 1500)
    {
        _name           = name;
        _address        = address;
        _pollIntervalMs = pollIntervalMs;
    }

    public Task OpenAsync(CancellationToken ct)
    {
        _cts       = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _eventLoop = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Announce connection
        Push(EsdCommand.Event, [(byte)EsdEventType.DeviceConnected]);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_pollIntervalMs, ct).ConfigureAwait(false);
            _empCounter++;

            var empId  = (byte)(_empCounter % 5 + 1); // EMP001..EMP005
            var isInstall = _empCounter % 2 != 0;
            var evtType   = isInstall
                ? EsdEventType.WorkerDetected
                : EsdEventType.WorkerRemoved;

            var strapStatus = isInstall
                ? (byte)WristStrapStatus.Ok
                : (byte)WristStrapStatus.NotConnected;

            // DATA: [EventType(1)] [EmployeeId(1)] [StrapStatus(1)]
            Push(EsdCommand.Event, [(byte)evtType, empId, strapStatus]);
        }
    }

    /// <summary>Called when the host "sends" a command — we build a reply.</summary>
    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        Task.Run(() => HandleCommand(data), ct);
        return Task.CompletedTask;
    }

    private void HandleCommand(byte[] raw)
    {
        if (!_proto.TryParse(raw, out var frame, out _) || frame is null) return;

        switch (frame.Command)
        {
            case EsdCommand.Ping:
                Reply(frame, EsdCommand.Ack, []);
                break;

            case EsdCommand.GetStatus:
                // DATA: [StrapStatus(1)]
                Reply(frame, EsdCommand.Ack, [(byte)WristStrapStatus.Ok]);
                break;

            case EsdCommand.GetDeviceInfo:
                // DATA: "ESD-MOCK" as ASCII
                Reply(frame, EsdCommand.Ack,
                    System.Text.Encoding.ASCII.GetBytes("ESD-MOCK-V1"));
                break;

            default:
                Reply(frame, EsdCommand.Nack, [(byte)NackError.UnknownCommand]);
                break;
        }
    }

    private void Reply(EsdFrame req, EsdCommand cmd, byte[] data)
    {
        var frame = _proto.BuildFrame(_address, cmd, req.Sequence, data);
        DataReceived?.Invoke(this, frame);
    }

    private void Push(EsdCommand cmd, byte[] data)
    {
        var seq   = (byte)(_rng.Next(1, 255));
        var frame = _proto.BuildFrame(_address, cmd, seq, data);
        DataReceived?.Invoke(this, frame);
    }

    public Task CloseAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { CloseAsync(); return ValueTask.CompletedTask; }
}
