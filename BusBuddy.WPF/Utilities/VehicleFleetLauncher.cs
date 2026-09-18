using System.Windows;
using BusBuddy.WPF.ViewModels.Vehicle;
using BusBuddy.WPF.Views.Vehicle;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Fleet CRUD chrome. Persist type is <c>BusBuddy.Core.Models.Bus</c> (specs/buses.md).
/// Window filenames still say Vehicle; do not introduce a Vehicle entity.
/// </summary>
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
