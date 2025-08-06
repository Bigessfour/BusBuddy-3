using BusBuddy.Core.Models.Trips;

namespace BusBuddy.Core.Services.Interfaces
{
    public interface ITripEventService
    {
        Task<IEnumerable<TripEvent>> GetAllTripsAsync();
        Task<TripEvent?> GetTripByIdAsync(int id);
        Task<IEnumerable<TripEvent>> GetTripsByTypeAsync(TripType tripType);
        Task<IEnumerable<TripEvent>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<TripEvent>> GetUnassignedTripsAsync();
        Task AddTripAsync(TripEvent tripEvent);
        Task UpdateTripAsync(TripEvent tripEvent);
        Task DeleteTripAsync(int id);
        Task<bool> HasConflictsAsync(int? vehicleId, int? driverId, DateTime startTime, DateTime endTime, int? excludeTripId = null);
        Task<IEnumerable<TripEvent>> GetConflictingTripsAsync(int? vehicleId, int? driverId, DateTime startTime, DateTime endTime);
    }
}
