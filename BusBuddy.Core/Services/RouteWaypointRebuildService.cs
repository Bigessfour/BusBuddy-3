using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Builds Route.WaypointsJson from assigned students' homes, school destinations,
/// and active school-to-school transfer pickup/dropoff pairs (home→school path).
/// </summary>
public interface IRouteWaypointRebuildService
{
    Task<string?> RebuildAndPersistAsync(int routeId, CancellationToken cancellationToken = default);

    Task RebuildForStudentRoutesAsync(int studentId, CancellationToken cancellationToken = default);
}

public sealed class RouteWaypointRebuildService : IRouteWaypointRebuildService
{
    private static readonly ILogger Logger = Log.ForContext<RouteWaypointRebuildService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly RoutingDistrictSettings _districtSettings;

    public RouteWaypointRebuildService(
        IBusBuddyDbContextFactory contextFactory,
        IOptions<RoutingDistrictSettings>? districtSettings = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _districtSettings = districtSettings?.Value ?? new RoutingDistrictSettings();
    }

    public async Task RebuildForStudentRoutesAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var student = await context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            .ConfigureAwait(false);
        if (student is null)
        {
            return;
        }

        var routeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(student.AMRoute))
        {
            routeNames.Add(student.AMRoute!);
        }

        if (!string.IsNullOrWhiteSpace(student.PMRoute))
        {
            routeNames.Add(student.PMRoute!);
        }

        if (routeNames.Count == 0)
        {
            return;
        }

        var routeIds = await context.Routes.AsNoTracking()
            .Where(r => routeNames.Contains(r.RouteName))
            .Select(r => r.RouteId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var routeId in routeIds)
        {
            await RebuildAndPersistAsync(routeId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string?> RebuildAndPersistAsync(int routeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null || string.IsNullOrWhiteSpace(route.RouteName))
        {
            return null;
        }

        var students = await context.Students.AsNoTracking()
            .Where(s => s.Active &&
                        (s.AMRoute == route.RouteName || s.PMRoute == route.RouteName))
            .OrderBy(s => s.StudentName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentIds = students.Select(s => s.StudentId).ToList();
        var transfers = studentIds.Count == 0
            ? new List<StudentSchoolTransfer>()
            : await context.StudentSchoolTransfers.AsNoTracking()
                .Include(t => t.FromDestination)
                .Include(t => t.ToDestination)
                .Where(t => t.IsActive && studentIds.Contains(t.StudentId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var transfersByStudent = transfers
            .GroupBy(t => t.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.EffectiveDate).First());

        var isPmRoute = route.RouteName.EndsWith("-PM", StringComparison.OrdinalIgnoreCase);
        var points = new List<(double Lat, double Lon)>();

        // Terminal school for home→school / school→home path
        Destination? school = null;
        if (!string.IsNullOrWhiteSpace(route.School))
        {
            school = await context.Destinations.AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.IsActive && !d.IsDeleted && d.Name == route.School,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (school is null)
        {
            var destinationIds = students
                .Where(s => s.DestinationId.HasValue)
                .Select(s => s.DestinationId!.Value)
                .Distinct()
                .ToList();
            if (destinationIds.Count == 1)
            {
                school = await context.Destinations.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DestinationId == destinationIds[0], cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (isPmRoute)
        {
            TryAdd(points, school?.Latitude, school?.Longitude);
        }
        else if (DistrictDepot.TryGetCoordinates(_districtSettings, out var depotLat, out var depotLon))
        {
            TryAdd(points, depotLat, depotLon);
        }

        foreach (var student in students)
        {
            TryAdd(points, student.Latitude, student.Longitude);

            if (!transfersByStudent.TryGetValue(student.StudentId, out var transfer))
            {
                continue;
            }

            // Transfer stop pair: requested pickup → dropoff (coords or school destination GPS)
            var pickupLat = transfer.PickupLatitude ?? transfer.FromDestination?.Latitude;
            var pickupLon = transfer.PickupLongitude ?? transfer.FromDestination?.Longitude;
            var dropLat = transfer.DropoffLatitude ?? transfer.ToDestination?.Latitude;
            var dropLon = transfer.DropoffLongitude ?? transfer.ToDestination?.Longitude;
            TryAdd(points, pickupLat, pickupLon);
            TryAdd(points, dropLat, dropLon);
        }

        if (isPmRoute)
        {
            if (DistrictDepot.TryGetCoordinates(_districtSettings, out var depotLat, out var depotLon))
            {
                TryAdd(points, depotLat, depotLon);
            }
        }
        else
        {
            TryAdd(points, school?.Latitude, school?.Longitude);
        }

        if (points.Count < 2)
        {
            Logger.Information(
                "Waypoint rebuild skipped RouteId={RouteId} — need ≥2 points (have {Count})",
                routeId,
                points.Count);
            return route.WaypointsJson;
        }

        var json = RouteWaypointSerializer.FromPairs(points);
        route.WaypointsJson = json;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Rebuilt WaypointsJson RouteId={RouteId} Points={Count} Students={Students} Transfers={Transfers}",
            routeId,
            points.Count,
            students.Count,
            transfersByStudent.Count);
        return json;
    }

    private static void TryAdd(List<(double Lat, double Lon)> points, decimal? lat, decimal? lon)
    {
        if (!lat.HasValue || !lon.HasValue)
        {
            return;
        }

        TryAdd(points, (double)lat.Value, (double)lon.Value);
    }

    private static void TryAdd(List<(double Lat, double Lon)> points, double lat, double lon)
    {
        var next = (lat, lon);
        if (points.Count > 0)
        {
            var last = points[^1];
            if (Math.Abs(last.Lat - next.Item1) < 1e-7 && Math.Abs(last.Lon - next.Item2) < 1e-7)
            {
                return;
            }
        }

        points.Add(next);
    }
}
