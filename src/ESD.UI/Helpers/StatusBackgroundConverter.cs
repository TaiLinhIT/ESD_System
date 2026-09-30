using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ESD.UI.Models;

namespace ESD.UI.Helpers;

public class StatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            StationStatus.Ok => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            StationStatus.Ng => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            StationStatus.NotConnected => new SolidColorBrush(Color.FromRgb(234, 138, 0)),
            _ => new SolidColorBrush(Color.FromRgb(71, 85, 105))
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
