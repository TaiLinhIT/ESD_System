namespace ESD.UI.Models;

public class EsdEvent
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string StationCode { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public StationStatus OldStatus { get; set; }
    public StationStatus NewStatus { get; set; }
    public TimeSpan Duration { get; set; }
    public string Message { get; set; } = "";
}
