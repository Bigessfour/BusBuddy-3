using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Trips;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
{
    public interface ITripEventService
    {
        Task<IEnumerable<TripEvent>> GetAllTripsAsync();
        Task<TripEvent?> GetTripByIdAsync(int id);
        Task<IEnumerable<TripEvent>> GetTripsByTypeAsync(TripType tripType);
        Task<IEnumerable<TripEvent>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate);
        /// <summary>
        /// Open trips missing a bus, a driver, or both.
        /// Multi-asset rows omit BusId on purpose, so only a missing driver counts.
        /// </summary>
        Task<IEnumerable<TripEvent>> GetUnassignedTripsAsync();

        /// <summary>Ticket rows still missing a place or time. Status MissingInfo. Kept on the board.</summary>
        Task<IEnumerable<TripEvent>> GetIncompleteTripsAsync();

        Task AddTripAsync(TripEvent tripEvent);
        Task UpdateTripAsync(TripEvent tripEvent);

        /// <summary>
        /// Clerk cancel. Sets Cancelled and keeps the ticket. Completed rows stay completed.
        /// </summary>
        Task<Result> CancelTripAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-delete for an import mistake that is still MissingInfo or Draft.
        /// Confirmed, changed, completed, and cancelled rows stay.
        /// </summary>
        Task<bool> DeleteTripAsync(int id);
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

        /// <summary>Confirmed requires validated destination + times + driver + bus, and bus capacity.</summary>
        Task<Result> ConfirmTripAsync(int tripEventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Same as <see cref="ConfirmTripAsync(int, CancellationToken)"/>.
        /// Seating or wheelchair overflow blocks unless <paramref name="overrideSeating"/> is set
        /// and the district allows a seating override.
        /// </summary>
        Task<Result> ConfirmTripAsync(
            int tripEventId,
            bool overrideSeating,
            CancellationToken cancellationToken = default);

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
