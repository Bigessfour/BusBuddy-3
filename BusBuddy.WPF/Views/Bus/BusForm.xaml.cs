using System.Windows;
using Syncfusion.Windows.Shared;
using Microsoft.Extensions.DependencyInjection;
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
                // Resolve from DI if available, fallback to parameterless
                if (App.ServiceProvider != null)
                {
                    var vm = App.ServiceProvider.GetService<BusFormViewModel>() ?? new BusFormViewModel();
                    DataContext = vm;
                    vm.RequestClose += (_, result) =>
                    {
                        DialogResult = result;
                        Close();
                    };
                }
                else
                {
                    DataContext = new BusFormViewModel();
                }
        }

        public BusForm(BusBuddy.Core.Models.Bus bus)
        {
            InitializeComponent();
            ApplySyncfusionTheme();
            if (App.ServiceProvider != null)
            {
                var vm = App.ServiceProvider.GetService<BusBuddy.WPF.ViewModels.Bus.BusFormViewModel>();
                if (vm != null)
                {
                    // Replace backing bus through reflection-free assignment
                    // Simpler: create new instance with service + bus
                    var busService = App.ServiceProvider.GetService<BusBuddy.Core.Services.Interfaces.IBusService>();
                    vm = new BusBuddy.WPF.ViewModels.Bus.BusFormViewModel(busService, bus);
                    DataContext = vm;
                    vm.RequestClose += (_, result) =>
                    {
                        DialogResult = result;
                        Close();
                    };
                }
                else
                {
                    DataContext = new BusBuddy.WPF.ViewModels.Bus.BusFormViewModel(null, bus);
                }
            }
            else
            {
                DataContext = new BusBuddy.WPF.ViewModels.Bus.BusFormViewModel(null, bus);
            }
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
    }
}
