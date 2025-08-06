using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing activity schedules
    /// Provides operations for creating, retrieving, updating, and deleting activity schedules
    /// </summary>
    public interface IActivityScheduleService
    {
        // Basic CRUD Operations
        Task<IEnumerable<ActivitySchedule>> GetAllActivitySchedulesAsync();
        Task<ActivitySchedule?> GetActivityScheduleByIdAsync(int id);
        Task<ActivitySchedule> CreateActivityScheduleAsync(ActivitySchedule activitySchedule);
        Task<ActivitySchedule> UpdateActivityScheduleAsync(ActivitySchedule activitySchedule);
        Task<bool> DeleteActivityScheduleAsync(int id);

        // Filtering Operations
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDriverAsync(int driverId);
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByVehicleAsync(int vehicleId);
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByStatusAsync(string status);
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByTripTypeAsync(string tripType);
        Task<IEnumerable<ActivitySchedule>> GetActivitySchedulesByDestinationAsync(string destination);

        // Scheduling Operations
        Task<bool> IsVehicleAvailableAsync(int vehicleId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);
        Task<bool> IsDriverAvailableAsync(int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);
        Task<IEnumerable<Driver>> GetAvailableDriversAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);
        Task<IEnumerable<Bus>> GetAvailableVehiclesAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);
        Task<bool> ConfirmActivityScheduleAsync(int activityScheduleId);
        Task<bool> CancelActivityScheduleAsync(int activityScheduleId, string? reason = null);
        Task<bool> CompleteActivityScheduleAsync(int activityScheduleId);

        // Conflict Detection
        Task<IEnumerable<ActivitySchedule>> FindScheduleConflictsAsync(DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityScheduleId = null);
        Task<bool> HasConflictsAsync(ActivitySchedule activitySchedule);

        // Analytics and Reporting
        Task<Dictionary<string, int>> GetActivityScheduleStatisticsByTripTypeAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, int>> GetActivityScheduleStatisticsByDriverAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, int>> GetActivityScheduleStatisticsByVehicleAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, int>> GetActivityScheduleStatisticsByStatusAsync(DateTime startDate, DateTime endDate);
        Task<string> ExportActivitySchedulesToCsvAsync(DateTime startDate, DateTime endDate);

#if DEBUG
        // DEBUG Instrumentation
        Task<Dictionary<string, object>> GetActivityScheduleDiagnosticsAsync(int activityScheduleId);
        Task<Dictionary<string, object>> GetActivityScheduleOperationMetricsAsync();
#endif
    }
}
