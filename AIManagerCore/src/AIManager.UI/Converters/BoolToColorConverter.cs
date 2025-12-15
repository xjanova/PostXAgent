using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIManager.UI.Converters;

/// <summary>
/// Converts boolean to color (green for true, red for false)
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
        }
        return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
