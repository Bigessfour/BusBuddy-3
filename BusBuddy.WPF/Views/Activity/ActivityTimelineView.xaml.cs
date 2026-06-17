using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Activity;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Views.Activity
{
    public partial class ActivityTimelineView : UserControl
    {
        public ActivityTimelineView()
        {
            InitializeComponent();
            if (DataContext == null && App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetService<ActivityTimelineViewModel>()
                    ?? App.ServiceProvider.GetRequiredService<ActivityTimelineViewModel>();
            }
        }
    }
}
