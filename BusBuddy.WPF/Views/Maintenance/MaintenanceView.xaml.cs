using System;
using System.Windows.Controls;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Maintenance;

public partial class MaintenanceView : UserControl
{
    private static readonly ILogger Logger = Log.ForContext<MaintenanceView>();

    public MaintenanceView()
    {
        InitializeComponent();

        try
        {
            if (App.ServiceProvider is null)
            {
                Logger.Warning("MaintenanceView opened before ServiceProvider was ready");
                return;
            }

            var maintenanceService = App.ServiceProvider.GetRequiredService<IMaintenanceService>();
            var busService = App.ServiceProvider.GetRequiredService<IBusService>();
            DataContext = new MaintenanceViewModel(maintenanceService, busService);
            Logger.Information("MaintenanceView DataContext set");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize MaintenanceViewModel");
        }
    }
}
