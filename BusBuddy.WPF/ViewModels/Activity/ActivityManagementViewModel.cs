using System;
using System.Collections.ObjectModel;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Activity
{
    // Simple ViewModel to back ActivityManagementView during MVP
    public class ActivityManagementViewModel
    {
        private static readonly ILogger Logger = Log.ForContext<ActivityManagementViewModel>();

        public ObservableCollection<Models.Activity.ActivityItem> Activities { get; } = new();

        private Models.Activity.ActivityItem? _selectedActivity;
        public Models.Activity.ActivityItem? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                _selectedActivity = value;
            }
        }

        public ActivityManagementViewModel()
        {
            try
            {
                // Seed lightweight demo data for grid bindings
                Activities.Add(new Models.Activity.ActivityItem
                {
                    ActivityName = "Route Audit",
                    ActivityDate = DateTime.Today,
                    Status = "Planned"
                });
                Activities.Add(new Models.Activity.ActivityItem
                {
                    ActivityName = "Driver Training",
                    ActivityDate = DateTime.Today.AddDays(1),
                    Status = "Scheduled"
                });
                Activities.Add(new Models.Activity.ActivityItem
                {
                    ActivityName = "Fuel Reconciliation Review",
                    ActivityDate = DateTime.Today.AddDays(2),
                    Status = "Pending"
                });

                Logger.Information("ActivityManagementViewModel initialized with {Count} demo activities", Activities.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize ActivityManagementViewModel");
            }
        }
    }
}
