using System.Collections.ObjectModel;
using System.Linq;
using ESD.UI.Models;
using LiveCharts;
using LiveCharts.Wpf;

namespace ESD.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel(ObservableCollection<Station> stations)
        {
            Stations = stations;

            // Đăng ký sự kiện cập nhật biểu đồ khi danh sách Stations thay đổi
            Stations.CollectionChanged += (s, e) => UpdateCharts();

            InitCharts();
        }

        public ObservableCollection<Station> Stations { get; }
        public int Total => Stations.Count;
        public int OkCount => Stations.Count(s => s.Status == StationStatus.Ok);
        public int NgCount => Stations.Count(s => s.Status == StationStatus.Ng);
        public int NotConnectedCount => Stations.Count(s => s.Status == StationStatus.NotConnected);
        public int OfflineCount => Stations.Count(s => s.Status == StationStatus.Offline);
        public double Compliance => Total == 0 ? 0 : OkCount * 100.0 / Total;

        // --- DỮ LIỆU CHO BIỂU ĐỒ ---
        public SeriesCollection TrendSeries { get; set; }
        public string[] TrendLabels { get; set; }

        public SeriesCollection StatusPieSeries { get; set; }

        private void InitCharts()
        {
            // 1. Biểu đồ Trend Over Time (Ví dụ dữ liệu 6 tháng gần nhất)
            TrendLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
            TrendSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "OK Count",
                    Values = new ChartValues<int> { 45, 48, 47, 49, 46, OkCount },
                    Fill = System.Windows.Media.Brushes.MediumSeaGreen
                },
                new ColumnSeries
                {
                    Title = "NG Count",
                    Values = new ChartValues<int> { 5, 2, 3, 1, 4, NgCount },
                    Fill = System.Windows.Media.Brushes.IndianRed
                }
            };

            // 2. Biểu đồ Pie/Donut Breakdown Status
            StatusPieSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "OK",
                    Values = new ChartValues<int> { OkCount },
                    Fill = System.Windows.Media.Brushes.MediumSeaGreen,
                    DataLabels = true
                },
                new PieSeries
                {
                    Title = "NG",
                    Values = new ChartValues<int> { NgCount },
                    Fill = System.Windows.Media.Brushes.IndianRed,
                    DataLabels = true
                },
                new PieSeries
                {
                    Title = "Disconnected",
                    Values = new ChartValues<int> { NotConnectedCount },
                    Fill = System.Windows.Media.Brushes.Orange,
                    DataLabels = true
                },
                new PieSeries
                {
                    Title = "Offline",
                    Values = new ChartValues<int> { OfflineCount },
                    Fill = System.Windows.Media.Brushes.Gray,
                    DataLabels = true
                }
            };
        }

        private void UpdateCharts()
        {
            // Cập nhật lại giá trị realtime khi danh sách station thay đổi
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(OkCount));
            OnPropertyChanged(nameof(NgCount));
            OnPropertyChanged(nameof(NotConnectedCount));
            OnPropertyChanged(nameof(OfflineCount));
            OnPropertyChanged(nameof(Compliance));

            // Cập nhật giá trị biểu đồ Pie
            if (StatusPieSeries != null && StatusPieSeries.Count >= 4)
            {
                StatusPieSeries[0].Values[0] = OkCount;
                StatusPieSeries[1].Values[0] = NgCount;
                StatusPieSeries[2].Values[0] = NotConnectedCount;
                StatusPieSeries[3].Values[0] = OfflineCount;
            }
        }
    }
}