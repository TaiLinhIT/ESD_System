using ESD.UI.Models;

namespace ESD.UI.Services;

public class AlarmService
{
    public event EventHandler<Alarm>? AlarmRaised;
    public void Raise(Alarm alarm) => AlarmRaised?.Invoke(this, alarm);
}
