using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESD.Service;

public sealed class JsonConfig
{
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            var def = Default();
            Save(path, def);
            return def;
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
        WriteIndented          = true,
        Converters             = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private static AppSettings Default() => new()
    {
        LogDirectory = "logs",
        Devices =
        [
            new DeviceSetting
            {
                Name                = "ESD-MOCK-01",
                ProtocolVersion     = "Mock",
                HeartbeatIntervalMs = 5_000,
                RetryCount          = 3,
            },
            new DeviceSetting
            {
                Name                = "ESD-RS485-01",
                PortName            = "COM3",
                BaudRate            = 115200,
                ProtocolVersion     = "ESD-V1",
                HeartbeatIntervalMs = 5_000,
                RetryCount          = 3,
            },
        ],
    };
}
