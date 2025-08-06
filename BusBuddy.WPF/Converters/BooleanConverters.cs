using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converts a Boolean value to a Visibility value.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets a value indicating whether the conversion should be inverted.
        /// </summary>
        public bool IsInverted { get; set; }        /// <summary>
                                                    /// Converts a Boolean value to a Visibility value.
                                                    /// </summary>
                                                    /// <param name="value">The Boolean value to convert.</param>
                                                    /// <param name="targetType">The type of the binding target property.</param>
                                                    /// <param name="parameter">The converter parameter to use.</param>
                                                    /// <param name="culture">The culture to use in the converter.</param>
                                                    /// <returns>Visibility.Visible if value is true; otherwise Visibility.Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool result = IsInverted ? !boolValue : boolValue;
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts a Visibility value to a Boolean value.
        /// </summary>
        /// <param name="value">The Visibility value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>true if value is Visibility.Visible; otherwise false.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return IsInverted ? !result : result;
            }
            return false;
        }
    }
}
