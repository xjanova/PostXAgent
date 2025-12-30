using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AIManager.UI.Converters;

/// <summary>
/// Converts boolean to Visibility (true = Visible, false = Collapsed)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts success rate to color (green > 80, yellow > 50, red < 50)
/// </summary>
public class SuccessRateColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double rate)
        {
            if (rate >= 80)
                return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            if (rate >= 50)
                return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow/Amber
            return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to status text (Success/Failed)
/// </summary>
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool success)
        {
            return success ? "Success" : "Failed";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts worker state string to color
/// </summary>
public class WorkerStateColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string state)
        {
            return state.ToLower() switch
            {
                "running" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                "paused" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),    // Yellow
                "stopped" => new SolidColorBrush(Color.FromRgb(66, 66, 66)),    // Dark Gray
                "error" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),     // Red
                "created" => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts platform name to icon color
/// </summary>
public class PlatformColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string platform)
        {
            return platform.ToLower() switch
            {
                "facebook" => new SolidColorBrush(Color.FromRgb(24, 119, 242)),   // Facebook Blue
                "instagram" => new SolidColorBrush(Color.FromRgb(225, 48, 108)),  // Instagram Pink
                "tiktok" => new SolidColorBrush(Color.FromRgb(0, 0, 0)),          // TikTok Black
                "twitter" => new SolidColorBrush(Color.FromRgb(29, 161, 242)),    // Twitter Blue
                "line" => new SolidColorBrush(Color.FromRgb(0, 185, 0)),          // LINE Green
                "youtube" => new SolidColorBrush(Color.FromRgb(255, 0, 0)),       // YouTube Red
                "linkedin" => new SolidColorBrush(Color.FromRgb(0, 119, 181)),    // LinkedIn Blue
                "pinterest" => new SolidColorBrush(Color.FromRgb(189, 8, 28)),    // Pinterest Red
                "threads" => new SolidColorBrush(Color.FromRgb(0, 0, 0)),         // Threads Black
                _ => new SolidColorBrush(Color.FromRgb(25, 118, 210))             // Default Blue
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
