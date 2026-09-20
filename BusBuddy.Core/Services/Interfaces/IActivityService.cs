using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing activity scheduling operations
    /// Provides methods for managing bus activity scheduling and related operations
    /// </summary>
    public interface IActivityService
    {
        // Basic CRUD Operations
        Task<IEnumerable<Activity>> GetAllActivitiesAsync();
        Task<Activity?> GetActivityByIdAsync(int id);
        Task<Activity> CreateActivityAsync(Activity activity);
        Task<Activity> UpdateActivityAsync(Activity activity);
        Task<bool> DeleteActivityAsync(int id);

        // Date Range Queries
        Task<IEnumerable<Activity>> GetActivitiesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<Activity>> GetActivitiesByDateAsync(DateTime activityDate);
        Task<IEnumerable<Activity>> GetUpcomingActivitiesAsync(int days = 7);

        // Filtering Operations
        Task<IEnumerable<Activity>> GetActivitiesByRouteAsync(int routeId);
        Task<IEnumerable<Activity>> GetActivitiesByDriverAsync(int driverId);
        Task<IEnumerable<Activity>> GetActivitiesByVehicleAsync(int vehicleId);
        Task<IEnumerable<Activity>> GetActivitiesByTypeAsync(string activityType);
        Task<IEnumerable<Activity>> GetActivitiesByStatusAsync(string status);
        Task<IEnumerable<Activity>> GetActivitiesByRequestorAsync(string requestedBy);
        Task<IEnumerable<Activity>> SearchActivitiesAsync(string searchTerm);

        // Assignment Operations
        Task<bool> AssignDriverToActivityAsync(int activityId, int driverId);
        Task<bool> AssignVehicleToActivityAsync(int activityId, int vehicleId);
        Task<bool> UpdateActivityStatusAsync(int activityId, string status);
        Task<List<Driver>> GetAvailableDriversForActivityAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime);
        Task<List<Bus>> GetAvailableVehiclesForActivityAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime);
        Task<bool> IsDriverAvailableForActivityAsync(int driverId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime);
        Task<bool> IsVehicleAvailableForActivityAsync(int vehicleId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime);

        // Conflict Detection and Resolution
        Task<List<Activity>> DetectScheduleConflictsAsync(Activity newActivity);
        Task<List<string>> ValidateActivityAsync(Activity activity);

        // Analytics and Reporting
        Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, double>> GetActivityMetricsAsync(DateTime startDate, DateTime endDate);
        Task<string> ExportActivitiesToCsvAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateActivityReportAsync(int activityId);
        Task<byte[]> GenerateActivityCalendarReportAsync(DateTime startDate, DateTime endDate);

#if DEBUG
        // DEBUG Instrumentation
        Task<Dictionary<string, object>> GetActivityDiagnosticsAsync(int activityId);
        Task<Dictionary<string, object>> GetScheduleOperationMetricsAsync();
#endif
    }
}
