namespace ESD.Core;

// ─────────────────────────────────────────────
// Transport — raw byte I/O, knows nothing about protocol
// ─────────────────────────────────────────────
public interface ITransport : IAsyncDisposable
{
    string  Name        { get; }
    bool    IsConnected { get; }

    Task OpenAsync(CancellationToken ct);
    Task CloseAsync();
    Task SendAsync(byte[] data, CancellationToken ct);

    /// <summary>Raw bytes arrived from the physical port.</summary>
    event EventHandler<byte[]>? DataReceived;
}

// ─────────────────────────────────────────────
// Protocol — frame building + parsing
// ─────────────────────────────────────────────
public interface IProtocol
{
    /// <summary>Build a complete wire frame.</summary>
    byte[] BuildFrame(byte address, EsdCommand command, byte sequence, byte[]? data = null);

    /// <summary>
    /// Try to extract one complete frame from <paramref name="buffer"/>.
    /// Returns true and advances <paramref name="consumed"/> if a frame was found.
    /// Returns false if more bytes are needed.
    /// Throws <see cref="InvalidDataException"/> if the frame is corrupt (bad CRC etc.).
    /// </summary>
    bool TryParse(ReadOnlySpan<byte> buffer, out EsdFrame? frame, out int consumed);
}

// ─────────────────────────────────────────────
// Device — high-level command API
// ─────────────────────────────────────────────
public interface IEsdDevice : IAsyncDisposable
{
    string Name    { get; }
    byte   Address { get; }
    bool   IsConnected { get; }

    /// <summary>Send a command and wait for ACK/response with retry.</summary>
    Task<CommandResult> SendCommandAsync(
        EsdCommand command,
        byte[]?    data               = null,
        TimeSpan?  timeout            = null,
        CancellationToken ct          = default);

    /// <summary>Raised when the device pushes an unsolicited event frame.</summary>
    event EventHandler<EsdFrame>? EventReceived;

    Task OpenAsync(CancellationToken ct);
    Task CloseAsync();
}

// ─────────────────────────────────────────────
// Device Manager
// ─────────────────────────────────────────────
public interface IDeviceManager
{
    IReadOnlyCollection<IEsdDevice> Devices { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}

// ─────────────────────────────────────────────
// Event Manager
// ─────────────────────────────────────────────
public interface IEventManager
{
    event EventHandler<EsdEvent>? EventReceived;
    void Publish(EsdEvent evt);
}

// ─────────────────────────────────────────────
// Repository
// ─────────────────────────────────────────────
public interface IEventRepository
{
    Task InsertAsync(DbEvent item, CancellationToken ct);
}
