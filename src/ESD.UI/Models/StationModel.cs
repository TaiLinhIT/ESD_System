using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ESD.UI.Models;

/// <summary>
/// Observable station model bound to Dashboard DataGrid.
/// Receives live updates from MainViewModel when device events arrive.
/// </summary>
public sealed class StationModel : INotifyPropertyChanged
{
    private StationStatus _status;
    private string        _employeeId   = "";
    private string        _operatorName = "";
    private DateTime      _lastUpdate;
    private string        _lastEvent    = "";
    private double        _compliance;

    public string DeviceName   { get; set; } = "";
    public string Code         { get; set; } = "";
    public string Line         { get; set; } = "";
    public string OperatorId
    {
        get => _employeeId;
        set { _employeeId = value; OnPropertyChanged(); }
    }
    public string OperatorName
    {
        get => _operatorName;
        set { _operatorName = value; OnPropertyChanged(); }
    }
    public StationStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }
    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set { _lastUpdate = value; OnPropertyChanged(); }
    }
    public string LastEvent
    {
        get => _lastEvent;
        set { _lastEvent = value; OnPropertyChanged(); }
    }
    public double Compliance
    {
        get => _compliance;
        set { _compliance = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
