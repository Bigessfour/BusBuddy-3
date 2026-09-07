using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace BusBuddy.WPF
{
    public class DebugConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            Debug.WriteLine($"Binding value: {value}, Type: {value?.GetType().Name ?? "null"}, Target Type: {targetType.Name}");
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
