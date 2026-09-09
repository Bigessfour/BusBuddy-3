using System.Windows.Controls;
using Serilog;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Thin host so leftover navigation still opens fleet CRUD (VehicleManagementView).
    /// </summary>
    public partial class VehiclesView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<VehiclesView>();

        public VehiclesView()
        {
            InitializeComponent();
            Logger.Information("VehiclesView host constructed — child VehicleManagementView owns DataContext");
        }
    }
}
