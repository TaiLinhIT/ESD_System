using System.Collections.Concurrent;
using ESD.Core;

namespace ESD.Device.Communication;

/// <summary>
/// Serializes outgoing commands one at a time (no concurrent requests unless
/// the device explicitly supports it), matches responses by SEQ number,
/// enforces timeout and retry, and exposes unsolicited event frames.
/// </summary>
internal sealed class CommandManager : IDisposable
{
    // Pending requests keyed by SEQ number
    private readonly ConcurrentDictionary<byte, PendingRequest> _pending = new();

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ITransport    _transport;
    private readonly IProtocol     _protocol;
    private readonly int           _defaultRetry;

    // Buffer + parse state — only touched from the DataReceived callback
    private readonly List<byte> _receiveBuffer = new(256);
    private readonly object     _bufLock       = new();

    public event EventHandler<EsdFrame>? EventReceived;

    public CommandManager(ITransport transport, IProtocol protocol, int defaultRetry = 3)
    {
        _transport    = transport;
        _protocol     = protocol;
        _defaultRetry = defaultRetry;
        _transport.DataReceived += OnRawBytesReceived;
    }

    // ─────────────────────────────────────────
    // Receive path: buffer → frame parser
    // ─────────────────────────────────────────

    private void OnRawBytesReceived(object? sender, byte[] chunk)
    {
        lock (_bufLock)
        {
            _receiveBuffer.AddRange(chunk);
            ParseBuffer();
        }
    }

    private void ParseBuffer()
    {
        while (_receiveBuffer.Count > 0)
        {
            var span = System.Runtime.InteropServices.CollectionsMarshal
                           .AsSpan(_receiveBuffer);
            try
            {
                bool got = _protocol.TryParse(span, out var frame, out int consumed);
                if (consumed > 0)
                    _receiveBuffer.RemoveRange(0, consumed);

                if (!got)
                {
                    // Protocol discarded some bytes (e.g. garbage before STX,
                    // or LEN too large). Resync: continue looking for the next frame.
                    if (consumed > 0) continue;
                    break; // NeedMore — wait for more data
                }

                DispatchFrame(frame!);
            }
            catch (InvalidDataException)
            {
                // Bad CRC — consumed is already set to 1 by the protocol, so
                // we skip one byte and retry.
                if (_receiveBuffer.Count > 0)
                    _receiveBuffer.RemoveAt(0);
            }
        }
    }

    private void DispatchFrame(EsdFrame frame)
    {
        // Unsolicited event — route directly to EventReceived
        if (frame.Command == EsdCommand.Event || frame.Command == EsdCommand.Alarm)
        {
            EventReceived?.Invoke(this, frame);
            return;
        }

        // Try to match a pending request by SEQ
        if (_pending.TryRemove(frame.Sequence, out var pending))
            pending.Complete(frame);
        else
            // Unknown SEQ — treat as event
            EventReceived?.Invoke(this, frame);
    }

    // ─────────────────────────────────────────
    // Send path: queue, timeout, retry
    // ─────────────────────────────────────────

    public async Task<CommandResult> SendAsync(
        byte          address,
        EsdCommand    command,
        byte          sequence,
        byte[]?       data,
        TimeSpan      timeout,
        int           retryCount,
        CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ExecuteWithRetryAsync(
                address, command, sequence, data, timeout, retryCount, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<CommandResult> ExecuteWithRetryAsync(
        byte address, EsdCommand command, byte sequence,
        byte[]? data, TimeSpan timeout, int retryCount, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            var pending = new PendingRequest();
            _pending[sequence] = pending;

            var frame = _protocol.BuildFrame(address, command, sequence, data);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                var response = await pending.WaitAsync(timeoutCts.Token)
                                            .ConfigureAwait(false);

                if (response.Command == EsdCommand.Nack)
                {
                    var nackCode = response.Data.Length > 0
                        ? (NackError)response.Data[0]
                        : NackError.UnknownCommand;
                    return new CommandResult(CommandResultStatus.Nack, response, nackCode,
                        $"NACK: {nackCode}");
                }

                return new CommandResult(CommandResultStatus.Ok, response);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _pending.TryRemove(sequence, out _);
                // Timeout on this attempt — retry unless last
                if (attempt == retryCount)
                    return new CommandResult(CommandResultStatus.Timeout, null,
                        Message: $"Timeout after {retryCount + 1} attempt(s)");
            }
        }

        return new CommandResult(CommandResultStatus.Timeout, null,
            Message: "Exceeded retry count");
    }

    public void Dispose()
    {
        _transport.DataReceived -= OnRawBytesReceived;
        foreach (var p in _pending.Values) p.Cancel();
        _pending.Clear();
        _sendLock.Dispose();
    }

    // ─────────────────────────────────────────
    // Helper: waitable pending request
    // ─────────────────────────────────────────
    private sealed class PendingRequest
    {
        private readonly TaskCompletionSource<EsdFrame> _tcs = new();

        public Task<EsdFrame> WaitAsync(CancellationToken ct) =>
            _tcs.Task.WaitAsync(ct);

        public void Complete(EsdFrame frame)  => _tcs.TrySetResult(frame);
        public void Cancel()                   => _tcs.TrySetCanceled();
    }
}
