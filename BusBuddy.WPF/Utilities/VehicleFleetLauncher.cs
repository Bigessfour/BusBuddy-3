using System.Windows;
using BusBuddy.WPF.ViewModels.Vehicle;
using BusBuddy.WPF.Views.Vehicle;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>Single entry point for fleet CRUD — always opens <see cref="VehicleForm"/> hosting <see cref="VehicleManagementView"/>.</summary>
public static class VehicleFleetLauncher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(VehicleFleetLauncher));

    public static bool? ShowDialog(Window? owner, VehicleManagementStartup startup = VehicleManagementStartup.None)
    {
        Logger.Information("Opening vehicle fleet dialog Startup={Startup}", startup);
        var form = new VehicleForm(startup)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return form.ShowDialog();
    }

    public static void Show(Window? owner, VehicleManagementStartup startup = VehicleManagementStartup.None)
    {
        Logger.Information("Opening vehicle fleet window Startup={Startup}", startup);
        var form = new VehicleForm(startup)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        form.Show();
    }
}
