using ESD.Core;
using ESD.Service;
using ESD.UI.Models;
using ESD.UI.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ESD.UI.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _clockTimer;
    private readonly EsdRuntime      _runtime;
    private readonly CancellationTokenSource _cts = new();

    private object  _currentView = null!;
    private string  _currentTime = "";
    private bool    _tvMode;

    // ── Shared state exposed to child ViewModels ──────────────────────────────
    public ObservableCollection<StationModel> Stations { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────
    public RelayCommand ShowDashboardCommand   { get; }
    public RelayCommand ShowHistoryCommand     { get; }
    public RelayCommand ShowWorkingTimeCommand { get; }
    public RelayCommand ShowReportCommand      { get; }
    public RelayCommand ShowSettingsCommand    { get; }
    public RelayCommand ToggleTvModeCommand    { get; }

    // ── Bindable properties ───────────────────────────────────────────────────
    public object CurrentView
    {
        get => _currentView;
        private set { _currentView = value; OnPropertyChanged(); }
    }

    public string CurrentTime
    {
        get => _currentTime;
        private set { _currentTime = value; OnPropertyChanged(); }
    }

    public bool TvMode
    {
        get => _tvMode;
        private set { _tvMode = value; OnPropertyChanged(); }
    }

    public string CommunicationStatus =>
        _runtime.Devices.Devices.Any(d => d.IsConnected)
            ? "RS485: CONNECTED"
            : "RS485: SIMULATION";

    public string DatabaseStatus => "DATABASE: READY";

    // ── Constructor ───────────────────────────────────────────────────────────
    public MainViewModel()
    {
        // Load runtime from config
        var cfgPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
        _runtime = new EsdRuntime(cfgPath);

        // Pre-populate stations from device config
        SeedStations();

        // Wire real events from device layer into UI
        _runtime.Events.EventReceived += OnEsdEvent;

        // Start runtime in background
        _ = StartRuntimeAsync();

        // Clock
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        _clockTimer.Start();

        // Navigation
        ShowDashboardCommand   = new RelayCommand(() => CurrentView = new DashboardView   { DataContext = new DashboardViewModel(Stations) });
        ShowHistoryCommand     = new RelayCommand(() => CurrentView = new HistoryView     { DataContext = new HistoryViewModel() });
        ShowWorkingTimeCommand = new RelayCommand(() => CurrentView = new WorkingTimeView { DataContext = new WorkingTimeViewModel() });
        ShowReportCommand      = new RelayCommand(() => CurrentView = new ReportView      { DataContext = new ReportViewModel() });
        ShowSettingsCommand    = new RelayCommand(() => CurrentView = new SettingView     { DataContext = new SettingViewModel(_runtime.Settings) });
        ToggleTvModeCommand    = new RelayCommand(() => TvMode = !TvMode);

        // Default view
        CurrentView = new DashboardView { DataContext = new DashboardViewModel(Stations) };
    }

    private async Task StartRuntimeAsync()
    {
        try { await _runtime.StartAsync(_cts.Token); }
        catch (Exception ex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
                MessageBox.Show($"Runtime error: {ex.Message}", "ESD System",
                    MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }

    // ── Event handler from device layer (may come from any thread) ────────────
    private void OnEsdEvent(object? sender, EsdEvent e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            // Update matching station in the shared collection
            var station = Stations.FirstOrDefault(s => s.DeviceName == e.DeviceName);
            if (station is null) return;

            station.LastUpdate = e.Timestamp;
            station.OperatorId = e.EmployeeId;
            station.Status     = MapStatus(e.StrapStatus);
            station.LastEvent  = $"{e.EventType} | {e.Message}";

            OnPropertyChanged(nameof(CommunicationStatus));
        });
    }

    private static StationStatus MapStatus(WristStrapStatus s) => s switch
    {
        WristStrapStatus.Ok      => StationStatus.Ok,
        WristStrapStatus.Ng      => StationStatus.Ng,
        WristStrapStatus.Warning => StationStatus.Ng,
        WristStrapStatus.Error   => StationStatus.Ng,
        _                        => StationStatus.NotConnected,
    };

    private void SeedStations()
    {
        // Create one station per configured device
        foreach (var d in _runtime.Settings.Devices)
        {
            Stations.Add(new StationModel
            {
                DeviceName   = d.Name,
                Code         = d.Name,
                Line         = "MAIN",
                OperatorId   = "",
                OperatorName = "",
                Status       = StationStatus.NotConnected,
                LastUpdate   = DateTime.Now,
                Compliance   = 0,
            });
        }

        // Pad with simulated stations so Dashboard looks populated
        int id = Stations.Count + 1;
        for (int i = 1; i <= 48 && Stations.Count < 50; i++, id++)
            Stations.Add(CreateDemoStation(id, "MAIN", $"M{i:00}", i));
        for (int i = 1; i <= 38 && Stations.Count < 88; i++, id++)
            Stations.Add(CreateDemoStation(id, "SUB", $"S{i:00}", i));
    }

    private static StationModel CreateDemoStation(int id, string line, string code, int idx)
    {
        var status = idx % 29 == 0 ? StationStatus.Ng
                   : idx % 17 == 0 ? StationStatus.NotConnected
                   : StationStatus.Ok;
        return new StationModel
        {
            DeviceName   = code,
            Code         = code,
            Line         = line,
            OperatorId   = $"EMP{idx:000}",
            OperatorName = $"Operator {idx:00}",
            Status       = status,
            LastUpdate   = DateTime.Now,
            Compliance   = status == StationStatus.Ok ? 98.5 : 72.0,
        };
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _cts.Cancel();
        _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts.Dispose();
    }
}
