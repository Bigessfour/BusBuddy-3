using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
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

    /// <summary>
    /// Writes home and school <see cref="RouteStop"/> rows when the route has no geocoded stop list.
    /// Clerk Optimize Order calls this so visit-order has stops to reorder. Does not replace an existing list.
    /// </summary>
    Task<int> PublishRosterStopsIfMissingAsync(int routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops stops named for a school that is not an active destination, and clears a stored
    /// drive path that still visits that school. Leaves an already-valid path unchanged.
    /// </summary>
    Task<UnlistedSchoolCleanup> OmitUnlistedSchoolsAsync(int routeId, CancellationToken cancellationToken = default);
}

/// <summary>Result of removing a school stop that is not an active catalog destination.</summary>
public sealed class UnlistedSchoolCleanup
{
    public bool Changed { get; init; }

    public string? WaypointsJson { get; init; }

    public string? School { get; init; }
}

public sealed class RouteWaypointRebuildService : IRouteWaypointRebuildService
{
    private static readonly ILogger Logger = Log.ForContext<RouteWaypointRebuildService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IDistrictSettingsAccessor? _districtAccessor;
    private readonly RoutingDistrictSettings _districtSettingsFallback;

    public RouteWaypointRebuildService(
        IBusBuddyDbContextFactory contextFactory,
        IOptions<RoutingDistrictSettings>? districtSettings = null,
        IDistrictSettingsAccessor? districtAccessor = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _districtSettingsFallback = districtSettings?.Value ?? new RoutingDistrictSettings();
        _districtAccessor = districtAccessor;
    }

    private RoutingDistrictSettings DistrictSettings =>
        _districtAccessor?.Current ?? _districtSettingsFallback;

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

        var students = await LoadAssignedStudentsAsync(context, route, cancellationToken).ConfigureAwait(false);
        var schools = await LoadSchoolsAsync(context, cancellationToken).ConfigureAwait(false);
        await OmitUnlistedSchoolsCoreAsync(context, route, students, schools, cancellationToken).ConfigureAwait(false);
        var publishedStops = await context.RouteStops.AsNoTracking()
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.StopOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var routableStops = AssignedRouteStops.ForRouting(publishedStops, students, schools);
        var publishedCoords = routableStops
            .Where(s => s.HasValidatedCoordinates)
            .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
            .ToList();
        if (publishedCoords.Count >= 2)
        {
            var fromStops = RouteWaypointSerializer.FromPairs(publishedCoords);
            route.WaypointsJson = fromStops;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Logger.Information(
                "Rebuilt WaypointsJson from assigned stops RouteId={RouteId} Points={Count} OmittedStudentStops={Omitted}",
                routeId,
                publishedCoords.Count,
                publishedStops.Count - routableStops.Count);
            return fromStops;
        }

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
        else if (DistrictDepot.TryGetCoordinates(DistrictSettings, out var depotLat, out var depotLon))
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
            if (DistrictDepot.TryGetCoordinates(DistrictSettings, out var depotLat, out var depotLon))
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
            if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
            {
                route.WaypointsJson = null;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            Logger.Information(
                "Waypoint rebuild cleared RouteId={RouteId} — need ≥2 points (have {Count})",
                routeId,
                points.Count);
            return null;
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

    public async Task<UnlistedSchoolCleanup> OmitUnlistedSchoolsAsync(
        int routeId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return new UnlistedSchoolCleanup();
        }

        var students = await LoadAssignedStudentsAsync(context, route, cancellationToken).ConfigureAwait(false);
        var schools = await LoadSchoolsAsync(context, cancellationToken).ConfigureAwait(false);
        var changed = await OmitUnlistedSchoolsCoreAsync(context, route, students, schools, cancellationToken)
            .ConfigureAwait(false);
        return new UnlistedSchoolCleanup
        {
            Changed = changed,
            WaypointsJson = route.WaypointsJson,
            School = route.School
        };
    }

    /// <summary>
    /// Removes stops named for an inactive school, and a placeholder barn written at the
    /// clerk map center when the route has no riders. Rewrites the stored path only when
    /// a stop was removed, so a valid Google polyline is left alone.
    /// </summary>
    private static async Task<bool> OmitUnlistedSchoolsCoreAsync(
        BusBuddyDbContext context,
        Route route,
        List<Student> students,
        List<Destination> schools,
        CancellationToken cancellationToken)
    {
        var stops = await context.RouteStops
            .Where(s => s.RouteId == route.RouteId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var routable = AssignedRouteStops.ForRouting(stops, students, schools);
        var remove = stops
            .Where(stop => !routable.Contains(stop) && AssignedRouteStops.NamesCatalogSchool(stop, schools))
            .ToList();
        if (students.Count == 0)
        {
            foreach (var barn in stops.Where(IsPlaceholderBarn))
            {
                if (!remove.Contains(barn))
                {
                    remove.Add(barn);
                }
            }
        }

        var schoolCleared = AlignRouteSchoolWithActiveCatalog(route, students, schools);
        if (remove.Count == 0 && !schoolCleared)
        {
            return false;
        }

        if (remove.Count > 0)
        {
            context.RouteStops.RemoveRange(remove);
            route.StopCount = Math.Max(0, stops.Count - remove.Count);
        }

        var remaining = AssignedRouteStops.ForRouting(stops.Except(remove), students, schools)
            .Where(s => s.HasValidatedCoordinates)
            .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
            .ToList();
        route.WaypointsJson = remaining.Count >= 2
            ? RouteWaypointSerializer.FromPairs(remaining)
            : null;
        var entry = context.Entry(route);
        entry.Property(r => r.School).IsModified = true;
        entry.Property(r => r.WaypointsJson).IsModified = true;
        if (remove.Count > 0)
        {
            entry.Property(r => r.StopCount).IsModified = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Omitted unlisted school stops RouteId={RouteId} Removed={Removed} School={School}",
            route.RouteId,
            string.Join(", ", remove.Select(s => s.StopName)),
            route.School ?? "(none)");
        return true;
    }

    private static bool AlignRouteSchoolWithActiveCatalog(
        Route route,
        IReadOnlyList<Student> students,
        IReadOnlyList<Destination> schools)
    {
        if (string.IsNullOrWhiteSpace(route.School))
        {
            return false;
        }

        var named = ActiveSchoolNamed(schools, route.School);
        if (named is not null)
        {
            return false;
        }

        var attended = students
            .Select(student => ActiveSchoolForStudent(schools, student))
            .Where(school => school is not null)
            .DistinctBy(school => school!.DestinationId)
            .ToList();
        var next = attended.Count == 1 ? attended[0]!.Name : null;
        if (string.Equals(route.School, next, StringComparison.Ordinal))
        {
            return false;
        }

        route.School = next;
        return true;
    }

    private static Destination? ActiveSchoolNamed(IReadOnlyList<Destination> schools, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return schools.FirstOrDefault(school =>
            school.IsActive
            && !school.IsDeleted
            && school.HasValidatedCoordinates
            && DestinationTypes.IsSchool(school.DestinationType)
            && string.Equals(school.Name?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static Destination? ActiveSchoolForStudent(IReadOnlyList<Destination> schools, Student student)
    {
        if (student.DestinationId is int destinationId)
        {
            var byId = schools.FirstOrDefault(school =>
                school.DestinationId == destinationId
                && school.IsActive
                && !school.IsDeleted
                && DestinationTypes.IsSchool(school.DestinationType));
            if (byId is not null)
            {
                return byId;
            }
        }

        return ActiveSchoolNamed(schools, student.School);
    }

    /// <summary>
    /// Seed wrote "District Bus Barn" / "Bus barn" at the clerk camera center when no barn was configured.
    /// That point is a map default, not a stop.
    /// </summary>
    private static bool IsPlaceholderBarn(RouteStop stop) =>
        string.Equals(stop.StopName?.Trim(), "District Bus Barn", StringComparison.OrdinalIgnoreCase)
        && string.Equals(stop.StopAddress?.Trim(), "Bus barn", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<Destination>> LoadSchoolsAsync(
        BusBuddyDbContext context,
        CancellationToken cancellationToken) =>
        await context.Destinations.AsNoTracking()
            .Where(d => d.DestinationType == DestinationTypes.School)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> PublishRosterStopsIfMissingAsync(int routeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateWriteDbContext();
        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return 0;
        }

        var existing = await context.RouteStops
            .Where(s => s.RouteId == routeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var students = await LoadAssignedStudentsAsync(context, route, cancellationToken).ConfigureAwait(false);
        var routableExisting = AssignedRouteStops.ForRouting(existing, students);
        if (routableExisting.Count(s => s.HasValidatedCoordinates) >= 2)
        {
            return 0;
        }

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

        var stops = new List<RouteStop>();
        var isPm = route.RouteName.EndsWith("-PM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(route.Session, "PM", StringComparison.OrdinalIgnoreCase);
        if (!isPm && DistrictDepot.TryGetCoordinates(DistrictSettings, out var depotLat, out var depotLon))
        {
            AddPublishedStop(stops, routeId, DistrictDepot.GetDisplayName(DistrictSettings), "Bus barn", (decimal)depotLat, (decimal)depotLon);
        }

        if (isPm)
        {
            AddPublishedStop(stops, routeId, school?.Name ?? "School", school?.Address, school?.Latitude, school?.Longitude);
        }

        foreach (var student in students)
        {
            AddPublishedStop(
                stops,
                routeId,
                string.IsNullOrWhiteSpace(student.StudentName) ? $"Student {student.StudentId}" : student.StudentName.Trim(),
                student.HomeAddress,
                student.Latitude,
                student.Longitude,
                student.StudentId);
        }

        if (!isPm)
        {
            AddPublishedStop(stops, routeId, school?.Name ?? "School", school?.Address, school?.Latitude, school?.Longitude);
        }
        else if (DistrictDepot.TryGetCoordinates(DistrictSettings, out var endLat, out var endLon))
        {
            AddPublishedStop(stops, routeId, DistrictDepot.GetDisplayName(DistrictSettings), "Bus barn", (decimal)endLat, (decimal)endLon);
        }

        if (stops.Count < 2)
        {
            Logger.Information(
                "Roster stop publish skipped RouteId={RouteId} — need ≥2 validated places (have {Count})",
                routeId,
                stops.Count);
            return 0;
        }

        if (existing.Count > 0)
        {
            context.RouteStops.RemoveRange(existing);
        }

        var order = 1;
        foreach (var stop in stops)
        {
            stop.StopOrder = order++;
            context.RouteStops.Add(stop);
        }

        route.StopCount = stops.Count;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Published roster stops RouteId={RouteId} Stops={Count} Students={Students}",
            routeId,
            stops.Count,
            students.Count);
        return stops.Count;
    }

    private static void AddPublishedStop(
        List<RouteStop> stops,
        int routeId,
        string? name,
        string? address,
        decimal? lat,
        decimal? lon,
        int? studentId = null)
    {
        if (!RouteStop.IsValidatedCoordinate(lat, lon))
        {
            return;
        }

        var latitude = (double)lat!.Value;
        var longitude = (double)lon!.Value;
        var samePlace = stops.FirstOrDefault(s =>
            Math.Abs((double)s.Latitude!.Value - latitude) < 1e-5
            && Math.Abs((double)s.Longitude!.Value - longitude) < 1e-5);
        if (samePlace is not null)
        {
            if (studentId is int id)
            {
                samePlace.Notes = MergeStudentNotes(samePlace.Notes, id);
            }

            return;
        }

        var arrival = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(stops.Count * 5));
        var departure = arrival.Add(TimeSpan.FromMinutes(1));
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var notes = studentId is int riderId ? $"StudentId={riderId}" : null;
        stops.Add(new RouteStop
        {
            RouteId = routeId,
            StopName = string.IsNullOrWhiteSpace(name) ? "Stop" : name.Trim(),
            StopAddress = address?.Trim() ?? string.Empty,
            Latitude = lat,
            Longitude = lon,
            ScheduledArrival = arrival,
            ScheduledDeparture = departure,
            Status = "Scheduled",
            CreatedDate = DateTime.UtcNow,
            EstimatedArrivalTime = day.Add(arrival),
            EstimatedDepartureTime = day.Add(departure),
            Notes = notes
        });
    }

    private static string MergeStudentNotes(string? notes, int studentId)
    {
        var idText = studentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        const string many = "StudentIds=";
        const string single = "StudentId=";
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(notes) && notes.StartsWith(many, StringComparison.Ordinal))
        {
            ids.AddRange(notes[many.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }
        else if (!string.IsNullOrWhiteSpace(notes) && notes.StartsWith(single, StringComparison.Ordinal))
        {
            ids.Add(notes[single.Length..]);
        }
        else if (!string.IsNullOrWhiteSpace(notes))
        {
            return notes;
        }

        if (!ids.Contains(idText, StringComparer.Ordinal))
        {
            ids.Add(idText);
        }

        return ids.Count == 1 ? $"StudentId={ids[0]}" : $"StudentIds={string.Join(",", ids)}";
    }

    private static async Task<List<Student>> LoadAssignedStudentsAsync(
        BusBuddyDbContext context,
        Route route,
        CancellationToken cancellationToken)
    {
        return await context.Students.AsNoTracking()
            .Where(s => s.Active)
            .WhereOnRoute(route)
            .OrderBy(s => s.StudentName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
