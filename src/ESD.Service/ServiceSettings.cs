namespace ESD.Service;

public sealed class AppSettings
{
    public string LogDirectory            { get; set; } = "logs";
    public string DatabaseConnectionString { get; set; } =
        "Server=ADMIN-PC\\ANTHONYNGUYEN;Database=EsdSystem;Trusted_Connection=True;TrustServerCertificate=True;";
    public List<DeviceSetting> Devices    { get; set; } = [];
}

public sealed class DeviceSetting
{
    public string Name                { get; set; } = "ESD-MOCK-01";
    public string PortName            { get; set; } = "COM3";
    public int    BaudRate            { get; set; } = 9600;
    public int    DataBits            { get; set; } = 8;
    public string Parity              { get; set; } = "None";
    public int    StopBits            { get; set; } = 1;
    public int    ReadTimeoutMs       { get; set; } = 500;
    public int    WriteTimeoutMs      { get; set; } = 500;
    public int    RetryCount          { get; set; } = 3;
    public int    HeartbeatIntervalMs { get; set; } = 5_000;

    /// <summary>"Mock" or "ESD-V1" (serial).</summary>
    public string ProtocolVersion     { get; set; } = "Mock";
}
