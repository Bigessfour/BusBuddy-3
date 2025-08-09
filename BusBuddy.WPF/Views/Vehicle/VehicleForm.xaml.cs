using System.Windows;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehicleForm.xaml
    /// Step 3: Vehicle form with Syncfusion ChromelessWindow, SkinManager theming, and Core service integration
    /// </summary>
    public partial class VehicleForm : ChromelessWindow
    {
        public VehicleForm()
        {
            InitializeComponent();
            ApplySyncfusionTheme();
        }

        /// <summary>
        /// Apply Syncfusion theme with FluentDark default and FluentWhite fallback
        /// </summary>
        private void ApplySyncfusionTheme()
        {
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            try
            {
                using var fluentDarkTheme = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, fluentDarkTheme);
            }
            catch
            {
                try
                {
                    using var fluentWhiteTheme = new Theme("FluentWhite");
                    SfSkinManager.SetTheme(this, fluentWhiteTheme);
                }
                catch
                {
                    // Continue without theme if both fail
                }
            }
        }
    }
}
