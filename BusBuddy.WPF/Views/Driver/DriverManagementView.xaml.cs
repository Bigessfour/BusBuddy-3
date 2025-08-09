using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverManagementView.xaml
    /// Driver management view with comprehensive Syncfusion theming and skin manager integration
    /// </summary>
    public partial class DriverManagementView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<DriverManagementView>();
        private readonly ISkinManagerService? _skinManagerService;

        public DriverManagementView()
        {
            InitializeComponent();

            // Get SkinManagerService from DI container
            _skinManagerService = App.ServiceProvider?.GetService<ISkinManagerService>();

            if (_skinManagerService == null)
            {
                Logger.Warning("SkinManagerService not available, using default theming");
            }

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Debug("DriverManagementView loaded, applying theme");

                // Apply current theme to this control
                _skinManagerService?.ApplyThemeToElement(this);

                Logger.Information("DriverManagementView theme applied successfully");
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "Failed to apply theme to DriverManagementView");
            }
        }
    }
}
