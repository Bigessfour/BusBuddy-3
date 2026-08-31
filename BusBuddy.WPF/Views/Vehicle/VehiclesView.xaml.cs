using System.Windows.Controls;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Thin host so leftover navigation still opens fleet CRUD (VehicleManagementView).
    /// </summary>
    public partial class VehiclesView : UserControl
    {
        public VehiclesView()
        {
            InitializeComponent();
        }
    }
}
