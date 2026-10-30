using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusBuddy.DbPrep;

/// <summary>
/// Clerk Optimize Order for general-ed routes that have riders and no published stops.
/// Pins the school and the farthest home, then uses Route Optimization for the pickups
/// between them. Prints counts and times only.
/// </summary>
internal static class RosterRouteOptimization
{
    public static async Task<int> RunAsync(BusBuddyDbContextFactory factory)
    {
        var maps = new GoogleMapsOptions { EnableUspsCass = true, RegionCode = "US" };
        maps.Normalize();
        var options = Options.Create(maps);
        using var routing = new GoogleRoutingService(new HttpClient { Timeout = TimeSpan.FromSeconds(90) }, options, ownsHttpClient: true);
        using var optimization = new GoogleRouteOptimizationService(new HttpClient { Timeout = TimeSpan.FromSeconds(90) }, options, ownsHttpClient: true);
        var rebuild = new RouteWaypointRebuildService(factory);
        var routes = new RouteService(factory, rebuild, null, routing);
        var clocks = new RouteDeterminationService(
            factory,
            routes,
            waypointRebuild: rebuild,
            routeOptimization: optimization,
            routing: routing);

        if (!optimization.IsConfigured)
        {
            Console.Error.WriteLine("Route Optimization is not configured.");
            return 2;
        }

        List<int> routeIds;
        await using (var read = factory.CreateDbContext())
        {
            routeIds = await read.Routes.AsNoTracking()
                .Where(r => r.IsActive && !r.IsSpecialNeedsRoute)
                .OrderBy(r => r.RouteName)
                .Select(r => r.RouteId)
                .ToListAsync();
        }

        Console.WriteLine($"General-ed routes: {routeIds.Count}");
        var failed = 0;
        foreach (var routeId in routeIds)
        {
            if (!await OptimizeOneAsync(factory, rebuild, routes, optimization, clocks, routeId))
            {
                failed++;
            }
        }

        Console.WriteLine(failed == 0
            ? "Optimize Order finished."
            : $"Optimize Order finished with {failed} route(s) unchanged.");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<bool> OptimizeOneAsync(
        BusBuddyDbContextFactory factory,
        RouteWaypointRebuildService rebuild,
        RouteService routes,
        GoogleRouteOptimizationService optimization,
        RouteDeterminationService clocks,
        int routeId)
    {
        string name;
        string session;
        int riders;
        int unpinned;
        await using (var read = factory.CreateDbContext())
        {
            var route = await read.Routes.AsNoTracking().FirstAsync(r => r.RouteId == routeId);
            name = route.RouteName;
            session = route.Session;
            if (route.WaypointsJson?.Contains("encodedPolyline", StringComparison.Ordinal) == true)
            {
                Console.WriteLine($"{name} ({session}): already has a road path. Skipped.");
                return true;
            }

            var assigned = await read.Students.AsNoTracking().Where(s => s.Active).WhereOnRoute(route).ToListAsync();
            riders = assigned.Count;
            unpinned = assigned.Count(s => s.Latitude is null || s.Longitude is null);
        }

        if (riders == 0)
        {
            Console.WriteLine($"{name} ({session}): no riders");
            return true;
        }

        var published = await rebuild.PublishRosterStopsIfMissingAsync(routeId);
        var afternoon = string.Equals(session, "PM", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("-PM", StringComparison.OrdinalIgnoreCase);
        var pinned = await PinSchoolAndOuterHomeAsync(factory, routeId, afternoon);
        if (!pinned)
        {
            Console.WriteLine($"{name} ({session}): no school stop to pin. Published {published}. Unpinned riders {unpinned}.");
            return false;
        }

        List<RouteStop> stops;
        await using (var read = factory.CreateDbContext())
        {
            stops = await read.RouteStops.AsNoTracking()
                .Where(s => s.RouteId == routeId)
                .OrderBy(s => s.StopOrder)
                .ToListAsync();
        }

        var validated = stops.Where(s => s.HasValidatedCoordinates).ToList();
        if (validated.Count == 2)
        {
            var refresh = await routes.RefreshDrivePathAsync(routeId);
            if (!refresh.IsSuccess)
            {
                Console.WriteLine($"{name} ({session}): two stops, drive path not saved. {refresh.Error}");
            }

            var twoStopClocks = await clocks.ApplyPublishedClocksAsync(routeId);
            await PrintSummaryAsync(factory, routeId, name, session, "two-stop", unpinned, twoStopClocks);
            return twoStopClocks.RoutesUpdated > 0;
        }

        var orderSource = "Google";
        IReadOnlyList<int> orderIds;
        if (validated.Count >= 3)
        {
            var ordered = await RouteStopOrderPlanner.ComputePinnedOrderAsync(
                validated,
                optimization,
                seatingCapacity: 72,
                DateTime.UtcNow);
            if (ordered.IsSuccess && ordered.Value is not null)
            {
                orderIds = AssignedRouteStops.OrderPreservingUnroutable(stops, ordered.Value.ToList());
            }
            else
            {
                orderSource = "nearest";
                orderIds = NearestPickupOrder(validated);
                Console.WriteLine($"{name}: Route Optimization skipped ({ordered.Error}). Using nearest-pickup order.");
            }
        }
        else
        {
            Console.WriteLine($"{name} ({session}): {validated.Count} geocoded stops. Need at least 3. Unpinned riders {unpinned}.");
            return false;
        }

        var reorder = await routes.ReorderRouteStopsAsync(routeId, orderIds.ToList());
        if (!reorder.IsSuccess)
        {
            Console.WriteLine($"{name} ({session}): could not save stop order. {reorder.Error}");
            return false;
        }

        var timed = await clocks.ApplyPublishedClocksAsync(routeId);
        await PrintSummaryAsync(factory, routeId, name, session, orderSource, unpinned, timed);
        return timed.RoutesUpdated > 0;
    }

    private static async Task<bool> PinSchoolAndOuterHomeAsync(
        BusBuddyDbContextFactory factory,
        int routeId,
        bool afternoon)
    {
        await using var context = factory.CreateWriteDbContext();
        var route = await context.Routes.FirstAsync(r => r.RouteId == routeId);
        var stops = await context.RouteStops.Where(s => s.RouteId == routeId).ToListAsync();
        var schoolNames = await context.Destinations.AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
            .Select(d => d.Name)
            .ToListAsync();
        var school = stops.FirstOrDefault(s =>
            s.HasValidatedCoordinates &&
            schoolNames.Any(n => string.Equals(n, s.StopName, StringComparison.OrdinalIgnoreCase)));
        var homes = stops.Where(s => s.HasValidatedCoordinates && !ReferenceEquals(s, school)).ToList();
        if (school is null || homes.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(route.School))
        {
            route.School = school.StopName;
        }

        var outer = homes
            .OrderByDescending(h => Miles(h, school))
            .First();
        var middle = homes
            .Where(h => h.RouteStopId != outer.RouteStopId)
            .OrderBy(h => h.StopOrder)
            .ToList();
        var sequence = afternoon
            ? new[] { school }.Concat(middle).Append(outer)
            : new[] { outer }.Concat(middle).Append(school);
        var order = 1;
        foreach (var stop in sequence)
        {
            stop.StopOrder = order++;
        }

        await context.SaveChangesAsync();
        return true;
    }

    private static List<int> NearestPickupOrder(IReadOnlyList<RouteStop> validated)
    {
        var ordered = validated.OrderBy(s => s.StopOrder).ToList();
        var start = ordered[0];
        var end = ordered[^1];
        var remaining = ordered.Skip(1).Take(ordered.Count - 2).ToList();
        var sequence = new List<RouteStop> { start };
        var cursor = start;
        while (remaining.Count > 0)
        {
            var next = remaining.OrderBy(s => Miles(cursor, s)).First();
            sequence.Add(next);
            remaining.Remove(next);
            cursor = next;
        }

        sequence.Add(end);
        return sequence.Select(s => s.RouteStopId).ToList();
    }

    private static async Task PrintSummaryAsync(
        BusBuddyDbContextFactory factory,
        int routeId,
        string name,
        string session,
        string orderSource,
        int unpinned,
        RouteGenerationResult timed)
    {
        await using var read = factory.CreateDbContext();
        var route = await read.Routes.AsNoTracking().FirstAsync(r => r.RouteId == routeId);
        var stops = await read.RouteStops.AsNoTracking()
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.StopOrder)
            .ToListAsync();
        var schoolNames = await read.Destinations.AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
            .Select(d => d.Name)
            .ToListAsync();
        var school = stops.FirstOrDefault(s =>
            schoolNames.Any(n => string.Equals(n, s.StopName, StringComparison.OrdinalIgnoreCase)));
        var firstPickup = string.Equals(session, "PM", StringComparison.OrdinalIgnoreCase)
            ? school
            : stops.FirstOrDefault(s => s.RouteStopId != school?.RouteStopId);
        var road = route.WaypointsJson?.Contains("encodedPolyline", StringComparison.Ordinal) == true;
        var clock = timed.RoutesUpdated > 0
            ? timed.Estimated ? "estimate" : "road"
            : timed.Error ?? "unchanged";
        Console.WriteLine(
            $"{name} ({session}): stops={stops.Count} order={orderSource} path={(road ? "road" : "straight")} " +
            $"miles={route.Distance} minutes={route.EstimatedDuration} " +
            $"first={Format(firstPickup?.ScheduledArrival)} school={Format(school?.ScheduledArrival)} " +
            $"clocks={clock} riders-without-pin={unpinned}");
    }

    private static string Format(TimeSpan? time) =>
        time is TimeSpan value ? timeValue(value) : "—";

    private static string timeValue(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}";

    private static double Miles(RouteStop a, RouteStop b)
    {
        const double earth = 3958.8;
        var lat1 = Rad((double)a.Latitude!.Value);
        var lat2 = Rad((double)b.Latitude!.Value);
        var dLat = lat2 - lat1;
        var dLon = Rad((double)b.Longitude!.Value - (double)a.Longitude!.Value);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earth * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180d;
}
