using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Vehicle;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehiclesView.xaml
    /// Phase 1: Simple ViewModel binding
    /// </summary>
    public partial class VehiclesView : UserControl
    {
        public VehiclesView()
        {
            InitializeComponent();

            // Phase 1: Direct ViewModel assignment
            DataContext = new VehiclesViewModel();
        }
    }
}
