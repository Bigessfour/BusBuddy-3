using System;
using System.Windows;
using System.Windows.Markup;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Custom markup extension to safely handle star width values in XAML.
    /// This prevents the "* is not a valid value for Double" error in Syncfusion templates.
    /// </summary>
    public class SafeStarWidthExtension : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new GridLength(1, GridUnitType.Star);
        }
    }
}
