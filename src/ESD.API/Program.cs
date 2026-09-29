using ESD.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
var runtime = new EsdRuntime(configPath);
var cts = new CancellationTokenSource();
await runtime.StartAsync(cts.Token);

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
});

app.MapGet("/", () => Results.Ok(new { service = "ESD.API", status = "running" }));
app.MapGet("/api/devices", () =>
    Results.Ok(runtime.Devices.Devices.Select(d => new { d.Name, d.IsConnected })));

app.MapGet("/api/status", () => Results.Ok(new
{
    service = "running",
    devices = runtime.Devices.Devices.Count,
    connected = runtime.Devices.Devices.Count(x => x.IsConnected),
    dbQueue = runtime.DbWorker.QueueCount
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
