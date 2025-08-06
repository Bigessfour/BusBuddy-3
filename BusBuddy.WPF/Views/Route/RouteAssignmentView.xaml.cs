using System;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Route;
using Serilog;

namespace BusBuddy.WPF.Views.Route
{
    /// <summary>
    /// Interaction logic for RouteAssignmentView.xaml
    /// MVP-ready route assignment interface with drag-drop functionality
    /// Supports comprehensive route building workflow with Syncfusion integration
    /// </summary>
    public partial class RouteAssignmentView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<RouteAssignmentView>();

        public RouteAssignmentView()
        {
            Logger.Debug("RouteAssignmentView constructor starting");
            try
            {
                Logger.Debug("Initializing RouteAssignmentView XAML components");
                InitializeComponent();

                Logger.Debug("Setting DataContext to RouteAssignmentViewModel");
                DataContext = new RouteAssignmentViewModel();

                Logger.Information("RouteAssignmentView initialized successfully with MVP route building interface");
                Logger.Debug("RouteAssignmentView constructor completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize RouteAssignmentView - Critical MVP component failure");
                throw; // Re-throw to maintain application stability
            }
        }
    }
}
