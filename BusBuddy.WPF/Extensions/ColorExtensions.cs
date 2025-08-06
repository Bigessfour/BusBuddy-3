using BusBuddy.Core.Models;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using CoreColor = BusBuddy.Core.Models.Color;

namespace BusBuddy.WPF.Extensions
{
    /// <summary>
    /// Extension methods for converting between Core Color and WPF Color types
    /// </summary>
    public static class ColorExtensions
    {
        /// <summary>
        /// Converts a Core Color to a WPF Color for use in WPF controls
        /// </summary>
        public static WpfColor ToWpfColor(this CoreColor coreColor)
        {
            return WpfColor.FromArgb(coreColor.A, coreColor.R, coreColor.G, coreColor.B);
        }

        /// <summary>
        /// Creates a Core Color from a WPF Color
        /// </summary>
        public static CoreColor ToCoreColor(this WpfColor wpfColor)
        {
            return new CoreColor(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
        }

        /// <summary>
        /// Converts a Core Color to a WPF SolidColorBrush
        /// </summary>
        public static SolidColorBrush ToSolidColorBrush(this CoreColor coreColor)
        {
            return new SolidColorBrush(coreColor.ToWpfColor());
        }
    }
}
