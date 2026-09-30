using ESD.UI.Models;
using ESD.UI.Services;
using ESD.UI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ESD.UI.ViewModels;
public class MainViewModel : ViewModelBase
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Rs485Service _rs485 = new();
    private object _currentView;
    private string _currentTime = "";
    private bool _tvMode;

    public MainViewModel()
    {
        Stations = new ObservableCollection<Station>();
        LoadStations();

        _currentView = new DashboardView { DataContext = new DashboardViewModel(Stations) };
        _timer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        _timer.Start();

        ShowDashboardCommand = new RelayCommand(() => CurrentView = new DashboardView { DataContext = new DashboardViewModel(Stations) });
        ShowHistoryCommand = new RelayCommand(() => CurrentView = new HistoryView { DataContext = new HistoryViewModel() });
        ShowWorkingTimeCommand = new RelayCommand(() => CurrentView = new WorkingTimeView { DataContext = new WorkingTimeViewModel() });
        ShowReportCommand = new RelayCommand(() => CurrentView = new ReportView { DataContext = new ReportViewModel() });
        ShowSettingsCommand = new RelayCommand(() => CurrentView = new SettingView { DataContext = new SettingViewModel(_rs485) });
        ToggleTvModeCommand = new RelayCommand(() => TvMode = !TvMode);
    }

    public ObservableCollection<Station> Stations { get; }
    public object CurrentView { get => _currentView; private set { _currentView = value; OnPropertyChanged(); } }
    public string CurrentTime { get => _currentTime; private set { _currentTime = value; OnPropertyChanged(); } }
    public string CommunicationStatus => _rs485.IsConnected ? "RS485: CONNECTED" : "RS485: SIMULATION";
    public string DatabaseStatus => "DATABASE: READY";
    public bool TvMode { get => _tvMode; private set { _tvMode = value; OnPropertyChanged(); } }

    public RelayCommand ShowDashboardCommand { get; }
    public RelayCommand ShowHistoryCommand { get; }
    public RelayCommand ShowWorkingTimeCommand { get; }
    public RelayCommand ShowReportCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }
    public RelayCommand ToggleTvModeCommand { get; }

    private void LoadStations()
    {
        int id = 1;
        for (int i = 1; i <= 50; i++)
            Stations.Add(CreateStation(id++, "MAIN", $"M{i:00}", i));
        for (int i = 1; i <= 38; i++)
            Stations.Add(CreateStation(id++, "SUB", $"S{i:00}", i));
    }

    private static Station CreateStation(int id, string line, string code, int index)
    {
        var status = index % 29 == 0 ? StationStatus.Ng :
                     index % 17 == 0 ? StationStatus.NotConnected :
                     StationStatus.Ok;
        return new Station
        {
            Id = id,
            Line = line,
            Code = code,
            OperatorId = $"EMP{index:000}",
            OperatorName = $"Operator {index:00}",
            Status = status,
            LastUpdate = DateTime.Now,
            StatusDuration = TimeSpan.FromMinutes(index % 35),
            Compliance = status == StationStatus.Ok ? 98.5 : 76.0
        };
    }
}
