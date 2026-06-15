using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BusBuddy.WPF.ViewModels
{
    public class ActivityLogViewModel : BaseInDevelopmentViewModel
    {
        private readonly IActivityLogService _activityLogService;
        public ObservableCollection<ActivityLog> Logs { get; } = new();

        public ActivityLogViewModel(IActivityLogService activityLogService)
            : base()
        {
            _activityLogService = activityLogService;
            _ = LoadLogsAsync();

            // Set as in-development
            IsInDevelopment = true;
        }

        public async Task LoadLogsAsync()
        {
            try
            {
                Logs.Clear();
                // Use the optimized paged method instead of getting all logs
                var logs = await _activityLogService.GetLogsPagedAsync(1, 50); // Load first 50 logs
                foreach (var log in logs)
                {
                    Logs.Add(log);
                }


                Logger.Information("Loaded {LogCount} activity logs using pagination", Logs.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading activity logs");
            }
        }
    }
}
