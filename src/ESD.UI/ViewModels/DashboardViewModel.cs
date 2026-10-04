using System.Collections.ObjectModel;
using ESD.UI.Models;
using LiveCharts;
using LiveCharts.Wpf;

namespace ESD.UI.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    public ObservableCollection<StationModel> Stations { get; }

    public int    Total            => Stations.Count;
    public int    OkCount          => Stations.Count(s => s.Status == StationStatus.Ok);
    public int    NgCount          => Stations.Count(s => s.Status == StationStatus.Ng);
    public int    NotConnectedCount => Stations.Count(s => s.Status == StationStatus.NotConnected);
    public int    OfflineCount     => Stations.Count(s => s.Status == StationStatus.Offline);
    public double Compliance       => Total == 0 ? 0 : OkCount * 100.0 / Total;

    public SeriesCollection TrendSeries { get; }
    public string[]         TrendLabels { get; }
    public SeriesCollection StatusPieSeries { get; }

    public DashboardViewModel(ObservableCollection<StationModel> stations)
    {
        Stations = stations;
        Stations.CollectionChanged += (_, _) => RefreshKpis();

        TrendLabels  = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"];
        TrendSeries  = new SeriesCollection
        {
            new ColumnSeries { Title = "OK", Values = new ChartValues<int> { 45, 48, 47, 49, 46, OkCount }, Fill = System.Windows.Media.Brushes.MediumSeaGreen },
            new ColumnSeries { Title = "NG", Values = new ChartValues<int> { 5,  2,  3,  1,  4, NgCount  }, Fill = System.Windows.Media.Brushes.IndianRed },
        };

        StatusPieSeries = new SeriesCollection
        {
            new PieSeries { Title = "OK",           Values = new ChartValues<int> { OkCount           }, Fill = System.Windows.Media.Brushes.MediumSeaGreen, DataLabels = true },
            new PieSeries { Title = "NG",           Values = new ChartValues<int> { NgCount           }, Fill = System.Windows.Media.Brushes.IndianRed,       DataLabels = true },
            new PieSeries { Title = "Disconnected", Values = new ChartValues<int> { NotConnectedCount }, Fill = System.Windows.Media.Brushes.Orange,           DataLabels = true },
            new PieSeries { Title = "Offline",      Values = new ChartValues<int> { OfflineCount      }, Fill = System.Windows.Media.Brushes.Gray,             DataLabels = true },
        };
    }

    private void RefreshKpis()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(OkCount));
        OnPropertyChanged(nameof(NgCount));
        OnPropertyChanged(nameof(NotConnectedCount));
        OnPropertyChanged(nameof(OfflineCount));
        OnPropertyChanged(nameof(Compliance));

        if (StatusPieSeries.Count >= 4)
        {
            ((ChartValues<int>)StatusPieSeries[0].Values)[0] = OkCount;
            ((ChartValues<int>)StatusPieSeries[1].Values)[0] = NgCount;
            ((ChartValues<int>)StatusPieSeries[2].Values)[0] = NotConnectedCount;
            ((ChartValues<int>)StatusPieSeries[3].Values)[0] = OfflineCount;
        }
    }
}
