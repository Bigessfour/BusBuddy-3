using System.Diagnostics;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Database-backed route geography for SfMap. Earth Engine is not used.
    /// Street geocoding/routing is Google Maps Platform (spec 007).
    /// </summary>
    public class GeoDataService : IGeoDataService
    {
        private static readonly ILogger Logger = Log.ForContext<GeoDataService>();
        private readonly IBusBuddyDbContextFactory? _contextFactory;

        public GeoDataService(IBusBuddyDbContextFactory? contextFactory = null)
        {
            _contextFactory = contextFactory;
            Logger.Information("GeoDataService constructed HasDbContext={HasDbContext}", contextFactory is not null);
        }

        public async Task<List<Route>> GetRoutesWithGeoDataAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            if (_contextFactory is null)
            {
                Logger.Warning("GetRoutesWithGeoDataAsync skipped — no DbContext factory");
                return [];
            }

            Logger.Information("Loading active routes with geo data from database");
            using var context = _contextFactory.CreateDbContext();
            var routes = await context.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteName)
                .ToListAsync();

            if (routes.Count == 0)
            {
                stopwatch.Stop();
                Logger.Information("No active routes found ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return routes;
            }

            var routeIds = routes.Select(r => r.RouteId).ToList();
            var stops = await context.RouteStops.AsNoTracking()
                .Where(s => routeIds.Contains(s.RouteId) && s.Latitude != null && s.Longitude != null)
                .OrderBy(s => s.RouteId)
                .ThenBy(s => s.StopOrder)
                .ToListAsync();
            var schools = await LoadSchoolsAsync(context).ConfigureAwait(false);
            var students = await context.Students.AsNoTracking().ToListAsync().ConfigureAwait(false);

            var derived = new List<(int RouteId, string Json)>();
            foreach (var route in routes)
            {
                var json = DeriveWaypointJson(RoutableStops(route, stops.Where(s => s.RouteId == route.RouteId), students, schools));
                if (json is null || !string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    continue;
                }

                route.WaypointsJson = json;
                derived.Add((route.RouteId, json));
            }

            await PersistDerivedWaypointsAsync(derived).ConfigureAwait(false);

            stopwatch.Stop();
            Logger.Information(
                "Loaded routes with geo data Routes={RouteCount} StopsWithCoords={StopCount} DerivedWaypoints={Derived} ElapsedMs={ElapsedMs}",
                routes.Count, stops.Count, derived.Count, stopwatch.ElapsedMilliseconds);

            return routes;
        }

        public async Task<Route?> GetRouteGeoDataAsync(int routeId)
        {
            Logger.Information("Loading geo data for route {RouteId}", routeId);
            if (_contextFactory is null)
            {
                Logger.Warning("GetRouteGeoDataAsync skipped — no DbContext factory RouteId={RouteId}", routeId);
                return null;
            }

            using var context = _contextFactory.CreateDbContext();
            var route = await context.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RouteId == routeId);
            if (route is null)
            {
                Logger.Warning("Route {RouteId} not found for geo data", routeId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(route.WaypointsJson))
            {
                var stops = await context.RouteStops.AsNoTracking()
                    .Where(s => s.RouteId == routeId)
                    .OrderBy(s => s.StopOrder)
                    .ToListAsync();
                var schools = await LoadSchoolsAsync(context).ConfigureAwait(false);
                var slot = RouteSession.ToAssignmentSlot(route);
                var students = await context.Students.AsNoTracking()
                    .WhereOnSlot(route.RouteId, route.RouteName ?? string.Empty, slot)
                    .ToListAsync()
                    .ConfigureAwait(false);
                if (await TryFillDerivedWaypointsAsync(route, RoutableStops(route, stops, students, schools)).ConfigureAwait(false))
                {
                    Logger.Information(
                        "Derived and persisted waypoints for route {RouteId} {RouteName}",
                        routeId,
                        route.RouteName);
                }
                else
                {
                    Logger.Information("Route {RouteId} {RouteName} has no stored or stop-derived waypoints", routeId, route.RouteName);
                }
            }
            else
            {
                Logger.Information("Route {RouteId} {RouteName} already has WaypointsJson", routeId, route.RouteName);
            }

            return route;
        }

        public async Task<DistrictMapSnapshot> GetDistrictMapAsync(
            int? routeId,
            CancellationToken cancellationToken = default)
        {
            if (_contextFactory is null)
            {
                Logger.Warning("GetDistrictMapAsync skipped — no DbContext factory");
                return DistrictMapSnapshot.Empty;
            }

            using var context = _contextFactory.CreateDbContext();
            var schools = await LoadSchoolsAsync(context, cancellationToken).ConfigureAwait(false);
            var pickups = await context.PickupStops.AsNoTracking()
                .Where(s => s.Active)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var needs = new List<string>();
            var schoolPins = new List<DistrictMapPlace>();
            foreach (var school in schools.Where(s => s.IsActive))
            {
                if (!school.HasValidatedCoordinates)
                {
                    needs.Add($"School: {DisplayPlaceName(school.Name)}");
                    continue;
                }

                schoolPins.Add(new DistrictMapPlace
                {
                    Id = school.DestinationId,
                    Name = school.Name?.Trim() ?? string.Empty,
                    Latitude = (double)school.Latitude!.Value,
                    Longitude = (double)school.Longitude!.Value
                });
            }

            var catalogPins = new List<DistrictMapPlace>();
            foreach (var stop in pickups)
            {
                if (!stop.HasValidatedCoordinates)
                {
                    needs.Add($"Pickup: {DisplayPlaceName(stop.Name)}");
                    continue;
                }

                catalogPins.Add(new DistrictMapPlace
                {
                    Id = stop.PickupStopId,
                    Name = stop.Name?.Trim() ?? string.Empty,
                    Latitude = (double)stop.Latitude,
                    Longitude = (double)stop.Longitude
                });
            }

            DistrictMapRoute? selected = null;
            if (routeId is int id)
            {
                selected = await LoadSelectedRouteAsync(context, id, schools, pickups, cancellationToken)
                    .ConfigureAwait(false);
            }

            Logger.Information(
                "District map query Schools={Schools} CatalogStops={Stops} NeedsValidation={Needs} RouteId={RouteId} Homes={Homes}",
                schoolPins.Count,
                catalogPins.Count,
                needs.Count,
                routeId,
                selected?.Homes.Count ?? 0);

            return new DistrictMapSnapshot
            {
                Schools = schoolPins,
                CatalogStops = catalogPins,
                NeedsValidation = needs,
                SelectedRoute = selected
            };
        }

        private async Task<DistrictMapRoute?> LoadSelectedRouteAsync(
            BusBuddyDbContext context,
            int routeId,
            IReadOnlyList<Destination> schools,
            IReadOnlyList<PickupStop> pickups,
            CancellationToken cancellationToken)
        {
            var route = await context.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
                .ConfigureAwait(false);
            if (route is null)
            {
                Logger.Warning("District map route {RouteId} not found", routeId);
                return null;
            }

            var stops = await context.RouteStops.AsNoTracking()
                .Where(s => s.RouteId == routeId)
                .OrderBy(s => s.StopOrder)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var slot = RouteSession.ToAssignmentSlot(route);
            var students = await context.Students.AsNoTracking()
                .WhereOnSlot(route.RouteId, route.RouteName ?? string.Empty, slot)
                .OrderBy(s => s.StudentName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (await AlignAssignedHomesAsync(routeId, students, cancellationToken).ConfigureAwait(false))
            {
                route = await context.Routes.AsNoTracking()
                    .FirstAsync(r => r.RouteId == routeId, cancellationToken)
                    .ConfigureAwait(false);
                stops = await context.RouteStops.AsNoTracking()
                    .Where(s => s.RouteId == routeId)
                    .OrderBy(s => s.StopOrder)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var routable = RoutableStops(route, stops, students, schools);
            await TryFillDerivedWaypointsAsync(route, routable).ConfigureAwait(false);

            var pickupIndex = StudentPlotLocation.Index(pickups);
            var homes = new List<DistrictMapHome>();
            foreach (var student in students)
            {
                var pins = StudentPlotLocation.PinsFromStored(student, pickupIndex)
                    .Where(pin => LocationCoordinate.IsValidated(pin.Latitude, pin.Longitude))
                    .ToList();
                if (pins.Count == 0)
                {
                    continue;
                }

                homes.Add(new DistrictMapHome
                {
                    StudentId = student.StudentId,
                    StudentName = string.IsNullOrWhiteSpace(student.StudentName)
                        ? student.StudentNumber ?? "Student"
                        : student.StudentName.Trim(),
                    Pins = pins
                });
            }

            var published = routable
                .Where(s => s.HasValidatedCoordinates)
                .OrderBy(s => s.StopOrder)
                .Select(s => new DistrictMapStop
                {
                    StopOrder = s.StopOrder,
                    Name = string.IsNullOrWhiteSpace(s.StopName) ? $"Stop {s.StopOrder}" : s.StopName.Trim(),
                    Latitude = (double)s.Latitude!.Value,
                    Longitude = (double)s.Longitude!.Value
                })
                .ToList();

            return new DistrictMapRoute
            {
                RouteId = route.RouteId,
                RouteName = route.RouteName,
                WaypointsJson = route.WaypointsJson,
                DistanceMiles = route.Distance,
                DurationMinutes = route.EstimatedDuration,
                PublishedStops = published,
                Homes = homes
            };
        }

        private static Task<List<Destination>> LoadSchoolsAsync(
            BusBuddyDbContext context,
            CancellationToken cancellationToken = default) =>
            context.Destinations.AsNoTracking()
                .Where(d => !d.IsDeleted && d.DestinationType == DestinationTypes.School)
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken);

        /// <summary>
        /// Copies stored home coordinates onto that student's published stops and drops a drive
        /// path that still visits the old place. Catalog riders are left on their shared stop.
        /// </summary>
        private async Task<bool> AlignAssignedHomesAsync(
            int routeId,
            IReadOnlyList<Student> students,
            CancellationToken cancellationToken)
        {
            if (_contextFactory is null || students.Count == 0)
            {
                return false;
            }

            await using var write = _contextFactory.CreateWriteDbContext();
            var changed = false;
            foreach (var student in students)
            {
                if (await AssignedHomeStopSync.ApplyAsync(write, student, routeId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            await write.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Stop-derived path uses the same roster filter as the map pins and the printed sheet.
        /// A student-home stop stays off the path unless that student is assigned to the route.
        /// </summary>
        private static List<RouteStop> RoutableStops(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            IReadOnlyList<Destination> schools)
        {
            var slot = RouteSession.ToAssignmentSlot(route);
            var roster = students.Where(s => StudentRouteAssignment.Matches(s, route, slot)).ToList();
            return AssignedRouteStops.ForRouting(stops, roster, schools).ToList();
        }

        private static string? DeriveWaypointJson(IEnumerable<RouteStop> stops)
        {
            var json = RouteWaypointSerializer.FromPairs(
                stops
                    .Where(s => LocationCoordinate.IsValidated(s.Latitude, s.Longitude))
                    .OrderBy(s => s.StopOrder)
                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
            return json == "[]" ? null : json;
        }

        /// <summary>
        /// Writes stop-derived pairs only when <see cref="Route.WaypointsJson"/> is empty.
        /// A stored drive path is left alone.
        /// </summary>
        private async Task<bool> TryFillDerivedWaypointsAsync(Route route, IEnumerable<RouteStop> stops)
        {
            if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
            {
                return false;
            }

            var json = DeriveWaypointJson(stops);
            if (json is null)
            {
                return false;
            }

            route.WaypointsJson = json;
            await PersistDerivedWaypointsAsync([(route.RouteId, json)]).ConfigureAwait(false);
            return true;
        }

        private static string DisplayPlaceName(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();

        private async Task PersistDerivedWaypointsAsync(IReadOnlyList<(int RouteId, string Json)> derived)
        {
            if (derived.Count == 0 || _contextFactory is null)
            {
                return;
            }

            try
            {
                await using var write = _contextFactory.CreateWriteDbContext();
                var ids = derived.Select(d => d.RouteId).ToList();
                var tracked = await write.Routes.Where(r => ids.Contains(r.RouteId)).ToListAsync();
                var byId = derived.ToDictionary(d => d.RouteId, d => d.Json);
                var updated = 0;
                foreach (var route in tracked)
                {
                    if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                    {
                        // Drive-path / stored geometry wins — never overwrite.
                        continue;
                    }

                    if (!byId.TryGetValue(route.RouteId, out var json))
                    {
                        continue;
                    }

                    route.WaypointsJson = json;
                    updated++;
                }

                if (updated > 0)
                {
                    await write.SaveChangesAsync();
                }

                Logger.Information("Persisted derived WaypointsJson Routes={Updated}", updated);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Persisting derived waypoints failed — in-memory JSON kept");
            }
        }
    }
}
