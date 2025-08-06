using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Route;

namespace BusBuddy.WPF.Views.Route
{
    /// <summary>
    /// Phase 2 Route Management View
    /// Enhanced route planning and management interface
    /// </summary>
    public partial class RouteManagementView : UserControl
    {
        public RouteManagementView()
        {
            InitializeComponent();
            DataContext = new RouteManagementViewModel();
        }
    }
}
