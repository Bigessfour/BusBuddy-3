using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Analytics;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Views.Analytics
{
    public partial class AnalyticsDashboardView : UserControl
    {
        public AnalyticsDashboardView()
        {
            InitializeComponent();
            if (DataContext == null && App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetService<AnalyticsDashboardViewModel>()
                    ?? App.ServiceProvider.GetRequiredService<AnalyticsDashboardViewModel>();
            }
        }
    }
}
