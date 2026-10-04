using ESD.Core;
using ESD.Device.Communication;
using ESD.Device.Protocol;

namespace ESD.Device;

/// <summary>
/// High-level ESD device facade.
/// Owns transport + command manager + heartbeat; exposes a clean async API.
/// </summary>
public sealed class EsdDevice : IEsdDevice
{
    private readonly ITransport       _transport;
    private readonly IProtocol        _protocol;
    private readonly CommandManager   _cmdMgr;
    private readonly HeartbeatManager _heartbeat;
    private readonly int              _retryCount;
    private readonly TimeSpan         _defaultTimeout;

    private byte _seq;

    public string Name    { get; }
    public byte   Address { get; }
    public bool   IsConnected => _transport.IsConnected && _heartbeat.IsOnline;

    public event EventHandler<EsdFrame>? EventReceived;

    public EsdDevice(
        string     name,
        byte       address,
        ITransport transport,
        int        retryCount         = 3,
        int        commandTimeoutMs   = 1_000,
        int        heartbeatIntervalMs = 5_000)
    {
        Name            = name;
        Address         = address;
        _transport      = transport;
        _protocol       = new EsdProtocol();
        _retryCount     = retryCount;
        _defaultTimeout = TimeSpan.FromMilliseconds(commandTimeoutMs);

        _cmdMgr    = new CommandManager(_transport, _protocol, retryCount);
        _heartbeat = new HeartbeatManager(_cmdMgr, address, heartbeatIntervalMs);

        _cmdMgr.EventReceived += (s, f) => EventReceived?.Invoke(this, f);
    }

    public async Task OpenAsync(CancellationToken ct)
    {
        await _transport.OpenAsync(ct).ConfigureAwait(false);
        _heartbeat.Start(ct);
    }

    public async Task CloseAsync()
    {
        _heartbeat.Stop();
        await _transport.CloseAsync().ConfigureAwait(false);
    }

    public Task<CommandResult> SendCommandAsync(
        EsdCommand command,
        byte[]?    data    = null,
        TimeSpan?  timeout = null,
        CancellationToken ct = default)
    {
        var seq = _seq++;
        return _cmdMgr.SendAsync(
            Address, command, seq, data,
            timeout ?? _defaultTimeout,
            _retryCount, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _cmdMgr.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
