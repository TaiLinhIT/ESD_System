using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ESD.UI.Models;

namespace ESD.UI.Helpers;

public class StatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            StationStatus.Ok           => new SolidColorBrush(Color.FromRgb(22,  163,  74)),
            StationStatus.Ng           => new SolidColorBrush(Color.FromRgb(220,  38,  38)),
            StationStatus.NotConnected => new SolidColorBrush(Color.FromRgb(217, 119,   6)),
            _                          => new SolidColorBrush(Color.FromRgb( 71,  85, 105))
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
