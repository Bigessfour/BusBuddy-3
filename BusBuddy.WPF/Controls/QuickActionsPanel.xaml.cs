using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Vehicle;
using Serilog;

namespace BusBuddy.WPF.Controls
{
    /// <summary>
    /// Interaction logic for QuickActionsPanel.xaml
    /// </summary>
    public partial class QuickActionsPanel : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<QuickActionsPanel>();

        public QuickActionsPanel()
        {
            InitializeComponent();
        }

        private void AddVehicle_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Add vehicle action requested");
            try
            {
                var owner = Window.GetWindow(this);
                VehicleFleetLauncher.ShowDialog(owner, VehicleManagementStartup.AddVehicle);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening vehicle fleet");
                MessageBox.Show($"Error opening vehicle management: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDriver_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Add driver action requested");
            try
            {
                var driverForm = new BusBuddy.WPF.Views.Driver.DriverForm();
                var result = driverForm.ShowDialog();
                if (result == true)
                {
                    MessageBox.Show("Driver added successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Driver form");
                MessageBox.Show($"Error opening Driver form: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScheduleRoute_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Schedule route action requested");
            MessageBox.Show("Route management feature available from the main menu (Routes tab).",
                "Route Management", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
