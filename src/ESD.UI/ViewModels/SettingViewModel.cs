using ESD.UI.Services;

namespace ESD.UI.ViewModels;

public class SettingViewModel : ViewModelBase
{
    private readonly Rs485Service _rs485;
    public SettingViewModel(Rs485Service rs485) => _rs485 = rs485;

    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Status => _rs485.IsConnected ? "Connected" : "Simulation / Disconnected";

    public RelayCommand ConnectCommand => new(() => _rs485.Connect(ComPort, BaudRate));
    public RelayCommand DisconnectCommand => new(() => _rs485.Disconnect());
}
