using System.Globalization;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// A home or special-needs pickup is the student's stored home. Published stops that
/// name only that student follow the home coordinate, and the stored drive path is
/// dropped so the map is drawn from the new place. A catalog stop that other riders
/// still use stays where it is.
/// </summary>
public static class AssignedHomeStopSync
{
    private static readonly ILogger Logger = Log.ForContext(typeof(AssignedHomeStopSync));

    /// <summary>
    /// Catalog corner only when the student is not special needs and a catalog stop is assigned.
    /// Special needs is home pickup even if a catalog id is still on the row.
    /// </summary>
    public static bool UseCatalogPickup(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        return !StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student)
            && student.PickupStopId is > 0;
    }

    public static bool FollowsValidatedHome(Student student) =>
        !UseCatalogPickup(student) && student.HasValidatedHomeCoordinates;

    /// <summary>
    /// Updates this student's published home stops. Does not save.
    /// </summary>
    public static async Task<bool> ApplyAsync(
        BusBuddyDbContext context,
        Student student,
        int? alsoOnRouteId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(student);
        if (student.StudentId <= 0 || !FollowsValidatedHome(student))
        {
            return false;
        }

        var routeIds = new[] { student.AmRouteId, student.PmRouteId }
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (alsoOnRouteId is > 0 && !routeIds.Contains(alsoOnRouteId.Value))
        {
            routeIds.Add(alsoOnRouteId.Value);
        }

        if (routeIds.Count == 0)
        {
            return false;
        }

        // Caller contexts are often NoTracking. Track these rows so the move is saved.
        var stops = await context.RouteStops
            .AsTracking()
            .Where(s => routeIds.Contains(s.RouteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var geometryChanged = new HashSet<int>();
        var touched = false;
        foreach (var stop in stops.ToList())
        {
            if (!NamesStudent(stop, student))
            {
                continue;
            }

            var ids = RouteSummarySheetBuilder.ParseStudentIds(stop.Notes);
            if (ids.Count > 1)
            {
                if (SplitOffSharedStop(context, stops, stop, student))
                {
                    geometryChanged.Add(stop.RouteId);
                    touched = true;
                }

                continue;
            }

            var edit = MoveExclusive(stop, student);
            if (edit.Changed)
            {
                touched = true;
            }

            if (edit.Geometry)
            {
                geometryChanged.Add(stop.RouteId);
            }
        }

        var routes = await context.Routes
            .AsTracking()
            .Where(r => routeIds.Contains(r.RouteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var route in routes)
        {
            var homes = stops.Where(s =>
                s.RouteId == route.RouteId
                && context.Entry(s).State != EntityState.Deleted
                && NamesStudent(s, student)
                && s.HasValidatedCoordinates);
            if (!DrivePathMissesHome(route, homes))
            {
                continue;
            }

            route.WaypointsJson = null;
            route.StopCount = stops.Count(s =>
                s.RouteId == route.RouteId
                && context.Entry(s).State != EntityState.Deleted);
            geometryChanged.Add(route.RouteId);
            touched = true;
        }

        if (!touched)
        {
            return false;
        }

        if (geometryChanged.Count > 0)
        {
            Logger.Information(
                "Home pickup aligned to stored coordinates StudentId={StudentId} Routes={RouteIds}",
                student.StudentId,
                string.Join(",", geometryChanged));
        }

        return true;
    }

    /// <summary>
    /// The stored line still visits the old place when this student's home is missing from it.
    /// </summary>
    private static bool DrivePathMissesHome(Route route, IEnumerable<RouteStop> homeStops)
    {
        var homes = homeStops.ToList();
        if (homes.Count == 0 || string.IsNullOrWhiteSpace(route.WaypointsJson))
        {
            return false;
        }

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        var stored = payload.Stops.Count > 0 ? payload.Stops : payload.Points;
        if (stored.Count == 0)
        {
            return true;
        }

        return homes.Any(home => stored.All(point =>
            !Near(point.Latitude, point.Longitude, (double)home.Latitude!.Value, (double)home.Longitude!.Value)));
    }

    private static bool Near(double lat, double lon, double otherLat, double otherLon) =>
        Math.Abs(lat - otherLat) < 0.00005 && Math.Abs(lon - otherLon) < 0.00005;

    private static bool NamesStudent(RouteStop stop, Student student)
    {
        var ids = RouteSummarySheetBuilder.ParseStudentIds(stop.Notes);
        if (ids.Contains(student.StudentId))
        {
            return true;
        }

        if (ids.Count > 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(student.StudentName)
            && string.Equals(stop.StopName?.Trim(), student.StudentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SameHomeAddress(stop.StopAddress, student.HomeAddress);
    }

    /// <summary>
    /// Published home stops on older routes have the street address and no StudentId note.
    /// </summary>
    private static bool SameHomeAddress(string? stopAddress, string? homeAddress)
    {
        var stop = NormalizeAddress(stopAddress);
        var home = NormalizeAddress(homeAddress);
        if (stop.Length < 6 || home.Length < 6)
        {
            return false;
        }

        return stop.Contains(home, StringComparison.OrdinalIgnoreCase)
            || home.Contains(stop, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private readonly record struct StopEdit(bool Changed, bool Geometry);

    private static StopEdit MoveExclusive(RouteStop stop, Student student)
    {
        var changed = false;
        var geometry = false;
        if (!SamePlace(stop.Latitude, stop.Longitude, student.Latitude, student.Longitude))
        {
            stop.Latitude = student.Latitude;
            stop.Longitude = student.Longitude;
            changed = true;
            geometry = true;
        }

        if (!string.IsNullOrWhiteSpace(student.HomeAddress)
            && !string.Equals(stop.StopAddress, student.HomeAddress, StringComparison.Ordinal))
        {
            stop.StopAddress = student.HomeAddress;
            changed = true;
        }

        var name = DisplayName(student);
        if (!string.Equals(stop.StopName, name, StringComparison.Ordinal))
        {
            stop.StopName = name;
            changed = true;
        }

        if (changed)
        {
            stop.UpdatedDate = DateTime.UtcNow;
        }

        return new StopEdit(changed, geometry);
    }

    private static bool SplitOffSharedStop(
        BusBuddyDbContext context,
        List<RouteStop> stops,
        RouteStop shared,
        Student student)
    {
        var remaining = RouteSummarySheetBuilder.ParseStudentIds(shared.Notes)
            .Where(id => id != student.StudentId)
            .ToList();
        shared.Notes = remaining.Count switch
        {
            0 => null,
            1 => $"StudentId={remaining[0]}",
            _ => "StudentIds=" + string.Join(",", remaining)
        };
        shared.UpdatedDate = DateTime.UtcNow;

        if (remaining.Count == 0)
        {
            context.RouteStops.Remove(shared);
        }

        var dedicated = stops.FirstOrDefault(s =>
            s.RouteId == shared.RouteId
            && s != shared
            && string.Equals(s.Notes, $"StudentId={student.StudentId}", StringComparison.Ordinal));
        if (dedicated is not null)
        {
            MoveExclusive(dedicated, student);
            return true;
        }

        var insertAt = shared.StopOrder;
        if (remaining.Count > 0)
        {
            foreach (var later in stops.Where(s => s.RouteId == shared.RouteId && s.StopOrder >= insertAt))
            {
                later.StopOrder++;
            }
        }

        var home = new RouteStop
        {
            RouteId = shared.RouteId,
            StopName = DisplayName(student),
            StopAddress = student.HomeAddress ?? string.Empty,
            Latitude = student.Latitude,
            Longitude = student.Longitude,
            StopOrder = insertAt,
            ScheduledArrival = shared.ScheduledArrival,
            ScheduledDeparture = shared.ScheduledDeparture,
            EstimatedArrivalTime = shared.EstimatedArrivalTime,
            EstimatedDepartureTime = shared.EstimatedDepartureTime,
            Notes = $"StudentId={student.StudentId}",
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        stops.Add(home);
        context.RouteStops.Add(home);
        return true;
    }

    private static string DisplayName(Student student) =>
        string.IsNullOrWhiteSpace(student.StudentName)
            ? string.Create(CultureInfo.InvariantCulture, $"Student {student.StudentId}")
            : student.StudentName.Trim();

    private static bool SamePlace(decimal? lat, decimal? lon, decimal? otherLat, decimal? otherLon)
    {
        if (lat is null || lon is null || otherLat is null || otherLon is null)
        {
            return false;
        }

        return Math.Abs(lat.Value - otherLat.Value) < 0.000001m
            && Math.Abs(lon.Value - otherLon.Value) < 0.000001m;
    }
}
