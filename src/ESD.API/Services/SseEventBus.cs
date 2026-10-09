using System.Threading.Channels;
using ESD.Core;

namespace ESD.API.Services;

/// <summary>
/// Bridges the in-process <see cref="IEventManager"/> stream to any
/// number of SSE clients. Each client gets its own unbounded channel,
/// so a slow or disconnected browser never blocks device event flow.
/// </summary>
public sealed class SseEventBus : IDisposable
{
    private readonly List<Channel<EsdEvent>> _clients = new();
    private readonly object                  _lock = new();
    private readonly IEventManager           _events;

    public SseEventBus(IEventManager events)
    {
        _events = events;
        _events.EventReceived += OnEvent;
    }

    private void OnEvent(object? sender, EsdEvent evt)
    {
        lock (_lock)
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                // Channel full (client too slow) → drop the subscriber
                if (!_clients[i].Writer.TryWrite(evt))
                    _clients.RemoveAt(i);
            }
        }
    }

    /// <summary>Subscribe to the live event stream.</summary>
    public SseSubscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<EsdEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        lock (_lock)
            _clients.Add(channel);

        return new SseSubscription(channel, this);
    }

    internal void Remove(Channel<EsdEvent> channel)
    {
        lock (_lock)
        {
            _clients.Remove(channel);
            channel.Writer.TryComplete();
        }
    }

    public int SubscriberCount
    {
        get { lock (_lock) return _clients.Count; }
    }

    public void Dispose()
        => _events.EventReceived -= OnEvent;
}

/// <summary>One SSE client subscription. Dispose to unsubscribe.</summary>
public sealed class SseSubscription : IDisposable
{
    private readonly Channel<EsdEvent> _channel;
    private readonly SseEventBus       _bus;
    private bool                       _disposed;

    public ChannelReader<EsdEvent> Reader => _channel.Reader;

    internal SseSubscription(Channel<EsdEvent> channel, SseEventBus bus)
    {
        _channel = channel;
        _bus     = bus;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bus.Remove(_channel);
    }
}
