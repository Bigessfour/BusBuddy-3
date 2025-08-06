using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Schedule-specific repository implementation
/// Extends generic repository with schedule operations
/// </summary>
public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
{
    public ScheduleRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async Schedule-Specific Operations

    public async Task<IEnumerable<Schedule>> GetSchedulesByDateAsync(DateTime date)
    {
        return await Query()
            .Where(s => s.ScheduleDate.Date == date.Date)
            .OrderBy(s => s.DepartureTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesByRouteAsync(int routeId)
    {
        return await Query()
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.ScheduleDate)
            .ThenBy(s => s.DepartureTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesByBusAsync(int busId)
    {
        return await Query()
            .Where(s => s.BusId == busId)
            .OrderBy(s => s.ScheduleDate)
            .ThenBy(s => s.DepartureTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesByDriverAsync(int driverId)
    {
        return await Query()
            .Where(s => s.DriverId == driverId)
            .OrderBy(s => s.ScheduleDate)
            .ThenBy(s => s.DepartureTime)
            .ToListAsync();
    }

    public async Task<bool> HasConflictAsync(int busId, int driverId, DateTime startTime, DateTime endTime)
    {
        return await Query()
            .AnyAsync(s => (s.BusId == busId || s.DriverId == driverId) &&
                          s.ScheduleDate.Date == startTime.Date &&
                          ((s.DepartureTime <= startTime && s.ArrivalTime > startTime) ||
                           (s.DepartureTime < endTime && s.ArrivalTime >= endTime) ||
                           (s.DepartureTime >= startTime && s.ArrivalTime <= endTime)));
    }

    #endregion

    #region Synchronous Schedule-Specific Operations

    public IEnumerable<Schedule> GetSchedulesByDate(DateTime date)
    {
        return Query()
            .Where(s => s.ScheduleDate.Date == date.Date)
            .OrderBy(s => s.DepartureTime)
            .ToList();
    }

    public IEnumerable<Schedule> GetSchedulesByRoute(int routeId)
    {
        return Query()
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.ScheduleDate)
            .ThenBy(s => s.DepartureTime)
            .ToList();
    }

    public bool HasConflict(int busId, int driverId, DateTime startTime, DateTime endTime)
    {
        return Query()
            .Any(s => (s.BusId == busId || s.DriverId == driverId) &&
                     s.ScheduleDate.Date == startTime.Date &&
                     ((s.DepartureTime <= startTime && s.ArrivalTime > startTime) ||
                      (s.DepartureTime < endTime && s.ArrivalTime >= endTime) ||
                      (s.DepartureTime >= startTime && s.ArrivalTime <= endTime)));
    }

    #endregion
}
