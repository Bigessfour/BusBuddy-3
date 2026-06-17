using System;
using System.Windows.Controls;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
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
                var fuelService = App.ServiceProvider.GetRequiredService<IFuelService>();
                var busService = App.ServiceProvider.GetRequiredService<IBusService>();
                DataContext = new FuelManagementViewModel(fuelService, busService);
                Logger.Information("FuelManagementView DataContext set");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize FuelManagementViewModel");
            }
        }
    }
}
