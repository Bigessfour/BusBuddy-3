using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converts a boolean value to a brush - Red for deprecated items, Black for normal items.
    /// </summary>
    public class BooleanToDeprecatedForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDeprecated && isDeprecated)
            {
                return new SolidColorBrush(Colors.Red);
            }

            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
