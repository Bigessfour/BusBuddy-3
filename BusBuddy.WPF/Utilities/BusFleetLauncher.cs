using System.Windows;
using BusBuddy.WPF.ViewModels.Bus;
using BusBuddy.WPF.Views.Bus;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Fleet CRUD chrome. Persist type is <c>BusBuddy.Core.Models.Bus</c> (specs/buses.md).
/// Opens <see cref="Views.Bus.BusFleetWindow"/>; do not introduce a Vehicle entity.
/// </summary>
public static class BusFleetLauncher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(BusFleetLauncher));

    public static bool? ShowDialog(Window? owner, BusManagementStartup startup = BusManagementStartup.None)
    {
        Logger.Information("Opening bus fleet dialog Startup={Startup}", startup);
        var form = new BusFleetWindow(startup)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return form.ShowDialog();
    }

    public static void Show(Window? owner, BusManagementStartup startup = BusManagementStartup.None)
    {
        Logger.Information("Opening bus fleet window Startup={Startup}", startup);
        var form = new BusFleetWindow(startup)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        form.Show();
    }
}
