using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converts schedule status string to color brush for visual representation
    /// </summary>
    public class ScheduleStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Scheduled" => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    "InProgress" => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    "Completed" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    "Cancelled" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    "Delayed" => new SolidColorBrush(Color.FromRgb(255, 235, 59)), // Yellow
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)) // Gray
                };
            }

            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
