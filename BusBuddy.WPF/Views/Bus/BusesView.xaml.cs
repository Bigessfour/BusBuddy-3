using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Bus;
using Serilog;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// UserControl host for fleet CRUD. <see cref="BusFleetWindow"/> / <see cref="BusBuddy.WPF.Utilities.BusFleetLauncher"/>
    /// always open this, which hosts <see cref="BusManagementView"/>.
    /// </summary>
    public partial class BusesView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<BusesView>();

        public BusesView() : this(BusManagementStartup.None)
        {
        }

        public BusesView(BusManagementStartup startup)
        {
            InitializeComponent();
            Host.Children.Add(new BusManagementView(startup));
            Logger.Information("BusesView host constructed Startup={Startup}", startup);
        }
    }
}
