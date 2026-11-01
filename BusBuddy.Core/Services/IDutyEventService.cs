using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>Shared driver+bus duty / inspection log (#112).</summary>
public interface IDutyEventService
{
    Task<IReadOnlyList<DutyEvent>> GetForDateAsync(DateTime dateUtc);

    Task<IReadOnlyList<DutyEvent>> GetByBusAndDriverOnDateAsync(
        int busId,
        int driverId,
        DateTime dateUtc);

    Task<DutyEvent?> GetByIdAsync(int dutyEventId);

    /// <summary>
    /// Persists a duty event. Defect also creates a Fleet Maintenance row.
    /// Fuel type writes attribution only — never inserts a Fuel gallons row.
    /// </summary>
    Task<DutyEvent> CreateAsync(DutyEvent dutyEvent);

    Task<bool> DeleteAsync(int dutyEventId);

    /// <summary>
    /// Active route AM/PM pairings for the day that lack a PreTrip log.
    /// Visible only — does not block the route.
    /// </summary>
    Task<IReadOnlyList<MissingPreTripRow>> GetMissingPreTripsAsync(DateTime dateUtc);
}
