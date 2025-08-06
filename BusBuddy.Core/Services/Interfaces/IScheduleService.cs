using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    public interface IScheduleService
    {
        Task<IEnumerable<Schedule>> GetSchedulesAsync();
        Task<Schedule?> GetScheduleByIdAsync(int id);
        Task AddScheduleAsync(Schedule schedule);
        Task UpdateScheduleAsync(Schedule schedule);
        Task DeleteScheduleAsync(int id);

        // New methods for sports category filtering and trip derivation
        Task<IEnumerable<Schedule>> GetSchedulesByCategoryAsync(string category);
        void DeriveTripDetails(Schedule schedule);
    }
}
