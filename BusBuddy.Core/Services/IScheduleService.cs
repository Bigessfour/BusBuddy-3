using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    public interface IScheduleService
    {
        Task<IEnumerable<Schedule>> GetSchedulesAsync();
        Task<Schedule?> GetScheduleByIdAsync(int id);
        Task AddScheduleAsync(Schedule schedule);
        Task UpdateScheduleAsync(Schedule schedule);
        Task DeleteScheduleAsync(int id);

        /// <summary>
        /// Dated session rows for one route on a calendar day (UTC date). Used for #113 substitutes.
        /// Includes Bus/Driver so print can label a spare that is no longer in the available list.
        /// </summary>
        Task<IReadOnlyList<Schedule>> GetSchedulesForRouteOnDateAsync(
            int routeId,
            DateTime scheduleDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upserts a published-route session row and cancels other active siblings for the same
        /// route / calendar day / departure clock (spare bus must not leave a competing home row).
        /// </summary>
        Task SaveRouteSessionScheduleAsync(Schedule schedule, CancellationToken cancellationToken = default);

        // New methods for sports category filtering and trip derivation
        Task<IEnumerable<Schedule>> GetSchedulesByCategoryAsync(string category);
        void DeriveTripDetails(Schedule schedule);
    }
}
