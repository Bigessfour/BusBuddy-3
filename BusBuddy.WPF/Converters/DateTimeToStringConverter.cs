using System;
using System.Globalization;
using System.Windows.Data;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converts DateTime values to string representation for UI binding and vice versa
    /// Handles null DateTime values gracefully
    /// </summary>
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
                return dateTime.ToString("MM/dd/yyyy", culture);

            if (value != null && value.GetType() == typeof(DateTime?))
            {
                var nullableDateTime = (DateTime?)value;
                if (nullableDateTime.HasValue)
                    return nullableDateTime.Value.ToString("MM/dd/yyyy", culture);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                if (DateTime.TryParse(str, culture, DateTimeStyles.None, out var dateTime))
                {
                    // Return DateTime? if target type is nullable
                    if (targetType == typeof(DateTime?))
                        return (DateTime?)dateTime;
                    return dateTime;
                }
            }

            // Return null for nullable DateTime, or do nothing for regular DateTime
            if (targetType == typeof(DateTime?))
                return null;

            return Binding.DoNothing;
        }
    }
}
