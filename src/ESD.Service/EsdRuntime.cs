using ESD.Core;
using ESD.Data;

namespace ESD.Service;

public sealed class EsdRuntime : IAsyncDisposable
{
    public AppSettings Settings { get; }
    public EventManager Events { get; }
    public DeviceManager Devices { get; }
    public BackgroundDbWorker DbWorker { get; }
    public FileLogger Logger { get; }

    public EsdRuntime(string configPath)
    {
        Settings = JsonConfig.Load(configPath);
        Logger = new FileLogger(Settings.LogDirectory);
        Events = new EventManager();
        Devices = new DeviceManager(Settings, Events, Logger);
        var repo = new MySqlEventRepository(Settings.DatabaseConnectionString);
        DbWorker = new BackgroundDbWorker(repo, Logger);

        Events.EventReceived += (_, e) => DbWorker.Enqueue(e);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        DbWorker.Start(ct);
        await Devices.StartAsync(ct);
        Logger.Info("ESD Runtime started.");
    }

    public async ValueTask DisposeAsync()
    {
        await Devices.StopAsync();
        await DbWorker.StopAsync();
        Logger.Info("ESD Runtime stopped.");
    }
}
