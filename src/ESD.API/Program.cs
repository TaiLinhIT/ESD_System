using ESD.API;
using ESD.API.Services;
using ESD.Core;
using ESD.Data;
using ESD.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────
// Host settings (ports, CORS) come from appsettings.json,
// device/runtime settings come from config/appsettings.json.
var apiSettings = builder.Configuration
    .GetSection(ApiSettings.SectionName)
    .Get<ApiSettings>() ?? new ApiSettings();

builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection(ApiSettings.SectionName));

// ── Services ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title    = "ESD System API",
        Version  = "v1",
        Description = "Real-time ESD wrist-strap monitoring: devices, events, dashboard analytics.",
        Contact  = new OpenApiContact { Name = "HAN ESD Team" },
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var origins = apiSettings.CorsOrigins;
        if (string.IsNullOrWhiteSpace(origins) || origins == "*")
            policy.AllowAnyOrigin();
        else
            policy.WithOrigins(origins.Split(';', StringSplitOptions.RemoveEmptyEntries));

        policy.AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Runtime: config → logger → devices → events → DB worker (singleton)
var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
builder.Services.AddSingleton(_ => new EsdRuntime(configPath));

// Expose the runtime's collaborators through their interfaces so
// DeviceStateTracker / SseEventBus / controllers can consume them.
builder.Services.AddSingleton(sp =>
{
    var runtime = sp.GetRequiredService<EsdRuntime>();
    return (IDeviceManager)runtime.Devices;
});
builder.Services.AddSingleton(sp =>
{
    var runtime = sp.GetRequiredService<EsdRuntime>();
    return (IEventManager)runtime.Events;
});

builder.Services.AddSingleton<DeviceStateTracker>();
builder.Services.AddSingleton<SseEventBus>();
builder.Services.AddHostedService<EsdRuntimeHostedService>();

// Read-side queries (each call uses a short-lived DbContext)
builder.Services.AddSingleton(sp =>
{
    var runtime = sp.GetRequiredService<EsdRuntime>();
    var options = new DbContextOptionsBuilder<ESD.Data.EsdDbContext>()
        .UseSqlServer(runtime.Settings.DatabaseConnectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory"))
        .Options;
    return new EsdEventQuery(options);
});

var app = builder.Build();

// ── Middleware ───────────────────────────────────────────────────────
app.UseCors("CorsPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ESD System API v1");
        c.RoutePrefix = string.Empty; // serve Swagger UI at /
    });
}

app.MapControllers();

app.Run();
