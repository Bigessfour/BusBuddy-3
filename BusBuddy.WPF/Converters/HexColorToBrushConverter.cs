using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BusBuddy.WPF.Converters;

/// <summary>Legend swatches bind hex tokens from <see cref="Utilities.MapMarkerLabels"/>.</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return Brushes.Transparent;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
