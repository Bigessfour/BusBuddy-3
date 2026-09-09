using System.Windows.Controls;
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
    }
}
