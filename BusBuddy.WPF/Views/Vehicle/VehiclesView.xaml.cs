using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Vehicle;
using Serilog;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// UserControl host for fleet CRUD. VehicleForm / VehicleFleetLauncher always open this, which hosts VehicleManagementView.
    /// </summary>
    public partial class VehiclesView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<VehiclesView>();

        public VehiclesView() : this(VehicleManagementStartup.None)
        {
        }

        public VehiclesView(VehicleManagementStartup startup)
        {
            InitializeComponent();
            Host.Children.Add(new VehicleManagementView(startup));
            Logger.Information("VehiclesView host constructed Startup={Startup}", startup);
        }
    }
}
