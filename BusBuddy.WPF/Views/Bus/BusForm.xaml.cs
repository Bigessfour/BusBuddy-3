using System.Windows;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.ViewModels.Bus;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusForm.xaml
    /// MVP-ready bus entry form with Syncfusion ChromelessWindow and SkinManager theming
    /// </summary>
    public partial class BusForm : ChromelessWindow
    {
        public BusForm()
        {
            InitializeComponent();
            ApplySyncfusionTheme();
            DataContext = new BusFormViewModel();
        }

        public BusForm(BusBuddy.Core.Models.Bus bus)
        {
            InitializeComponent();
            ApplySyncfusionTheme();
            DataContext = new BusFormViewModel(bus);
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
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
