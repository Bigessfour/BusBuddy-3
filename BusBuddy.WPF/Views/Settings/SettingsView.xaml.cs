using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using BusBuddy.WPF.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsView>();

        public SettingsView()
        {
            InitializeComponent();
            try
            {
                if (DataContext == null && App.ServiceProvider != null)
                {
                    DataContext = App.ServiceProvider.GetService<SettingsViewModel>()
                        ?? App.ServiceProvider.GetRequiredService<SettingsViewModel>();
                }

                if (DataContext is SettingsViewModel)
                {
                    Logger.Information("SettingsView DataContext set {DataContext}", DataContext.GetType().Name);
                }
                else
                {
                    Logger.Warning("SettingsView opened without SettingsViewModel DataContext={DataContext}", DataContext?.GetType().Name ?? "(null)");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "SettingsView failed to resolve SettingsViewModel");
            }
        }

        private void OnMapsLegalLinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                Logger.Information("Opened Maps legal URL {Uri}", e.Uri);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "Failed to open Maps legal URL {Uri}", e.Uri);
            }

            e.Handled = true;
        }
    }
}
