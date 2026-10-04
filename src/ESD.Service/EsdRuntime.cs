using ESD.Core;
using ESD.Data;
using Microsoft.EntityFrameworkCore;

namespace ESD.Service;

/// <summary>
/// Composes the full runtime: config → logger → devices → events → DB worker.
/// </summary>
public sealed class EsdRuntime : IAsyncDisposable
{
    public AppSettings        Settings  { get; }
    public FileLogger         Logger    { get; }
    public EventManager       Events    { get; }
    public DeviceManager      Devices   { get; }
    public BackgroundDbWorker DbWorker  { get; }

    public EsdRuntime(string configPath)
    {
        Settings = JsonConfig.Load(configPath);
        Logger   = new FileLogger(Settings.LogDirectory);
        Events   = new EventManager();
        Devices  = new DeviceManager(Settings, Events, Logger);

        var dbOptions = BuildDbOptions(Settings.DatabaseConnectionString);
        var repo      = new EfEventRepository(dbOptions);
        DbWorker      = new BackgroundDbWorker(repo, Logger);

        // Wire event pipeline: Device → EventManager → DB queue
        Events.EventReceived += (_, e) => DbWorker.Enqueue(e);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // Auto-migrate: creates the database and tables if they don't exist
        try
        {
            await using var ctx = new EsdDbContext(
                BuildDbOptions(Settings.DatabaseConnectionString));
            await ctx.Database.MigrateAsync(ct);
            Logger.Info("Database migration applied successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Database migration failed (events will queue in memory): {ex.Message}");
        }

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

    private static DbContextOptions<EsdDbContext> BuildDbOptions(string connectionString) =>
        new DbContextOptionsBuilder<EsdDbContext>()
            .UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory"))
            .Options;
}
