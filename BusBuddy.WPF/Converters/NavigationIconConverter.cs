using System;
using System.Globalization;
using System.Windows.Data;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converter to provide navigation icons based on view model names
    /// </summary>
    public class NavigationIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string viewModelName)
            {
                return viewModelName switch
                {
                    "Dashboard" => "\uE80F", // Home icon
                    "Buses" => "\uE7F4", // Bus/Vehicle icon
                    "Drivers" => "\uE77B", // People icon
                    "Routes" => "\uE8B7", // Map icon
                    "Schedule" => "\uE787", // Calendar icon
                    "Students" => "\uE716", // Contact icon
                    "Maintenance" => "\uE90F", // Repair icon
                    "Fuel" => "\uE7B7", // Gas pump icon
                    "Activity" => "\uE81C", // Activity icon
                    "Settings" => "\uE713", // Settings icon
                    "StudentList" => "\uE8FD", // List icon
                    _ => "\uE8B9" // Default icon
                };
            }
            return "\uE8B9"; // Default icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
