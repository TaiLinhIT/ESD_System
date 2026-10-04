using ESD.Core;

namespace ESD.Device.Communication;

/// <summary>
/// Sends PING every <see cref="IntervalMs"/> milliseconds.
/// Raises <see cref="DeviceOffline"/> after <see cref="MaxMisses"/> consecutive misses.
/// Raises <see cref="DeviceOnline"/> when heartbeat recovers.
/// </summary>
internal sealed class HeartbeatManager
{
    private readonly CommandManager _cmd;
    private readonly byte           _address;
    private readonly int            _intervalMs;
    private readonly int            _maxMisses;
    private readonly TimeSpan       _pingTimeout;

    private int   _missCount;
    private bool  _isOnline = true;
    private Task? _loop;
    private CancellationTokenSource? _cts;

    public bool IsOnline => _isOnline;

    public event EventHandler? DeviceOffline;
    public event EventHandler? DeviceOnline;

    public HeartbeatManager(
        CommandManager cmd,
        byte   address,
        int    intervalMs  = 5_000,
        int    maxMisses   = 3,
        int    pingTimeoutMs = 500)
    {
        _cmd         = cmd;
        _address     = address;
        _intervalMs  = intervalMs;
        _maxMisses   = maxMisses;
        _pingTimeout = TimeSpan.FromMilliseconds(pingTimeoutMs);
    }

    public void Start(CancellationToken ct)
    {
        _cts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        byte seq = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_intervalMs, ct).ConfigureAwait(false);

            var result = await _cmd.SendAsync(
                _address, EsdCommand.Ping, seq++, null,
                _pingTimeout, retryCount: 0, ct).ConfigureAwait(false);

            if (result.Status == CommandResultStatus.Ok)
            {
                _missCount = 0;
                if (!_isOnline)
                {
                    _isOnline = true;
                    DeviceOnline?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _missCount++;
                if (_isOnline && _missCount >= _maxMisses)
                {
                    _isOnline = false;
                    DeviceOffline?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
