using ESD.UI.Models;

namespace ESD.UI.Services;

public class WorkingTimeService
{
    public TimeSpan CalculateTotal(WorkingSession session) =>
        session.OkTime + session.NgTime + session.DisconnectedTime;

    public double CalculateCompliance(WorkingSession session)
    {
        var total = CalculateTotal(session).TotalSeconds;
        return total <= 0 ? 0 : session.OkTime.TotalSeconds / total * 100.0;
    }
}
