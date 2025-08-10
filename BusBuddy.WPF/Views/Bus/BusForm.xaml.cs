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
        /// Apply Syncfusion theme with FluentDark default and FluentLight fallback
        /// </summary>
        private void ApplySyncfusionTheme()
        {
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            try
            {
                using var fluentDarkTheme = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, fluentDarkTheme);
                Serilog.Log.Information("FluentDark theme applied to {ViewName}", GetType().Name);
            }
            catch
            {
                try
                {
                    using var fluentLightTheme = new Theme("FluentLight");
                    SfSkinManager.SetTheme(this, fluentLightTheme);
                    Serilog.Log.Information("Fallback to FluentLight theme for {ViewName}", GetType().Name);
                }
                catch
                {
                    // Continue without theme if both fail
                }
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                SfSkinManager.Dispose(this);
                Serilog.Log.Information("SfSkinManager resources disposed for {ViewName}", GetType().Name);
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Error("Error disposing SfSkinManager for {ViewName}: {Error}", GetType().Name, ex.Message);
            }
            base.OnClosed(e);
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
