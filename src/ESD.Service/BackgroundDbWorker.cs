using System.Threading.Channels;
using ESD.Core;

namespace ESD.Service;

public sealed class BackgroundDbWorker
{
    private readonly Channel<DbEvent> _queue = Channel.CreateUnbounded<DbEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly IEventRepository _repository;
    private readonly FileLogger       _log;

    private CancellationTokenSource? _cts;
    private Task?                    _worker;
    private int                      _queueCount;

    public int QueueCount => Volatile.Read(ref _queueCount);

    public BackgroundDbWorker(IEventRepository repository, FileLogger log)
    {
        _repository = repository;
        _log        = log;
    }

    public void Enqueue(EsdEvent evt)
    {
        var item = new DbEvent(
            evt.Timestamp,
            evt.DeviceName,
            evt.EmployeeId,
            evt.EventType.ToString(),
            evt.StrapStatus.ToString(),
            evt.RawHex,
            evt.Message);

        if (_queue.Writer.TryWrite(item))
            Interlocked.Increment(ref _queueCount);
        else
            _log.Error("[DB] Queue rejected event — channel full.");
    }

    public void Start(CancellationToken ct)
    {
        _cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _worker = Task.Run(() => ProcessLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(ct))
        {
            await InsertWithRetryAsync(item, ct);
            Interlocked.Decrement(ref _queueCount);
        }
    }

    private async Task InsertWithRetryAsync(DbEvent item, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _repository.InsertAsync(item, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _log.Error($"[DB] Insert attempt {attempt} failed: {ex.Message} — retrying…");
                await Task.Delay(1_000 * attempt, ct); // back-off: 1s, 2s
            }
            catch (Exception ex)
            {
                _log.Error($"[DB] Insert failed after {maxAttempts} attempts: {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        _queue.Writer.TryComplete();
        if (_worker is not null)
        {
            try { await _worker; }
            catch (OperationCanceledException) { }
        }
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
