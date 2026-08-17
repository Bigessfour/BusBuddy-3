using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>
/// AM pickups work backward from school StartTime; PM dropoffs forward from DismissalTime.
/// Uses Haversine + AverageSpeedMph (Maps ETA optional / fail-open).
/// </summary>
public static class PickupScheduleCalculator
{
    public static readonly TimeSpan DefaultDwell = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Transfer AM uses pickup; PM uses dropoff when present, otherwise pickup.
    /// </summary>
    public static bool TryResolveTransferStop(
        StudentSchoolTransfer transfer,
        RouteTimeSlotKind slot,
        out decimal latitude,
        out decimal longitude,
        out string address)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        var useDropoff = slot == RouteTimeSlotKind.PM &&
                         transfer.DropoffLatitude is not null &&
                         transfer.DropoffLongitude is not null;
        var lat = useDropoff ? transfer.DropoffLatitude : transfer.PickupLatitude;
        var lon = useDropoff ? transfer.DropoffLongitude : transfer.PickupLongitude;
        if (lat is null || lon is null)
        {
            latitude = default;
            longitude = default;
            address = string.Empty;
            return false;
        }

        latitude = lat.Value;
        longitude = lon.Value;
        address = useDropoff
            ? (transfer.DropoffAddress ?? transfer.PickupAddress ?? string.Empty)
            : (transfer.PickupAddress ?? string.Empty);
        return true;
    }

    /// <summary>
    /// Ordered stops from first pickup toward school. Returns arrival times per stop (same length).
    /// <paramref name="underflow"/> is true when travel would push a stop before midnight (00:00);
    /// arrivals are still clamped to zero so callers can warn or fail.
    /// </summary>
    public static IReadOnlyList<TimeSpan> ComputeAmPickupArrivals(
        IReadOnlyList<(double Latitude, double Longitude)> orderedStopsTowardSchool,
        double schoolLat,
        double schoolLon,
        TimeSpan schoolStartTime,
        RoutingDistrictSettings settings,
        out bool underflow,
        TimeSpan? dwellPerStop = null)
    {
        ArgumentNullException.ThrowIfNull(orderedStopsTowardSchool);
        ArgumentNullException.ThrowIfNull(settings);
        underflow = false;

        if (orderedStopsTowardSchool.Count == 0)
        {
            return Array.Empty<TimeSpan>();
        }

        var dwell = dwellPerStop ?? DefaultDwell;
        var mph = settings.AverageSpeedMph <= 0 ? 25.0 : settings.AverageSpeedMph;
        var arrivals = new TimeSpan[orderedStopsTowardSchool.Count];

        // Work backward from school arrival
        var cursor = schoolStartTime;
        for (var i = orderedStopsTowardSchool.Count - 1; i >= 0; i--)
        {
            var (lat, lon) = orderedStopsTowardSchool[i];
            double nextLat, nextLon;
            if (i == orderedStopsTowardSchool.Count - 1)
            {
                nextLat = schoolLat;
                nextLon = schoolLon;
            }
            else
            {
                (nextLat, nextLon) = orderedStopsTowardSchool[i + 1];
            }

            var minutes = LegMinutes(lat, lon, nextLat, nextLon, mph);
            var next = cursor - TimeSpan.FromMinutes(minutes) -
                       (i < orderedStopsTowardSchool.Count - 1 ? dwell : TimeSpan.Zero);
            if (next < TimeSpan.Zero)
            {
                underflow = true;
                next = TimeSpan.Zero;
            }

            cursor = next;
            arrivals[i] = RoundToMinute(cursor);
        }

        return arrivals;
    }

    /// <summary>PM: depart school at dismissal, then stop arrivals in dropoff order.</summary>
    public static IReadOnlyList<TimeSpan> ComputePmDropoffArrivals(
        IReadOnlyList<(double Latitude, double Longitude)> orderedStopsFromSchool,
        double schoolLat,
        double schoolLon,
        TimeSpan dismissalTime,
        RoutingDistrictSettings settings,
        TimeSpan? dwellPerStop = null)
    {
        ArgumentNullException.ThrowIfNull(orderedStopsFromSchool);
        ArgumentNullException.ThrowIfNull(settings);

        if (orderedStopsFromSchool.Count == 0)
        {
            return Array.Empty<TimeSpan>();
        }

        var dwell = dwellPerStop ?? DefaultDwell;
        var mph = settings.AverageSpeedMph <= 0 ? 25.0 : settings.AverageSpeedMph;
        var arrivals = new TimeSpan[orderedStopsFromSchool.Count];

        var cursor = dismissalTime;
        for (var i = 0; i < orderedStopsFromSchool.Count; i++)
        {
            var (lat, lon) = orderedStopsFromSchool[i];
            double prevLat, prevLon;
            if (i == 0)
            {
                prevLat = schoolLat;
                prevLon = schoolLon;
            }
            else
            {
                (prevLat, prevLon) = orderedStopsFromSchool[i - 1];
            }

            var minutes = LegMinutes(prevLat, prevLon, lat, lon, mph);
            cursor = cursor + TimeSpan.FromMinutes(minutes) + (i > 0 ? dwell : TimeSpan.Zero);
            arrivals[i] = RoundToMinute(cursor);
        }

        return arrivals;
    }

    public static bool IsMonotonicNonDecreasing(IReadOnlyList<TimeSpan> times)
    {
        for (var i = 1; i < times.Count; i++)
        {
            if (times[i] < times[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    private static double LegMinutes(double lat1, double lon1, double lat2, double lon2, double mph)
    {
        var miles = RoutePacker.HaversineMiles(lat1, lon1, lat2, lon2);
        return miles / mph * 60.0;
    }

    private static TimeSpan RoundToMinute(TimeSpan t) =>
        TimeSpan.FromMinutes(Math.Floor(t.TotalMinutes));
}
