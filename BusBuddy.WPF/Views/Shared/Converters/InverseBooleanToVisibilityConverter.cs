using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BusBuddy.WPF.Views.Shared.Converters
{
	// InverseBooleanToVisibilityConverter — maps true => Collapsed, false => Visible.
	// Official docs: IValueConverter — https://learn.microsoft.com/dotnet/api/system.windows.data.ivalueconverter
	// Suppress CA1716 (namespace contains 'Shared') as renaming the namespace is a larger repo refactor;
	// this local suppression keeps analysis clean while preserving existing project structure.
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Naming",
		"CA1716:Identifiers should not match keywords",
		Justification = "Namespace uses 'Shared' for logical grouping; renaming would require broad changes.")]
	public class InverseBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool flag)
			{
				return flag ? Visibility.Collapsed : Visibility.Visible;
			}
			// If no boolean provided, default to Visible to avoid hiding UI unexpectedly.
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Visibility v)
			{
				return v != Visibility.Visible;
			}
			return Binding.DoNothing;
		}
	}
}
