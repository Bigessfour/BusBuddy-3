using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// SchoolCalendar-specific repository implementation
/// Extends generic repository with school calendar operations
/// </summary>
public class SchoolCalendarRepository : Repository<SchoolCalendar>, ISchoolCalendarRepository
{
    public SchoolCalendarRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async SchoolCalendar-Specific Operations

    public async Task<IEnumerable<SchoolCalendar>> GetEventsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(sc => sc.Date >= startDate.Date && sc.Date <= endDate.Date)
            .OrderBy(sc => sc.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<SchoolCalendar>> GetEventsByTypeAsync(string eventType)
    {
        return await Query()
            .Where(sc => sc.EventType == eventType)
            .OrderBy(sc => sc.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<SchoolCalendar>> GetSchoolDaysAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(sc => sc.Date >= startDate.Date && sc.Date <= endDate.Date && sc.EventType == "School Day")
            .OrderBy(sc => sc.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<SchoolCalendar>> GetHolidaysAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(sc => sc.Date >= startDate.Date && sc.Date <= endDate.Date && sc.EventType == "Holiday")
            .OrderBy(sc => sc.Date)
            .ToListAsync();
    }

    public async Task<bool> IsSchoolDayAsync(DateTime checkDate)
    {
        var calendarEntry = await FirstOrDefaultAsync(sc => sc.Date.Date == checkDate.Date);
        return calendarEntry != null && calendarEntry.EventType == "School Day";
    }

    public async Task<bool> AreRoutesRequiredAsync(DateTime checkDate)
    {
        var calendarEntry = await FirstOrDefaultAsync(sc => sc.Date.Date == checkDate.Date);
        return calendarEntry?.RoutesRequired ?? false;
    }

    #endregion

    #region Synchronous SchoolCalendar-Specific Operations

    public IEnumerable<SchoolCalendar> GetEventsByDateRange(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(sc => sc.Date >= startDate.Date && sc.Date <= endDate.Date)
            .OrderBy(sc => sc.Date)
            .ToList();
    }

    public IEnumerable<SchoolCalendar> GetSchoolDays(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(sc => sc.Date >= startDate.Date && sc.Date <= endDate.Date && sc.EventType == "School Day")
            .OrderBy(sc => sc.Date)
            .ToList();
    }

    public bool IsSchoolDay(DateTime date)
    {
        var calendarEntry = FirstOrDefault(sc => sc.Date.Date == date.Date);
        return calendarEntry != null && calendarEntry.EventType == "School Day";
    }

    public bool AreRoutesRequired(DateTime date)
    {
        var calendarEntry = FirstOrDefault(sc => sc.Date.Date == date.Date);
        return calendarEntry?.RoutesRequired ?? false;
    }

    #endregion
}
