using System.Windows.Controls;
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
    }
}
