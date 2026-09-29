using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESD.Service;

public sealed class JsonConfig
{
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            var settings = Default();
            Save(path, settings);
            return settings;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options()) ?? Default();
    }

    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options()));
    }

    private static JsonSerializerOptions Options() => new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static AppSettings Default() => new()
    {
        LogDirectory = "logs",
        Devices =
        [
            new DeviceSetting { Name = "ESD-MOCK-01", Transport = "Mock", PollIntervalMs = 1500 },
            new DeviceSetting { Name = "ESD-RS485-01", Transport = "Serial", PortName = "COM3", BaudRate = 9600, Protocol = "ModbusRtu" }
        ]
    };
}
