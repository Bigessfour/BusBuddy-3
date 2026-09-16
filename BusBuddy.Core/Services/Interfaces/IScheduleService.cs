using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    public interface IScheduleService
    {
        Task<IEnumerable<Schedule>> GetSchedulesAsync();
        Task<Schedule?> GetScheduleByIdAsync(int id);
        Task AddScheduleAsync(Schedule schedule);

        /// <summary>
        /// Hop 5 daily instance from the published route's session pairing and begin time.
        /// Returns false when the route is missing or the session slot has no bus and driver.
        /// Does not invent a trip. Route ≠ Trip.
        /// </summary>
        Task<bool> AddDailyFromPublishedRouteAsync(int routeId, DateTime utcDay);
        Task UpdateScheduleAsync(Schedule schedule);
        Task DeleteScheduleAsync(int id);
    }
}
