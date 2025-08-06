using System;
using System.Globalization;
using System.Windows.Data;

namespace BusBuddy.WPF.Converters
{
    public class ThemeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDarkTheme)
            {
                return isDarkTheme ? "Dark Theme" : "Light Theme";
            }
            return "Light Theme";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
