using System.Collections.ObjectModel;
using ESD.UI.Models;

namespace ESD.UI.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    public ObservableCollection<EsdEvent> Events { get; } = new()
    {
        new EsdEvent { Id=1, Timestamp=DateTime.Now.AddMinutes(-5), StationCode="M03", OperatorId="EMP003", OldStatus=StationStatus.Ok, NewStatus=StationStatus.Ng, Duration=TimeSpan.FromSeconds(18), Message="ESD wrist strap NG" },
        new EsdEvent { Id=2, Timestamp=DateTime.Now.AddMinutes(-4), StationCode="M03", OperatorId="EMP003", OldStatus=StationStatus.Ng, NewStatus=StationStatus.Ok, Duration=TimeSpan.FromSeconds(18), Message="ESD wrist strap restored" },
        new EsdEvent { Id=3, Timestamp=DateTime.Now.AddMinutes(-2), StationCode="M17", OperatorId="EMP017", OldStatus=StationStatus.Ok, NewStatus=StationStatus.NotConnected, Duration=TimeSpan.FromSeconds(42), Message="Wrist strap disconnected" }
    };
}
