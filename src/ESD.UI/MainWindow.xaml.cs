using ESD.Core;
using ESD.Service;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ESD.UI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly EsdRuntime _runtime;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _cts;

    public ObservableCollection<EventRow> Events { get; } = new();
    public ObservableCollection<StationRow> Stations { get; } = new();
    public ObservableCollection<TrendBar> TrendBars { get; } = new();

    private int _okCount;
    private int _ngCount;
    private int _activeWorkers;

    public string CurrentDate => DateTime.Now.ToString("yyyy-MM-dd (ddd)");
    public string CurrentTime => DateTime.Now.ToString("HH:mm:ss");
    public string ComplianceText
    {
        get
        {
            var total = _okCount + _ngCount;
            return total == 0 ? "100.0%" : $"{(_okCount * 100.0 / total):F1}%";
        }
    }
    public int ActiveWorkers => _activeWorkers;
    public int OkCount => _okCount;
    public int NgCount => _ngCount;
    public string ConnectedStationsText =>
        $"{Stations.Count(x => x.ConnectionStatus == "Connected")} / {Stations.Count}";
    public string EventCountText => $"Total Records: {Events.Count}";
    public string DbQueueText => $"DB Queue: {_runtime.DbWorker.QueueCount}";
    public string SettingsSummary =>
        $"Devices: {_runtime.Settings.Devices.Count} | " +
        $"Database: {_runtime.Settings.DatabaseConnectionString.Split(';').FirstOrDefault() ?? "Configured"}";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
        _runtime = new EsdRuntime(configPath);
        _runtime.Events.EventReceived += OnEvent;

        BuildStations();
        BuildTrend();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => RefreshUi();
        _timer.Start();

        Loaded += async (_, _) =>
        {
            _cts = new CancellationTokenSource();
            try
            {
                await _runtime.StartAsync(_cts.Token);
                RefreshUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ESD Runtime Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        Closing += (_, _) =>
        {
            _timer.Stop();
            _cts?.Cancel();
            _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        };
    }

    private void BuildStations()
    {
        foreach (var d in _runtime.Devices.Devices)
        {
            var setting = _runtime.Settings.Devices.FirstOrDefault(
                x => x.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));

            Stations.Add(new StationRow
            {
                Name = d.Name,
                Port = setting?.PortName ?? "-",
                Transport = setting?.Transport ?? "-",
                Online = d.IsConnected,
                LastMessage = "Waiting for device..."
            });
        }
    }

    private void BuildTrend()
    {
        var values = new[] { 18, 25, 22, 31, 28, 35, 40, 38, 44, 48, 46, 52 };
        var max = values.Max();
        foreach (var (value, index) in values.Select((v, i) => (v, i)))
        {
            TrendBars.Add(new TrendBar
            {
                Label = DateTime.Now.AddHours(index - values.Length + 1).ToString("HH:mm"),
                Value = value.ToString(),
                BarHeight = 18 + (value / (double)max) * 90
            });
        }
    }

    private void OnEvent(object? sender, EsdEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            var row = new EventRow(e, Events.Count + 1);
            Events.Insert(0, row);
            if (Events.Count > 250)
                Events.RemoveAt(Events.Count - 1);

            if (e.Status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                _okCount++;
            else if (e.Status.Equals("NG", StringComparison.OrdinalIgnoreCase))
                _ngCount++;

            if (!string.IsNullOrWhiteSpace(e.EmployeeId) &&
                !e.EmployeeId.Equals("-", StringComparison.OrdinalIgnoreCase))
            {
                _activeWorkers = Events.Select(x => x.EmployeeId)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
            }

            var station = Stations.FirstOrDefault(x =>
                x.Name.Equals(e.Device, StringComparison.OrdinalIgnoreCase));
            if (station != null)
            {
                station.LastMessage = e.Message;
                station.Online = true;
            }

            NotifyAll();
        });
    }

    private void RefreshUi()
    {
        NotifyAll();

        foreach (var d in _runtime.Devices.Devices)
        {
            var row = Stations.FirstOrDefault(x => x.Name == d.Name);
            if (row != null)
            {
                row.Online = d.IsConnected;
                row.LastMessage = d.IsConnected ? "Connected" : "Disconnected";
            }
        }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(CurrentDate));
        OnPropertyChanged(nameof(CurrentTime));
        OnPropertyChanged(nameof(ComplianceText));
        OnPropertyChanged(nameof(ActiveWorkers));
        OnPropertyChanged(nameof(OkCount));
        OnPropertyChanged(nameof(NgCount));
        OnPropertyChanged(nameof(ConnectedStationsText));
        OnPropertyChanged(nameof(EventCountText));
        OnPropertyChanged(nameof(DbQueueText));
        OnPropertyChanged(nameof(SettingsSummary));
    }

    private void ShowOnly(UIElement view, string title, string subtitle)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        HistoryView.Visibility = Visibility.Collapsed;
        DevicesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;

        view.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) =>
        ShowOnly(DashboardView, "DASHBOARD", "ESD compliance and wrist strap monitoring");

    private void History_Click(object sender, RoutedEventArgs e) =>
        ShowOnly(HistoryView, "WRIST STRAP USAGE HISTORY", "Search, review and export worker ESD activity");

    private void Devices_Click(object sender, RoutedEventArgs e) =>
        ShowOnly(DevicesView, "DEVICES / STATIONS", "Live RS232 / RS485 device connection status");

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        ShowOnly(SettingsView, "SETTINGS", "ESD system configuration and database");

    private void Search_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Search filters are ready for database query integration.",
            "Search", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Export_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Excel export can be connected to the history repository.",
            "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Print_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Print layout can be connected to the selected history records.",
            "Print", MessageBoxButton.OK, MessageBoxImage.Information);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class EventRow
{
    private static readonly Brush OkBrush = CreateFrozenBrush(22, 167, 101);
    private static readonly Brush NgBrush = CreateFrozenBrush(229, 57, 53);
    private static readonly Brush WarnBrush = CreateFrozenBrush(245, 158, 11);

    public int Number { get; }
    public string DateText { get; }
    public string TimeText { get; }
    public string Device { get; }
    public string EmployeeId { get; }
    public string EventType { get; }
    public string Status { get; }
    public string OldStatus { get; }
    public string Message { get; }

    public Brush StatusBackground =>
        Status.Equals("OK", StringComparison.OrdinalIgnoreCase) ? OkBrush :
        Status.Equals("NG", StringComparison.OrdinalIgnoreCase) ? NgBrush : WarnBrush;

    public EventRow(EsdEvent e, int number)
    {
        Number = number;
        DateText = e.Timestamp.ToString("yyyy-MM-dd");
        TimeText = e.Timestamp.ToString("HH:mm:ss");
        Device = e.Device;
        EmployeeId = string.IsNullOrWhiteSpace(e.EmployeeId) ? "-" : e.EmployeeId;
        EventType = e.EventType;
        Status = string.IsNullOrWhiteSpace(e.Status) ? "-" : e.Status;
        OldStatus = "-";
        Message = e.Message;
    }

    private static Brush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class StationRow : INotifyPropertyChanged
{
    private static readonly Brush OnlineBrush = CreateFrozenBrush(22, 167, 101);
    private static readonly Brush OfflineBrush = CreateFrozenBrush(100, 116, 139);

    public string Name { get; set; } = "";
    public string Port { get; set; } = "";
    public string Transport { get; set; } = "";
    public string LastMessage { get; set; } = "";

    private bool _online;
    public bool Online
    {
        get => _online;
        set
        {
            if (_online == value) return;
            _online = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ConnectionBackground));
        }
    }

    public string ConnectionStatus => Online ? "Connected" : "Offline";
    public Brush ConnectionBackground => Online ? OnlineBrush : OfflineBrush;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private static Brush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class TrendBar
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public double BarHeight { get; set; }
}