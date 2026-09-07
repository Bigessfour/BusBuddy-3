using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Syncfusion.Windows.Shared;
using Syncfusion.UI.Xaml.Grid;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Converts active status boolean to Syncfusion color brush
    /// </summary>
    public class ActiveStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // Using Syncfusion's predefined color scheme
                return isActive ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : // Success Green
                                 new SolidColorBrush(Color.FromRgb(255, 152, 0));   // Warning Orange
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Neutral Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts active status boolean to text with Syncfusion localization support
    /// </summary>
    public class ActiveStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // Using proper localization for Syncfusion controls
                return isActive ? "Active" : "Inactive";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text.Equals("Active", StringComparison.OrdinalIgnoreCase);
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts efficiency score to Syncfusion-compatible color brush for data visualization
    /// </summary>
    public class EfficiencyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double efficiency)
            {
                // Syncfusion chart color palette
                if (efficiency >= 90)
                {
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80));   // Material Green
                }

                if (efficiency >= 70)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 235, 59));  // Material Yellow
                }


                if (efficiency >= 50)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 152, 0));   // Material Orange
                }


                return new SolidColorBrush(Color.FromRgb(244, 67, 54));                        // Material Red
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Material Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts license status string to color brush
    /// </summary>
    public class LicenseStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLowerInvariant() switch
                {
                    "valid" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),      // Green
                    "expiring" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),   // Yellow
                    "expired" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),    // Red
                    "suspended" => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts general status string to Syncfusion theme-compatible color brush
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLowerInvariant() switch
                {
                    "active" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),      // Material Green
                    "inactive" => new SolidColorBrush(Color.FromRgb(158, 158, 158)),  // Material Gray
                    "pending" => new SolidColorBrush(Color.FromRgb(255, 235, 59)),    // Material Yellow
                    "maintenance" => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Material Orange
                    "out of service" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Material Red
                    "available" => new SolidColorBrush(Color.FromRgb(139, 195, 74)),  // Light Green
                    "assigned" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),   // Material Blue
                    _ => new SolidColorBrush(Color.FromRgb(96, 125, 139))             // Blue Gray
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts string to Syncfusion visibility (for SfDataGrid and other Syncfusion controls)
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts availability boolean to Syncfusion color scheme
    /// </summary>
    public class AvailabilityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAvailable)
            {
                return isAvailable ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) :  // Material Green
                                    new SolidColorBrush(Color.FromRgb(244, 67, 54));     // Material Red
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Material Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
