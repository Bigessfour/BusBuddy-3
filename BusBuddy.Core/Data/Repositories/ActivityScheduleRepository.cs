using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// ActivitySchedule-specific repository implementation
/// Extends generic repository with activity schedule operations
/// </summary>
public class ActivityScheduleRepository : Repository<ActivitySchedule>, IActivityScheduleRepository
{
    public ActivityScheduleRepository(BusBuddy.Core.Data.BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async ActivitySchedule-Specific Operations

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateAsync(DateTime scheduleDate)
    {
        return await Query()
            .Where(asc => asc.ScheduledDate.Date == scheduleDate.Date)
            .OrderBy(asc => asc.ScheduledLeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(asc => asc.ScheduledDate >= startDate.Date && asc.ScheduledDate <= endDate.Date)
            .OrderBy(asc => asc.ScheduledDate)
            .ThenBy(asc => asc.ScheduledLeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByTripTypeAsync(string tripType)
    {
        return await Query()
            .Where(asc => asc.TripType == tripType)
            .OrderBy(asc => asc.ScheduledDate)
            .ThenBy(asc => asc.ScheduledLeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByVehicleAsync(int vehicleId)
    {
        return await Query()
            .Where(asc => asc.ScheduledVehicleId == vehicleId)
            .OrderBy(asc => asc.ScheduledDate)
            .ThenBy(asc => asc.ScheduledLeaveTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDriverAsync(int driverId)
    {
        return await Query()
            .Where(asc => asc.ScheduledDriverId == driverId)
            .OrderBy(asc => asc.ScheduledDate)
            .ThenBy(asc => asc.ScheduledLeaveTime)
            .ToListAsync();
    }

    public async Task<bool> HasConflictAsync(int vehicleId, int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
    {
        return await Query()
            .AnyAsync(asc => (asc.ScheduledVehicleId == vehicleId || asc.ScheduledDriverId == driverId) &&
                            asc.ScheduledDate.Date == scheduleDate.Date &&
                            ((asc.ScheduledLeaveTime <= startTime && asc.ScheduledEventTime > startTime) ||
                             (asc.ScheduledLeaveTime < endTime && asc.ScheduledEventTime >= endTime) ||
                             (asc.ScheduledLeaveTime >= startTime && asc.ScheduledEventTime <= endTime)));
    }

    #endregion

    #region Synchronous ActivitySchedule-Specific Operations

    public IEnumerable<ActivitySchedule> GetSchedulesByDate(DateTime scheduleDate)
    {
        return Query()
            .Where(asc => asc.ScheduledDate.Date == scheduleDate.Date)
            .OrderBy(asc => asc.ScheduledLeaveTime)
            .ToList();
    }

    public IEnumerable<ActivitySchedule> GetSchedulesByTripType(string tripType)
    {
        return Query()
            .Where(asc => asc.TripType == tripType)
            .OrderBy(asc => asc.ScheduledDate)
            .ThenBy(asc => asc.ScheduledLeaveTime)
            .ToList();
    }

    public bool HasConflict(int vehicleId, int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime)
    {
        return Query()
            .Any(asc => (asc.ScheduledVehicleId == vehicleId || asc.ScheduledDriverId == driverId) &&
                       asc.ScheduledDate.Date == scheduleDate.Date &&
                       ((asc.ScheduledLeaveTime <= startTime && asc.ScheduledEventTime > startTime) ||
                        (asc.ScheduledLeaveTime < endTime && asc.ScheduledEventTime >= endTime) ||
                        (asc.ScheduledLeaveTime >= startTime && asc.ScheduledEventTime <= endTime)));
    }

    #endregion
}
