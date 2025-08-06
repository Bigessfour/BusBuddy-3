using System;
using System.Globalization;
using System.Windows.Data;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converter that compares a string value with a parameter and returns true if they are equal
    /// Used for menu checkboxes to indicate current view
    /// </summary>
    public class StringEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {

                return false;
            }


            var stringValue = value.ToString();
            var parameterValue = parameter.ToString();

            return string.Equals(stringValue, parameterValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
}
