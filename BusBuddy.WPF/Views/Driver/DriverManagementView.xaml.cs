using System.Windows.Controls;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverManagementView.xaml
    /// Driver management functionality - Coming in Phase 2
    /// </summary>
    public partial class DriverManagementView : UserControl
    {
        public DriverManagementView()
        {
            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);
        }
    }
}
