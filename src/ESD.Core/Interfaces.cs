namespace ESD.Core;

public interface IDevice : IAsyncDisposable
{
    string Name { get; }
    bool IsConnected { get; }
    event EventHandler<DeviceMessage>? DataReceived;
    Task OpenAsync(CancellationToken ct);
    Task CloseAsync();
    Task SendAsync(byte[] data, CancellationToken ct);
}

public interface IDeviceManager
{
    IReadOnlyCollection<IDevice> Devices { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}

public interface IEventManager
{
    event EventHandler<EsdEvent>? EventReceived;
    void Publish(EsdEvent evt);
}

public interface IEventRepository
{
    Task InsertAsync(DbEvent item, CancellationToken ct);
}
