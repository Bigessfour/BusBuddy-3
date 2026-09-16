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
        /// <summary>
        /// Cancels the trip (<see cref="TripStatus.Cancelled"/>) and keeps the row.
        /// Does not delete history. Route ≠ Trip.
        /// </summary>
        Task CancelTripAsync(int id);
        Task<bool> HasConflictsAsync(int? vehicleId, int? driverId, DateTime startTime, DateTime endTime, int? excludeTripId = null);
        Task<IEnumerable<TripEvent>> GetConflictingTripsAsync(int? vehicleId, int? driverId, DateTime startTime, DateTime endTime);

        /// <summary>
        /// Warn when a loan overlaps another trip or a published AM/PM home session.
        /// Does not refuse the assignment and does not change HomeRouteId or RouteId.
        /// </summary>
        Task<IReadOnlyList<string>> GetAssignmentWarningsAsync(
            int? vehicleId,
            int? driverId,
            DateTime startTime,
            DateTime endTime,
            int? excludeTripId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Clerk import of the office trip board. Upserts by ExternalTicketNo. Does not create students.</summary>
        Task<TripBoardImportResult> ImportBoardCsvAsync(string csv, CancellationToken cancellationToken = default);

        /// <summary>Confirmed requires validated destination + times + driver + bus.</summary>
        Task<Result> ConfirmTripAsync(int tripEventId, CancellationToken cancellationToken = default);

        /// <summary>Refresh PathMiles from Google Routes only after origin and destination are validated.</summary>
        Task RefreshPathMilesAsync(int tripEventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clerk-initiated same-day fleet suggestion via Route Optimization API.
        /// Does not rewrite PickupTime. Optionally assigns VehicleId on unassigned trips only.
        /// </summary>
        Task<TripFleetOptimizeResult> SuggestSameDayFleetAsync(
            DateTime tripDate,
            bool applyToUnassigned = false,
            CancellationToken cancellationToken = default);
    }

    public sealed class TripFleetOptimizeResult
    {
        public bool Succeeded { get; init; }

        public string Status { get; init; } = string.Empty;

        public int SuggestedCount { get; init; }

        public int AppliedCount { get; init; }
    }
}
