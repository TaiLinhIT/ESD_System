namespace ESD.Service;

public sealed class AppSettings
{
    public string LogDirectory { get; set; } = "logs";
    public string DatabaseConnectionString { get; set; } =
        "Server=localhost;Port=3306;Database=esd_system;User ID=root;Password=123456;";
    public List<DeviceSetting> Devices { get; set; } = [];
}

public sealed class DeviceSetting
{
    public string Name { get; set; } = "ESD-MOCK-01";
    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public int StopBits { get; set; } = 1;
    public string Transport { get; set; } = "Mock";
    public string Protocol { get; set; } = "Raw";
    public byte SlaveId { get; set; } = 1;
    public int PollIntervalMs { get; set; } = 1500;
}
