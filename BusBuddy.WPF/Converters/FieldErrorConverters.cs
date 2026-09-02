using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BusBuddy.WPF.Converters;

/// <summary>Red border when <c>FieldErrors</c> contains the converter parameter key.</summary>
public sealed class FieldErrorBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not IReadOnlyDictionary<string, string> errors
            || values[1] is not string fieldKey
            || string.IsNullOrWhiteSpace(fieldKey)
            || !errors.ContainsKey(fieldKey))
        {
            return DependencyProperty.UnsetValue;
        }

        return Application.Current.TryFindResource("BusBuddy.Brush.Semantic.Error") as Brush
               ?? Brushes.IndianRed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inline message for a field validation error.</summary>
public sealed class FieldErrorMessageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not IReadOnlyDictionary<string, string> errors
            || values[1] is not string fieldKey
            || !errors.TryGetValue(fieldKey, out var message))
        {
            return string.Empty;
        }

        return message;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Thicker border when a field has a validation error.</summary>
public sealed class FieldErrorThicknessConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not IReadOnlyDictionary<string, string> errors
            || values[1] is not string fieldKey
            || !errors.ContainsKey(fieldKey))
        {
            return new Thickness(1);
        }

        return new Thickness(2);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
