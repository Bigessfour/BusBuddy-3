using System.Windows.Controls;
using BusBuddy.WPF.Utilities;
using System.Windows;

namespace BusBuddy.WPF.Views
{
    /// <summary>
    /// Interaction logic for DriverManagementView.xaml
    /// MVP Phase 1: Stub implementation with basic event handlers
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
