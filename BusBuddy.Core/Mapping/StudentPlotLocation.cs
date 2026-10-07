using BusBuddy.Core.Models;
using BusBuddy.Core.Services;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Where a student pins on the district map.
/// Pickup stop with GPS → PK at the stop, plus an optional home pin when stored home GPS differs.
/// Otherwise home GPS only (geocoding is a separate step when this returns empty).
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

    private const double SameSpotDegrees = 0.00005;

    public static StudentPlotPoint? TryFromStored(
        Student student,
        IReadOnlyDictionary<int, PickupStop>? pickups)
    {
        var pins = PinsFromStored(student, pickups);
        return pins.Count == 0 ? null : pins[0];
    }

    /// <summary>
    /// Boarding pin first when the assigned catalog stop has coordinates, then a home pin
    /// when stored home coordinates differ from the stop. No pickup GPS → home only.
    /// </summary>
    public static IReadOnlyList<StudentPlotPoint> PinsFromStored(
        Student student,
        IReadOnlyDictionary<int, PickupStop>? pickups)
    {
        ArgumentNullException.ThrowIfNull(student);
        pickups ??= new Dictionary<int, PickupStop>();

        var pins = new List<StudentPlotPoint>(2);
        var stop = AssignedStop(student, pickups);
        if (stop is { HasValidatedCoordinates: true } && AssignedHomeStopSync.UseCatalogPickup(student))
        {
            pins.Add(new StudentPlotPoint(
                (double)stop.Latitude,
                (double)stop.Longitude,
                AtPickup: true,
                PickupName: stop.Name));
        }

        if (student.HasValidatedHomeCoordinates)
        {
            var home = new StudentPlotPoint(
                (double)student.Latitude!,
                (double)student.Longitude!,
                AtPickup: false,
                PickupName: null);
            if (pins.Count == 0 || !SameSpot(pins[0].Latitude, pins[0].Longitude, home.Latitude, home.Longitude))
            {
                pins.Add(home);
            }
        }

        return pins;
    }

    public static bool SameSpot(double lat1, double lon1, double lat2, double lon2) =>
        Math.Abs(lat1 - lat2) < SameSpotDegrees && Math.Abs(lon1 - lon2) < SameSpotDegrees;

    private static PickupStop? AssignedStop(Student student, IReadOnlyDictionary<int, PickupStop> pickups)
    {
        if (student.PickupStopId is not int id)
        {
            return null;
        }

        if (pickups.TryGetValue(id, out var stop))
        {
            return stop;
        }

        return student.PickupStop is { } nav && nav.PickupStopId == id ? nav : null;
    }
}
