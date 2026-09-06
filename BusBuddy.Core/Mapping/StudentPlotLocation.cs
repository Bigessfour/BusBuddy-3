using BusBuddy.Core.Models;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Where a student pins on the district map: catalog pickup stop if assigned, otherwise home GPS.
/// Geocoding is a separate step when this returns null.
/// </summary>
public readonly record struct StudentPlotPoint(
    double Latitude,
    double Longitude,
    bool AtPickup,
    string? PickupName);

public static class StudentPlotLocation
{
    public static IReadOnlyDictionary<int, PickupStop> Index(IEnumerable<PickupStop>? stops) =>
        stops is null
            ? new Dictionary<int, PickupStop>()
            : stops.ToDictionary(s => s.PickupStopId);

    public static StudentPlotPoint? TryFromStored(
        Student student,
        IReadOnlyDictionary<int, PickupStop>? pickups)
    {
        ArgumentNullException.ThrowIfNull(student);
        pickups ??= new Dictionary<int, PickupStop>();

        if (student.PickupStopId is int id && pickups.TryGetValue(id, out var stop))
        {
            return new StudentPlotPoint(
                (double)stop.Latitude,
                (double)stop.Longitude,
                AtPickup: true,
                PickupName: stop.Name);
        }

        if (student.Latitude is decimal lat && student.Longitude is decimal lon)
        {
            return new StudentPlotPoint((double)lat, (double)lon, AtPickup: false, PickupName: null);
        }

        return null;
    }
}
