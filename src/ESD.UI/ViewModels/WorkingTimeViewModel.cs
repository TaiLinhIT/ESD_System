using System.Collections.ObjectModel;
using ESD.UI.Models;

namespace ESD.UI.ViewModels;

public class WorkingTimeViewModel : ViewModelBase
{
    public ObservableCollection<WorkingSession> Sessions { get; } = new()
    {
        new WorkingSession { EmployeeId="EMP001", StationCode="M01", Start=DateTime.Today.AddHours(8), End=DateTime.Today.AddHours(17), OkTime=TimeSpan.FromHours(8.2), NgTime=TimeSpan.FromMinutes(3), DisconnectedTime=TimeSpan.FromMinutes(27), Compliance=94.7 },
        new WorkingSession { EmployeeId="EMP002", StationCode="M02", Start=DateTime.Today.AddHours(8), End=DateTime.Today.AddHours(17), OkTime=TimeSpan.FromHours(8.7), NgTime=TimeSpan.FromMinutes(1), DisconnectedTime=TimeSpan.FromMinutes(12), Compliance=97.6 }
    };
}
