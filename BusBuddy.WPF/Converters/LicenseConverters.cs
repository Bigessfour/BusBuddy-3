using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BusBuddy.WPF.Converters
{
    /// <summary>
    /// Checks if a license has expired
    /// </summary>
    public class LicenseExpiredConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime expiryDate)
            {
                return expiryDate < DateTime.Now;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Checks if a license is expiring soon (within 30 days)
    /// </summary>
    public class LicenseExpiringSoonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime expiryDate)
            {
                var daysUntilExpiry = (expiryDate - DateTime.Now).TotalDays;
                return daysUntilExpiry > 0 && daysUntilExpiry <= 30;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts days remaining to appropriate color
    /// </summary>
    public class DaysRemainingToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int daysRemaining)
            {
                if (daysRemaining < 0)
                {
                    return new SolidColorBrush(Color.FromRgb(244, 67, 54));   // Red - Expired
                }

                if (daysRemaining <= 7)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 152, 0));  // Orange - Critical
                }


                if (daysRemaining <= 30)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow - Warning
                }


                return new SolidColorBrush(Color.FromRgb(46, 125, 50));                         // Green - Good
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
