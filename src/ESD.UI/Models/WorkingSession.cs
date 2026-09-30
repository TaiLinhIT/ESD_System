namespace ESD.UI.Models;

public class WorkingSession
{
    public string EmployeeId { get; set; } = "";
    public string StationCode { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public TimeSpan OkTime { get; set; }
    public TimeSpan NgTime { get; set; }
    public TimeSpan DisconnectedTime { get; set; }
    public double Compliance { get; set; }
}
