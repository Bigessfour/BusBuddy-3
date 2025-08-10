using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Activity;
using Serilog;

namespace BusBuddy.WPF.Views.Activity
{
    /// <summary>
    /// Interaction logic for ActivityManagementView.xaml
    /// Activity management functionality - Coming in Phase 2
    /// </summary>
    public partial class ActivityManagementView : UserControl
    {
        public ActivityManagementView()
        {
            InitializeComponent();
            // Set DataContext to ensure bindings work even before full feature implementation
            if (DataContext is null)
            {
                DataContext = new ActivityManagementViewModel();
                Log.ForContext<ActivityManagementView>().Information("ActivityManagementView DataContext initialized");
            }
        }
    }
}
