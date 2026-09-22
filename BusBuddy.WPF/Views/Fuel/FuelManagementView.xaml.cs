using System;
using System.Windows.Controls;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Fuel;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Fuel
{
    public partial class FuelManagementView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<FuelManagementView>();

        public FuelManagementView()
        {
            InitializeComponent();

            try
            {
                if (App.ServiceProvider is null)
                {
                    Logger.Warning("FuelManagementView opened before ServiceProvider was ready");
                    return;
                }

                var fuelService = App.ServiceProvider.GetRequiredService<IFuelService>();
                var busService = App.ServiceProvider.GetRequiredService<IBusService>();
                var locationCatalog = App.ServiceProvider.GetService<IFuelLocationCatalog>();
                var userSettings = App.ServiceProvider.GetService<IUserSettingsService>();
                DataContext = new FuelManagementViewModel(fuelService, busService, locationCatalog, userSettings);
                Logger.Information("FuelManagementView DataContext set");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize FuelManagementViewModel");
            }
        }

        private void FuelManagementView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            NumpadInputHelper.HandlePreviewKeyDown(e);
        }
    }
}
