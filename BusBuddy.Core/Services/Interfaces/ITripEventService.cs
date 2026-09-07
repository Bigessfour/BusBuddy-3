using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Trips;
using BusBuddy.Core.Utilities;

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

        /// <summary>Clerk import of the office trip board. Upserts by ExternalTicketNo. Does not create students.</summary>
        Task<TripBoardImportResult> ImportBoardCsvAsync(string csv, CancellationToken cancellationToken = default);

        /// <summary>Confirmed requires validated destination + times + driver + bus.</summary>
        Task<Result> ConfirmTripAsync(int tripEventId, CancellationToken cancellationToken = default);

        /// <summary>Refresh PathMiles from Google Routes only after origin and destination are validated.</summary>
        Task RefreshPathMilesAsync(int tripEventId, CancellationToken cancellationToken = default);
    }
}
