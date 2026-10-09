namespace ESD.API;

/// <summary>
/// API-level settings (host behaviour), separate from the device
/// runtime config read by <c>ESD.Service.JsonConfig</c>.
/// </summary>
public sealed class ApiSettings
{
    public const string SectionName = "Api";

    /// <summary>Allowed CORS origins (comma separated or "*").</summary>
    public string CorsOrigins { get; set; } = "*";

    /// <summary>SSE heartbeat interval in seconds.</summary>
    public int SSEHeartbeatSeconds { get; set; } = 15;

    /// <summary>Max page size accepted by list endpoints.</summary>
    public int MaxPageSize { get; set; } = 500;
}
