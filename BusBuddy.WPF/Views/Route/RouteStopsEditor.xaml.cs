using System.Windows.Controls;
using System.Windows.Input;
using BusBuddy.WPF.ViewModels.Route;
using Serilog;

namespace BusBuddy.WPF.Views.Route
{
    public partial class RouteStopsEditor : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<RouteStopsEditor>();

        public RouteStopsEditor()
        {
            InitializeComponent();
            Loaded += (_, _) =>
                Logger.Information(
                    "RouteStopsEditor loaded DataContext={DataContext}",
                    DataContext?.GetType().Name ?? "(null)");
        }

        private RouteAssignmentViewModel? AssignmentViewModel =>
            DataContext as RouteAssignmentViewModel;

        private void RouteStopsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = AssignmentViewModel;
            if (vm?.EditStopCommand is not { } command)
            {
                return;
            }

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }
}
