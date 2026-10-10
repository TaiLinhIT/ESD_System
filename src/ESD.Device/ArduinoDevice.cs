using System.Text;
using ESD.Core;
using ESD.Device.Communication;
using ESD.Device.Transport;

namespace ESD.Device;
/// <summary>
/// High-level facade for an Arduino-based ESD station.
///
/// Owns the serial transport + <see cref="ArduinoCommandManager"/>
/// and translates every Arduino protocol frame into domain
/// <see cref="EsdEvent"/>s, so the rest of the pipeline
/// (EventManager → DB worker → API → Web UI) is protocol-agnostic.
///
/// Frame → domain event mapping:
///   StatusReport (worker present)  → WorkerDetected + Strap Ok/Ng
///   StatusReport (no worker)       → WorkerRemoved
///   StatusReport (alarm flag)      → Alarm
///   AlarmTrigger                   → Alarm (with reason)
///   AlarmClear                     → AlarmReset
///   LockStation / UnlockStation    → Alarm / AlarmReset
///   Heartbeat ack after offline    → DeviceConnected
///   Heartbeat missed ×3            → DeviceDisconnected
/// </summary>
public sealed class ArduinoDevice : IEsdDevice
{
    private readonly ITransport                 _transport;
    private readonly ArduinoCommandManager  _commands;
    private readonly int                    _heartbeatIntervalMs;
    private readonly byte[]                 _hmacKey;

    private CancellationTokenSource? _heartbeatCts;
    private Task?                  _heartbeatTask;
    private int                    _heartbeatMisses;
    private bool                   _heartbeatOnline = true;
    private bool                   _firstHeartbeat  = true;
    private string?                _lastWorkerId;

    public string Name    { get; }
    public uint   DeviceId { get; }
    public byte   Address => (byte)(DeviceId & 0xFF);

    public bool IsConnected => _transport.IsConnected && _heartbeatOnline;

    /// <summary>
    /// Interface compliance — ESD-V1 frames are not produced by
    /// this device; subscribe to <see cref="DomainEventReceived"/>.
    /// </summary>
    public event EventHandler<EsdFrame>? EventReceived;

    /// <summary>Decoded domain events — consumed by DeviceManager.</summary>
    public event EventHandler<EsdEvent>? DomainEventReceived;

    /// <summary>Raw protocol frames (diagnostics).</summary>
    public event EventHandler<ArduinoFrame>? FrameReceived;

    /// <summary>Raised for every decoded frame as a human-readable line.</summary>
    public event EventHandler<string>? RawFrameLogging;

    /// <summary>Raised when a frame fails HMAC/structure validation.</summary>
    public event EventHandler<string>? DecodeError;

    public ArduinoDevice(
        string name,
        uint deviceId,
        ITransport transport,
        ReadOnlySpan<byte> hmacKey,
        int retryCount          = 3,
        int commandTimeoutMs    = 3000,
        int heartbeatIntervalMs = 5000)
    {
        Name     = name;
        DeviceId = deviceId;
        _transport           = transport;
        _hmacKey             = hmacKey.ToArray();
        _heartbeatIntervalMs = heartbeatIntervalMs;

        _commands = new ArduinoCommandManager(
            transport, deviceId, hmacKey, retryCount);
        _commands.FrameReceived += (_, f) =>
        {
            FrameReceived?.Invoke(this, f);
            OnFrame(f);
        };
        _commands.DecodeError += (_, msg) =>
            DecodeError?.Invoke(this, msg);
    }

    // ─────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────

    public async Task OpenAsync(CancellationToken ct)
    {
        await _transport.OpenAsync(ct).ConfigureAwait(false);

        _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));
    }

    public async Task CloseAsync()
    {
        _heartbeatCts?.Cancel();
        if (_heartbeatTask is not null)
        {
            try { await _heartbeatTask; } catch (OperationCanceledException) { }
        }
        _heartbeatCts?.Dispose();

        await _transport.CloseAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _commands.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    // ─────────────────────────────────────────
    // Heartbeat — liveness via Heartbeat/Ack
    // ─────────────────────────────────────────

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_heartbeatIntervalMs, ct).ConfigureAwait(false);

                var result = await _commands.SendAsync(
                    ArduinoCommand.Heartbeat,
                    timeout: TimeSpan.FromSeconds(2),
                    retryCount: 0,
                    ct: ct).ConfigureAwait(false);

                if (result.Status == ArduinoCommandStatus.Ok)
                {
                    _heartbeatMisses = 0;
                    if (!_heartbeatOnline)
                    {
                        _heartbeatOnline = true;
                        Publish(EsdEventType.DeviceConnected, null,
                            WristStrapStatus.NotConnected,
                            "Heartbeat recovered");
                    }
                    else if (_firstHeartbeat)
                    {
                        _firstHeartbeat = false;
                        Publish(EsdEventType.DeviceConnected, null,
                            WristStrapStatus.NotConnected,
                            "Device connected");
                    }
                }
                else
                {
                    _heartbeatMisses++;
                    if (_heartbeatOnline && _heartbeatMisses >= 3)
                    {
                        _heartbeatOnline = false;
                        Publish(EsdEventType.DeviceDisconnected, null,
                            WristStrapStatus.NotConnected,
                            $"Heartbeat lost after {_heartbeatMisses} misses");
                    }
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception)
            {
                // Heartbeat failure — counted as a miss; logged by the caller
            }
        }
    }

    // ─────────────────────────────────────────
    // Frame → domain event translation
    // ─────────────────────────────────────────

    private void OnFrame(ArduinoFrame frame)
    {
        if (RawFrameLogging is not null)
            RawFrameLogging(this, $"{frame.Command} SEQ={frame.Sequence} DEV={frame.DeviceId} DATA={ArduinoFrameCodec.ToHex(frame.Payload)}");

        switch (frame.Command)
        {
            case ArduinoCommand.StatusReport:
                OnStatusReport(frame);
                break;

            case ArduinoCommand.AlarmTrigger:
                var reason = frame.Payload.Length > 0
                    ? (AlarmReason)frame.Payload[0]
                    : AlarmReason.ManualTrigger;
                Publish(EsdEventType.Alarm, _lastWorkerId,
                    WristStrapStatus.Ng,
                    $"Alarm triggered: {reason} (0x{frame.Payload.FirstOrDefault():X2})");
                break;

            case ArduinoCommand.AlarmClear:
                Publish(EsdEventType.AlarmReset, _lastWorkerId,
                    WristStrapStatus.Ok, "Alarm cleared");
                break;

            case ArduinoCommand.LockStation:
                Publish(EsdEventType.Alarm, _lastWorkerId,
                    WristStrapStatus.Ng, "Station locked");
                break;

            case ArduinoCommand.UnlockStation:
                Publish(EsdEventType.AlarmReset, _lastWorkerId,
                    WristStrapStatus.Ok, "Station unlocked");
                break;

            case ArduinoCommand.GetInfo:
                Publish(EsdEventType.DeviceConnected, null,
                    WristStrapStatus.NotConnected,
                    $"Device info: {Encoding.UTF8.GetString(frame.Payload)}");
                break;

            case ArduinoCommand.LogUpload:
                Publish(EsdEventType.DeviceConnected, null,
                    WristStrapStatus.NotConnected,
                    $"Log upload: {Encoding.UTF8.GetString(frame.Payload)}");
                break;
        }
    }

    /// <summary>
    /// StatusReport payload layout:
    ///   [0] locked  (1 = station locked)
    ///   [1] alarm   (1 = alarm active)
    ///   [2] workerId length N
    ///   [3..3+N) workerId (UTF-8)
    /// </summary>
    private void OnStatusReport(ArduinoFrame frame)
    {
        if (frame.Payload.Length < 3)
        {
            DecodeError?.Invoke(this, $"{Name}: StatusReport payload too short");
            return;
        }

        bool locked = frame.Payload[0] == 1;
        bool alarm  = frame.Payload[1] == 1;
        int workerLen = frame.Payload[2];

        string? workerId = null;
        if (workerLen > 0 && frame.Payload.Length >= 3 + workerLen)
            workerId = Encoding.UTF8.GetString(
                frame.Payload, 3, Math.Min(workerLen, frame.Payload.Length - 3));

        if (workerId is not null)
        {
            _lastWorkerId = workerId;

            var strap = (alarm || locked)
                ? WristStrapStatus.Ng
                : WristStrapStatus.Ok;

            Publish(EsdEventType.WorkerDetected, workerId, strap,
                $"Worker present — Locked={locked} Alarm={alarm}");
        }
        else
        {
            _lastWorkerId = null;

            Publish(EsdEventType.WorkerRemoved, null,
                WristStrapStatus.NotConnected,
                $"Worker removed — Locked={locked} Alarm={alarm}");
        }

        if (alarm)
            Publish(EsdEventType.Alarm, workerId, WristStrapStatus.Ng,
                "Station alarm active");
    }

    // ─────────────────────────────────────────
    // Public command API (for services / tools)
    // ─────────────────────────────────────────

    public Task<ArduinoCommandResult> AttachWorkerAsync(string workerId, CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.Attach, Encoding.UTF8.GetBytes(workerId), ct: ct);

    public Task<ArduinoCommandResult> DetachWorkerAsync(string workerId, CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.Detach, Encoding.UTF8.GetBytes(workerId), ct: ct);

    public Task<ArduinoCommandResult> ScanWorkerAsync(string workerId, CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.WorkerScan, Encoding.UTF8.GetBytes(workerId), ct: ct);

    public Task<ArduinoCommandResult> EsdCheckAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.EsdCheck, ct: ct);

    public Task<ArduinoCommandResult> GetInfoAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.GetInfo, ct: ct);

    public Task<ArduinoCommandResult> SetConfigAsync(string json, CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.SetConfig, Encoding.UTF8.GetBytes(json), ct: ct);

    public Task<ArduinoCommandResult> TriggerAlarmAsync(AlarmReason reason, CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.AlarmTrigger, new[] { (byte)reason }, ct: ct);

    public Task<ArduinoCommandResult> ClearAlarmAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.AlarmClear, ct: ct);

    public Task<ArduinoCommandResult> LockStationAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.LockStation, ct: ct);

    public Task<ArduinoCommandResult> UnlockStationAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.UnlockStation, ct: ct);

    public Task<ArduinoCommandResult> UploadLogAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.LogUpload, ct: ct);

    public Task<ArduinoCommandResult> RebootAsync(CancellationToken ct = default)
        => _commands.SendAsync(ArduinoCommand.Reboot, ct: ct);

    // ─────────────────────────────────────────
    // Interface compliance (ESD-V1 command surface)
    // ─────────────────────────────────────────

    public Task<CommandResult> SendCommandAsync(
        EsdCommand command,
        byte[]? data = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        // Map the few ESD-V1 commands that make sense onto Arduino commands
        var arduinoCommand = command switch
        {
            EsdCommand.Ping         => ArduinoCommand.Heartbeat,
            EsdCommand.GetStatus    => ArduinoCommand.StatusReport,
            EsdCommand.GetDeviceInfo or EsdCommand.GetVersion => ArduinoCommand.GetInfo,
            _ => (ArduinoCommand?)null,
        };

        if (arduinoCommand is null)
            return Task.FromResult(new CommandResult(
                CommandResultStatus.Nack, null,
                NackError.UnknownCommand,
                $"Command {command} is not supported by Arduino protocol"));

        return SendViaArduinoAsync(arduinoCommand.Value, data, timeout, ct);
    }

    private async Task<CommandResult> SendViaArduinoAsync(
        ArduinoCommand command, byte[]? data, TimeSpan? timeout, CancellationToken ct)
    {
        var result = await _commands.SendAsync(
            command, data, timeout, ct: ct).ConfigureAwait(false);

        return result.Status switch
        {
            ArduinoCommandStatus.Ok => new CommandResult(
                CommandResultStatus.Ok, ToEsdFrame(result.Frame!, EsdCommand.Ack)),
            ArduinoCommandStatus.Nack => new CommandResult(
                CommandResultStatus.Nack, ToEsdFrame(result.Frame!, EsdCommand.Nack),
                MapErrorCode(result.ErrorCode), result.Message),
            _ => new CommandResult(
                CommandResultStatus.Timeout, null,
                Message: result.Message),
        };
    }

    private static NackError MapErrorCode(ArduinoErrorCode? code)
        => code switch
        {
            ArduinoErrorCode.UnknownCommand     => NackError.UnknownCommand,
            ArduinoErrorCode.BadLength          => NackError.InvalidLength,
            ArduinoErrorCode.BadCrc             => NackError.CrcError,
            ArduinoErrorCode.DeviceNotAuthorized => NackError.PermissionError,
            ArduinoErrorCode.Busy               => NackError.DeviceBusy,
            ArduinoErrorCode.Replay             => NackError.Timeout,
            _ => NackError.UnknownCommand,
        };

    private EsdFrame ToEsdFrame(ArduinoFrame f, EsdCommand command)
        => new EsdFrame(
            Version: f.Version,
            Address: Address,
            Command: command,
            Sequence: (byte)(f.Sequence & 0xFF),
            Data: f.Payload);

    // ─────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────

    private void Publish(
        EsdEventType eventType,
        string? employeeId,
        WristStrapStatus strapStatus,
        string message)
    {
        var evt = new EsdEvent(
            Timestamp:    DateTime.Now,
            DeviceName:   Name,
            DeviceAddress: Address,
            EventType:    eventType,
            EmployeeId:   employeeId ?? string.Empty,
            StrapStatus:  strapStatus,
            RawHex:       string.Empty,
            Message:      message);

        DomainEventReceived?.Invoke(this, evt);
    }
}
