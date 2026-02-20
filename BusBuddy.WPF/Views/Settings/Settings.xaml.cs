using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BusBuddy.WPF.ViewModels.Settings;

namespace BusBuddy.WPF.Views.Settings
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// Production Phase: Full implementation with MVVM pattern
    /// </summary>
    public partial class Settings : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<Settings>();

        public Settings()
        {
            try
            {
                Logger.Debug("Initializing Settings view");

                InitializeComponent();

                // Get ViewModel from DI container
                var viewModel = App.ServiceProvider?.GetService<SettingsViewModel>();
                if (viewModel != null)
                {
                    DataContext = viewModel;
                    Logger.Debug("Settings ViewModel set successfully");
                }
                else
                {
                    Logger.Error("Failed to resolve SettingsViewModel from DI container");
                    throw new InvalidOperationException("SettingsViewModel could not be resolved from dependency injection container");
                }

                Logger.Information("Settings view initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize Settings view");
                throw;
            }
        }
    }
}
