using ESD.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ESD API", Version = "v1" });
});

var app    = builder.Build();
var cfgPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
var runtime = new EsdRuntime(cfgPath);
var cts     = new CancellationTokenSource();

try
{
    await runtime.StartAsync(cts.Token);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "ESD Runtime failed to start.");
}

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
});

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapGet("/", () => Results.Ok(new { service = "ESD.API", version = "1.0" }))
   .WithTags("Health");

app.MapGet("/api/status", () => Results.Ok(new
{
    service   = "running",
    devices   = runtime.Devices.Devices.Count,
    connected = runtime.Devices.Devices.Count(d => d.IsConnected),
    dbQueue   = runtime.DbWorker.QueueCount,
    utc       = DateTime.UtcNow,
})).WithTags("Status");

app.MapGet("/api/devices", () =>
    Results.Ok(runtime.Devices.Devices.Select(d => new
    {
        d.Name,
        d.Address,
        d.IsConnected,
    }))).WithTags("Devices");

// ── Swagger ───────────────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
