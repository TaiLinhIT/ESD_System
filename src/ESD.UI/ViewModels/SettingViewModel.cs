using ESD.Service;

namespace ESD.UI.ViewModels;

public sealed class SettingViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private string _status = "Idle";

    public SettingViewModel(AppSettings settings)
    {
        _settings = settings;
    }

    // Expose first device settings for the simple settings screen
    public string ComPort  { get => _settings.Devices.FirstOrDefault()?.PortName  ?? "COM3";  set { if (_settings.Devices.Count > 0) _settings.Devices[0].PortName  = value; OnPropertyChanged(); } }
    public int    BaudRate { get => _settings.Devices.FirstOrDefault()?.BaudRate  ?? 9600;    set { if (_settings.Devices.Count > 0) _settings.Devices[0].BaudRate  = value; OnPropertyChanged(); } }
    public string Parity   { get => _settings.Devices.FirstOrDefault()?.Parity    ?? "None";  set { if (_settings.Devices.Count > 0) _settings.Devices[0].Parity    = value; OnPropertyChanged(); } }
    public int    StopBits { get => _settings.Devices.FirstOrDefault()?.StopBits  ?? 1;       set { if (_settings.Devices.Count > 0) _settings.Devices[0].StopBits  = value; OnPropertyChanged(); } }
    public int    RetryCount { get => _settings.Devices.FirstOrDefault()?.RetryCount ?? 3;    set { if (_settings.Devices.Count > 0) _settings.Devices[0].RetryCount = value; OnPropertyChanged(); } }
    public int    HeartbeatMs { get => _settings.Devices.FirstOrDefault()?.HeartbeatIntervalMs ?? 5000; set { if (_settings.Devices.Count > 0) _settings.Devices[0].HeartbeatIntervalMs = value; OnPropertyChanged(); } }

    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    public RelayCommand SaveCommand => new(() => Status = "Settings saved (restart to apply).");
}
