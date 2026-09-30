namespace ESD.UI.Models;

public enum StationStatus { Ok, Ng, NotConnected, Offline }

public class Station
{
    public int Id { get; set; }
    public string Line { get; set; } = "";
    public string Code { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public StationStatus Status { get; set; }
    public DateTime LastUpdate { get; set; }
    public TimeSpan StatusDuration { get; set; }
    public double Compliance { get; set; }
}
