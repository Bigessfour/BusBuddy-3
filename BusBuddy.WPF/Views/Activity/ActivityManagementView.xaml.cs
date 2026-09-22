using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF;
using BusBuddy.WPF.ViewModels.Activity;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.UI.Xaml.Scheduler;

namespace BusBuddy.WPF.Views.Activity
{
    /// <summary>
    /// Clerk trip board. Not the published daily route editor.
    /// </summary>
    public partial class ActivityManagementView : UserControl
    {
        public ActivityManagementView()
        {
            InitializeComponent();
            if (DataContext is null)
            {
                DataContext = App.ServiceProvider?.GetService<ActivityManagementViewModel>()
                    ?? new ActivityManagementViewModel();
                Log.ForContext<ActivityManagementView>().Information("ActivityManagementView DataContext initialized");
            }
        }

        private void AuditLogButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = "Activity log",
                Content = new ActivityTimelineView(),
                Width = 1100,
                Height = 700,
                Owner = Window.GetWindow(this)
            };
            window.Show();
        }

        private void TripScheduler_AppointmentTapped(object sender, AppointmentTappedArgs e)
        {
            if (DataContext is not ActivityManagementViewModel vm)
            {
                return;
            }

            if (int.TryParse(e.Appointment?.Notes, out var id))
            {
                vm.SelectTripById(id);
            }
        }
    }
}
