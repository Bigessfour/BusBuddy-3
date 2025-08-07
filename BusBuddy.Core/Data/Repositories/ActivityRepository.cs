using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Activity-specific repository implementation
/// Extends generic repository with activity-specific operations
/// </summary>
public class ActivityRepository : Repository<Activity>, IActivityRepository
{
    public ActivityRepository(BusBuddy.Core.Data.BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async Activity-Specific Operations

    public async Task<IEnumerable<Activity>> GetActivitiesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.LeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByVehicleAsync(int vehicleId)
    {
        return await Query()
            .Where(a => a.AssignedVehicleId == vehicleId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByDriverAsync(int driverId)
    {
        return await Query()
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByStatusAsync(string status)
    {
        return await Query()
            .Where(a => a.Status == status)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetActivitiesByTypeAsync(string activityType)
    {
        return await Query()
            .Where(a => a.ActivityType == activityType)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetUpcomingActivitiesAsync(int days = 30)
    {
        var endDate = DateTime.Today.AddDays(days);
        return await Query()
            .Where(a => a.Date >= DateTime.Today && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.LeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Activity>> GetTodaysActivitiesAsync()
    {
        return await Query()
            .Where(a => a.Date.Date == DateTime.Today)
            .OrderBy(a => a.LeaveTime)
            .ToListAsync();
    }

    #endregion

    #region Scheduling Conflicts

    public async Task<IEnumerable<Activity>> GetConflictingActivitiesAsync(DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null)
    {
        var query = Query()
            .Where(a => a.Date.Date == activityDate.Date &&
                       ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                        (a.EventTime > startTime && a.EventTime <= endTime) ||
                        (a.LeaveTime <= startTime && a.EventTime >= endTime)));

        if (excludeActivityId.HasValue)
        {
            query = query.Where(a => a.ActivityId != excludeActivityId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<bool> HasSchedulingConflictAsync(int vehicleId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null)
    {
        var query = Query()
            .Where(a => a.AssignedVehicleId == vehicleId &&
                       a.Date.Date == activityDate.Date &&
                       ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                        (a.EventTime > startTime && a.EventTime <= endTime) ||
                        (a.LeaveTime <= startTime && a.EventTime >= endTime)));

        if (excludeActivityId.HasValue)
        {
            query = query.Where(a => a.ActivityId != excludeActivityId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> HasDriverConflictAsync(int driverId, DateTime activityDate, TimeSpan startTime, TimeSpan endTime, int? excludeActivityId = null)
    {
        var query = Query()
            .Where(a => a.DriverId == driverId &&
                       a.Date.Date == activityDate.Date &&
                       ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                        (a.EventTime > startTime && a.EventTime <= endTime) ||
                        (a.LeaveTime <= startTime && a.EventTime >= endTime)));

        if (excludeActivityId.HasValue)
        {
            query = query.Where(a => a.ActivityId != excludeActivityId.Value);
        }

        return await query.AnyAsync();
    }

    #endregion

    #region Statistics and Reporting

    public async Task<int> GetActivityCountByMonthAsync(int year, int month)
    {
        return await Query()
            .Where(a => a.Date.Year == year && a.Date.Month == month)
            .CountAsync();
    }

    public async Task<decimal> GetTotalEstimatedCostByMonthAsync(int year, int month)
    {
        return await Query()
            .Where(a => a.Date.Year == year && a.Date.Month == month && a.EstimatedCost.HasValue)
            .SumAsync(a => a.EstimatedCost ?? 0);
    }

    public async Task<IEnumerable<Activity>> GetActivitiesRequiringApprovalAsync()
    {
        return await Query()
            .Where(a => a.ApprovalRequired && !a.Approved)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetActivitiesByTypeCountAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = Query();

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Date <= endDate.Value);
        }

        return await query
            .GroupBy(a => a.ActivityType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }

    #endregion

    #region Synchronous Methods for Syncfusion

    public IEnumerable<Activity> GetActivitiesByDateRange(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(a => a.Date >= startDate && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.LeaveTime)
            .ToList();
    }

    public IEnumerable<Activity> GetActivitiesByVehicle(int vehicleId)
    {
        return Query()
            .Where(a => a.AssignedVehicleId == vehicleId)
            .OrderByDescending(a => a.Date)
            .ToList();
    }

    public IEnumerable<Activity> GetActivitiesByDriver(int driverId)
    {
        return Query()
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.Date)
            .ToList();
    }

    public IEnumerable<Activity> GetTodaysActivities()
    {
        return Query()
            .Where(a => a.Date.Date == DateTime.Today)
            .OrderBy(a => a.LeaveTime)
            .ToList();
    }

    public IEnumerable<Activity> GetUpcomingActivities(int days = 30)
    {
        var endDate = DateTime.Today.AddDays(days);
        return Query()
            .Where(a => a.Date >= DateTime.Today && a.Date <= endDate)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.LeaveTime)
            .ToList();
    }

    #endregion
}
