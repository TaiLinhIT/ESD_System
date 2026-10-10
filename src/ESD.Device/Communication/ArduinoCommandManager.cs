using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ESD.Core;

namespace ESD.Device.Communication;

/// <summary>
/// Command/response engine for the Arduino ESD protocol (V2).
///
/// Receive path (runs on the SerialPort.DataReceived thread):
///   raw bytes → buffer → find SOF (0xAA 0x55) → read payload length
///   → wait for full frame → verify EOF (0x0D 0x0A) → HMAC-SHA256
///   validation → dispatch.
///
/// Dispatch rules:
///   Ack / Nack  → matched by Sequence (uint) against pending requests.
///   Everything else (StatusReport, AlarmTrigger, LogUpload, …) →
///   raised as an unsolicited <see cref="ArduinoFrame"/> on
///   <see cref="FrameReceived"/> — the Arduino pushes these whenever
///   station state changes, without being asked.
///
/// Send path: frames are serialized through a semaphore, stamped with
/// an incrementing Sequence + unix timestamp, and retried on timeout.
/// </summary>
public sealed class ArduinoCommandManager : IDisposable
{
    private readonly ConcurrentDictionary<uint, PendingRequest> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ITransport _transport;
    private readonly byte[] _hmacKey;
    private readonly uint _deviceId;
    private readonly int _defaultRetry;

    // Buffer + parse state — only touched from the DataReceived callback
    private readonly List<byte> _receiveBuffer = new(512);
    private readonly object _bufLock = new();

    private uint _sequence;

    /// <summary>Raised for every decoded frame (diagnostics + logging).</summary>
    public event EventHandler<ArduinoFrame>? FrameReceived;

    public ArduinoCommandManager(
        ITransport transport,
        uint deviceId,
        ReadOnlySpan<byte> hmacKey,
        int defaultRetry = 3)
    {
        _transport    = transport;
        _deviceId     = deviceId;
        _hmacKey      = hmacKey.ToArray();
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
            var span = CollectionsMarshal.AsSpan(_receiveBuffer);

            // 1. Resync: find SOF (0xAA 0x55)
            int sof = IndexOfSof(span);
            if (sof < 0)
            {
                _receiveBuffer.Clear();
                return;
            }
            if (sof > 0) _receiveBuffer.RemoveRange(0, sof);

            // 2. Need at least the header to read the payload length
            if (_receiveBuffer.Count < ArduinoProtocolConstants.HeaderSize)
                return;

            // 3. Read frame length; drop 1 byte and resync on garbage length
            if (!ArduinoFrameCodec.TryReadFrameLength(
                    CollectionsMarshal.AsSpan(_receiveBuffer), out int total))
            {
                _receiveBuffer.RemoveAt(0);
                continue;
            }

            // 4. Wait for the complete frame
            if (_receiveBuffer.Count < total)
                return;

            // 5. Verify EOF marker; drop 1 byte and resync if corrupt
            if (_receiveBuffer[total - 2] != ArduinoProtocolConstants.Eof1
                || _receiveBuffer[total - 1] != ArduinoProtocolConstants.Eof2)
            {
                _receiveBuffer.RemoveAt(0);
                continue;
            }

            // 6. Extract the exact frame bytes and decode (HMAC)
            var raw = _receiveBuffer.Take(total).ToArray();
            _receiveBuffer.RemoveRange(0, total);

            if (!ArduinoFrameCodec.TryDecode(raw, _hmacKey, out var frame, out var error))
            {
                OnDecodeError(error, ArduinoFrameCodec.ToHex(raw));
                continue;
            }

            DispatchFrame(frame!);
        }
    }

    private static int IndexOfSof(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length - 1; i++)
        {
            if (span[i] == ArduinoProtocolConstants.Sof1
                && span[i + 1] == ArduinoProtocolConstants.Sof2)
                return i;
        }
        return -1;
    }

    private void DispatchFrame(ArduinoFrame frame)
    {
        // Response to a pending request — matched by Sequence
        if (frame.Command is ArduinoCommand.Ack or ArduinoCommand.Nack)
        {
            if (_pending.TryRemove(frame.Sequence, out var pending))
            {
                pending.Complete(frame);
                return;
            }
            // Ack/Nack without a pending request — treat as unsolicited
        }

        // Unsolicited push from the Arduino (StatusReport, AlarmTrigger, …)
        FrameReceived?.Invoke(this, frame);
    }

    /// <summary>Hook for logging corrupt frames (BadMac, BadVersion, …).</summary>
    public event EventHandler<string>? DecodeError;

    private void OnDecodeError(ArduinoErrorCode error, string rawHex)
        => DecodeError?.Invoke(this, $"{error} | RAW={rawHex}");

    // ─────────────────────────────────────────
    // Send path: serialize, stamp, timeout, retry
    // ─────────────────────────────────────────

    public async Task<ArduinoCommandResult> SendAsync(
        ArduinoCommand command,
        byte[]? payload        = null,
        TimeSpan? timeout      = null,
        int? retryCount        = null,
        CancellationToken ct   = default)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ExecuteWithRetryAsync(
                command, payload,
                timeout ?? TimeSpan.FromSeconds(3),
                retryCount ?? _defaultRetry,
                ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<ArduinoCommandResult> ExecuteWithRetryAsync(
        ArduinoCommand command,
        byte[]? payload,
        TimeSpan timeout,
        int retryCount,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            uint seq = ++_sequence;

            var pending = new PendingRequest();
            _pending[seq] = pending;

            var frame = new ArduinoFrame(
                ArduinoProtocolConstants.Version,
                ArduinoFrameFlags.AckRequested,
                _deviceId,
                command,
                seq,
                (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                payload ?? Array.Empty<byte>(),
                Array.Empty<byte>());

            await _transport.SendAsync(
                ArduinoFrameCodec.Encode(frame, _hmacKey), ct)
                .ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                var response = await pending.WaitAsync(timeoutCts.Token)
                                             .ConfigureAwait(false);

                if (response.Command == ArduinoCommand.Nack)
                {
                    var errorCode = response.Payload.Length > 0
                        ? (ArduinoErrorCode)response.Payload[0]
                        : ArduinoErrorCode.UnknownCommand;
                    return new ArduinoCommandResult(
                        ArduinoCommandStatus.Nack, response, errorCode,
                        $"NACK: {errorCode}");
                }

                if (response.Command == ArduinoCommand.Error)
                {
                    return new ArduinoCommandResult(
                        ArduinoCommandStatus.Error, response,
                        ArduinoErrorCode.UnknownCommand,
                        "Device reported Error frame");
                }

                return new ArduinoCommandResult(ArduinoCommandStatus.Ok, response);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _pending.TryRemove(seq, out _);
                if (attempt == retryCount)
                    return new ArduinoCommandResult(
                        ArduinoCommandStatus.Timeout, null, null,
                        $"Timeout after {retryCount + 1} attempt(s)");
            }
        }

        return new ArduinoCommandResult(
            ArduinoCommandStatus.Timeout, null, null, "Exceeded retry count");
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
        private readonly TaskCompletionSource<ArduinoFrame> _tcs = new();

        public Task<ArduinoFrame> WaitAsync(CancellationToken ct) =>
            _tcs.Task.WaitAsync(ct);

        public void Complete(ArduinoFrame frame) => _tcs.TrySetResult(frame);
        public void Cancel() => _tcs.TrySetCanceled();
    }
}
