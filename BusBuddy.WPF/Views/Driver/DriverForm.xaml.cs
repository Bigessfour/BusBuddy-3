using System.Windows;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.ViewModels.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverForm.xaml
    /// MVP-ready driver entry form with ChromelessWindow and SkinManager theming
    /// </summary>
    public partial class DriverForm : ChromelessWindow
    {
        public DriverForm()
        {
            InitializeComponent();

            // Apply Syncfusion theme — FluentDark default, FluentWhite fallback
            ApplySyncfusionTheme();

            // Set DataContext to DriverFormViewModel using DI
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DriverFormViewModel>();
            }
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
