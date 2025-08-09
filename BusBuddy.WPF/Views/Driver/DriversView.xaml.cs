using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Driver;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Enhanced Drivers View with proper ViewModel integration
    /// </summary>
    public partial class DriversView : UserControl
    {
        public DriversView()
        {
            InitializeComponent();

            // Set the ViewModel for data binding
            DataContext = new DriversViewModel();

            // Apply Syncfusion theme
            SyncfusionThemeManager.ApplyTheme(this);
        }
    }
}
