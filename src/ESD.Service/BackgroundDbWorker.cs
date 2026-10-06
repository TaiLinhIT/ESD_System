using System.Threading.Channels;
using ESD.Core;

namespace ESD.Service;

public sealed class BackgroundDbWorker
{
    // Bounded channel: drop oldest when full, batch writes to DB
    private readonly Channel<DbEvent> _queue = Channel.CreateBounded<DbEvent>(
        new BoundedChannelOptions(5_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    public int QueueCount => _queue.Reader.Count;
    private readonly IEventRepository _repository;
    private readonly FileLogger       _log;

    private CancellationTokenSource? _cts;
    private Task?                    _worker;

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

        // Boundeded channel — if full, oldest event is dropped automatically
        // This prevents unbounded RAM growth when DB is down
        if (!_queue.Writer.TryWrite(item))
        {
            _log.Warn("[DB] Queue full — dropping oldest event to protect memory.");
        }
    }

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _worker = Task.Run(() => ProcessLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        var batch = new List<DbEvent>(100);

        while (await _queue.Reader.WaitToReadAsync(ct))
        {
            batch.Clear();
            // Drain up to 100 items or until 300ms elapsed
            var deadline = Task.Delay(300, ct);
            while (batch.Count < 100 && _queue.Reader.TryRead(out var e))
            {
                batch.Add(e);
            }

            if (batch.Count > 0)
            {
                await WriteBatchAsync(batch, ct);
            }

            // Reset batch for next iteration
            batch = new List<DbEvent>(100);
        }
    }

    private async Task WriteBatchAsync(List<DbEvent> batch, CancellationToken ct)
    {
        // Insert all events using the repository (each gets its own DbContext)
        foreach (var item in batch)
        {
            await _repository.InsertAsync(item, ct);
        }
    }

    public async Task StopAsync()
    {
        if (_worker is not null)
        {
            try { await _worker; } catch (OperationCanceledException) { }
        }
        _cts?.Cancel();
        _cts?.Dispose();
    }
}