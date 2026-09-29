using System.Threading.Channels;
using ESD.Core;

namespace ESD.Service;

public sealed class BackgroundDbWorker
{
    private readonly Channel<DbEvent> _queue = Channel.CreateUnbounded<DbEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly IEventRepository _repository;
    private readonly FileLogger _log;
    private CancellationTokenSource? _cts;
    private Task? _worker;

    private int _queueCount;

    public int QueueCount => Volatile.Read(ref _queueCount);

    public BackgroundDbWorker(IEventRepository repository, FileLogger log)
    {
        _repository = repository;
        _log = log;
    }

    public void Enqueue(EsdEvent evt)
    {
        var item = new DbEvent(evt.Timestamp, evt.Device, evt.EmployeeId,
            evt.EventType, evt.Status, evt.RawData, evt.Message);

        if (_queue.Writer.TryWrite(item))
            Interlocked.Increment(ref _queueCount);
        else
            _log.Error("DB queue rejected event.");
    }

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _worker = Task.Run(async () =>
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    await _repository.InsertAsync(item, _cts.Token);
                    Interlocked.Decrement(ref _queueCount);
                }
                catch (Exception ex)
                {
                    _log.Error($"DB insert failed: {ex.Message}");
                    // Retry once after a delay; event remains in memory while retrying.
                    try
                    {
                        await Task.Delay(1000, _cts.Token);
                        await _repository.InsertAsync(item, _cts.Token);
                        Interlocked.Decrement(ref _queueCount);
                    }
                    catch (Exception retryEx)
                    {
                        _log.Error($"DB retry failed: {retryEx.Message}");
                        Interlocked.Decrement(ref _queueCount);
                    }
                }
            }
        }, _cts.Token);
    }

    public async Task StopAsync()
    {
        _queue.Writer.TryComplete();
        if (_worker != null)
        {
            try { await _worker; } catch (OperationCanceledException) { }
        }
        _cts?.Cancel();
    }
}
