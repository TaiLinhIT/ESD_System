namespace ESD.UI.Models;

public enum AlarmSeverity { Info, Warning, Critical }

public class Alarm
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string StationCode { get; set; } = "";
    public string Message { get; set; } = "";
    public AlarmSeverity Severity { get; set; }
    public bool Acknowledged { get; set; }
}
