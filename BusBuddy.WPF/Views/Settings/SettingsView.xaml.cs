using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using BusBuddy.WPF.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            if (DataContext == null && App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetService<SettingsViewModel>()
                    ?? App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            }
        }

        private void OnMapsLegalLinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // Best-effort open in the clerk's default browser.
            }

            e.Handled = true;
        }
    }
}
