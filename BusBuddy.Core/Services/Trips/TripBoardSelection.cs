using BusBuddy.Core.Models.Trips;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Board queries for open trips. Unassigned means a missing bus and/or driver.
/// MissingInfo is a separate query: the ticket stays, the place or time does not.
/// </summary>
public static class TripBoardSelection
{
    public static bool IsUnassigned(TripEvent trip)
    {
        ArgumentNullException.ThrowIfNull(trip);
        if (!IsOpen(trip))
        {
            return false;
        }

        if (trip.IsMultiAsset)
        {
            return !trip.DriverId.HasValue;
        }

        return !trip.VehicleId.HasValue || !trip.DriverId.HasValue;
    }

    /// <summary>Same-day fleet apply writes a bus only onto open single-asset trips that have none.</summary>
    public static bool NeedsBus(TripEvent trip)
    {
        ArgumentNullException.ThrowIfNull(trip);
        return IsOpen(trip) && !trip.IsMultiAsset && !trip.VehicleId.HasValue;
    }

    private static bool IsOpen(TripEvent trip)
    {
        var status = TripStatus.Normalize(trip.Status);
        return status is not (TripStatus.Cancelled or TripStatus.Completed);
    }
}
