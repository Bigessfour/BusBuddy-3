using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Activity-specific repository interface
/// Extends generic repository with activity-specific operations
/// </summary>
public interface IActivityRepository : IRepository<Activity>
{
    // Activity-specific queries
    Task<IEnumerable<Activity>> GetActivitiesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Activity>> GetActivitiesByVehicleAsync(int vehicleId);
    Task<IEnumerable<Activity>> GetActivitiesByDriverAsync(int driverId);
    Task<IEnumerable<Activity>> GetActivitiesByStatusAsync(string status);
    Task<IEnumerable<Activity>> GetActivitiesByTypeAsync(string activityType);
    Task<IEnumerable<Activity>> GetUpcomingActivitiesAsync(int days = 30);
    Task<IEnumerable<Activity>> GetTodaysActivitiesAsync();

    // Scheduling conflicts
    Task<IEnumerable<Activity>> GetConflictingActivitiesAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null);
    Task<bool> HasSchedulingConflictAsync(int vehicleId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null);
    Task<bool> HasDriverConflictAsync(int driverId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null);

    // Statistics and reporting
    Task<int> GetActivityCountByMonthAsync(int year, int month);
    Task<decimal> GetTotalEstimatedCostByMonthAsync(int year, int month);
    Task<IEnumerable<Activity>> GetActivitiesRequiringApprovalAsync();
    Task<Dictionary<string, int>> GetActivitiesByTypeCountAsync(DateTime? startDate = null, DateTime? endDate = null);

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Activity> GetActivitiesByDateRange(DateTime startDate, DateTime endDate);
    IEnumerable<Activity> GetActivitiesByVehicle(int vehicleId);
    IEnumerable<Activity> GetActivitiesByDriver(int driverId);
    IEnumerable<Activity> GetTodaysActivities();
    IEnumerable<Activity> GetUpcomingActivities(int days = 30);
}
