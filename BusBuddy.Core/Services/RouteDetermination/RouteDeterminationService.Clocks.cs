using System.Text.RegularExpressions;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services.RouteDetermination;

public sealed partial class RouteDeterminationService
{
    private static readonly Regex StudentIdNotes = new(
        @"StudentIds?\s*=\s*(\d+(?:\s*,\s*\d+)*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<RouteGenerationResult> ApplyPublishedClocksAsync(
        int routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return ClockFailure("Route id is required.");
        }

        await using var context = _contextFactory.CreateDbContext();
        var route = await context.Routes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return ClockFailure("Route not found.");
        }

        var stops = await context.RouteStops.AsNoTracking()
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.StopOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (stops.Count == 0)
        {
            return ClockFailure("Route has no stops.");
        }

        var riders = await context.Students.AsNoTracking()
            .WhereOnRoute(route)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var schools = await context.Destinations.AsNoTracking()
            .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ResolveAfternoon(route, riders) is not bool afternoon)
        {
            return ClockFailure(
                "This special-needs route has morning and afternoon riders. Split them before publishing clocks.");
        }

        var dwell = TimeSpan.FromMinutes(DwellMinutes());
        var classified = stops
            .Select(stop => ClassifyStop(stop, schools, riders, afternoon))
            .ToList();
        var (legs, estimated) = await LegSecondsAsync(stops, routeId, cancellationToken).ConfigureAwait(false);
        var plan = PublishedClockPlanner.Plan(classified, legs, dwell, afternoon, estimated);
        if (!plan.Success || plan.BeginTime is not TimeSpan begin || plan.Arrivals.Count != stops.Count)
        {
            return ClockFailure(FirstWarning(plan.Warnings), plan.Warnings);
        }

        for (var i = 0; i < stops.Count; i++)
        {
            stops[i].ScheduledArrival = plan.Arrivals[i];
            stops[i].ScheduledDeparture = plan.Departures[i];
            stops[i].EstimatedArrivalTime = DistrictWallClock(plan.Arrivals[i]);
            stops[i].EstimatedDepartureTime = DistrictWallClock(plan.Departures[i]);
        }

        var persist = await _routeService.UpdateRouteStopsTimingAsync(routeId, stops).ConfigureAwait(false);
        if (!persist.IsSuccess)
        {
            return ClockFailure(persist.Error ?? "Timing persist failed.");
        }

        await using (var write = _contextFactory.CreateWriteDbContext())
        {
            var tracked = await write.Routes
                .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
                .ConfigureAwait(false);
            if (tracked is not null)
            {
                if (afternoon)
                {
                    tracked.PMBeginTime = begin;
                }
                else
                {
                    tracked.AMBeginTime = begin;
                }

                await write.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        Logger.Information(
            "Published clocks RouteId={RouteId} Stops={Stops} Begin={Begin} Estimated={Estimated} Afternoon={Afternoon}",
            routeId,
            stops.Count,
            begin,
            plan.Estimated,
            afternoon);

        return new RouteGenerationResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            RoutesUpdated = 1,
            BeginTime = begin,
            Estimated = plan.Estimated,
            Warnings = plan.Warnings.ToList(),
            FleetKind = FleetKind.HomeToSchool
        };
    }

    private async Task<bool> RouteServesSchoolAsync(
        BusBuddyDbContext context,
        Route route,
        Destination school,
        CancellationToken cancellationToken)
    {
        var hasRider = await context.Students.AsNoTracking()
            .Where(s => s.DestinationId == school.DestinationId)
            .WhereOnRoute(route)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (hasRider)
        {
            return true;
        }

        var stops = await context.RouteStops.AsNoTracking()
            .Where(s => s.RouteId == route.RouteId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return stops.Any(stop => StopMatchesSchool(stop, school));
    }

    private async Task<(int[] Seconds, bool Estimated)> LegSecondsAsync(
        IReadOnlyList<RouteStop> stops,
        int routeId,
        CancellationToken cancellationToken)
    {
        if (stops.Count >= 2
            && _routing is not null
            && stops.All(HasPoint))
        {
            try
            {
                var origin = ((double)stops[0].Latitude!.Value, (double)stops[0].Longitude!.Value);
                var destination = ((double)stops[^1].Latitude!.Value, (double)stops[^1].Longitude!.Value);
                var waypoints = stops
                    .Skip(1)
                    .Take(stops.Count - 2)
                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
                    .ToList();
                var path = await _routing.ComputeDrivePathAsync(origin, destination, waypoints, cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrEmpty(path.Error) && path.LegDurationSeconds.Count == stops.Count - 1)
                {
                    return (path.LegDurationSeconds.ToArray(), false);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Published clock legs failed RouteId={RouteId}", routeId);
            }
        }

        return (HaversineLegs(stops), true);
    }

    private int[] HaversineLegs(IReadOnlyList<RouteStop> stops)
    {
        var speed = District.AverageSpeedMph <= 0 ? 25d : District.AverageSpeedMph;
        var legs = new int[Math.Max(0, stops.Count - 1)];
        for (var i = 0; i < legs.Length; i++)
        {
            if (!TryPoint(stops[i], out var aLat, out var aLon) || !TryPoint(stops[i + 1], out var bLat, out var bLon))
            {
                legs[i] = 0;
                continue;
            }

            var miles = RoutePacker.HaversineMiles(aLat, aLon, bLat, bLon);
            legs[i] = (int)Math.Round(miles / speed * 3600d, MidpointRounding.AwayFromZero);
        }

        return legs;
    }

    private int DwellMinutes()
    {
        var minutes = District.StopDwellMinutes;
        return minutes < 1 ? RoutingDistrictSettings.DefaultStopDwellMinutes : minutes;
    }

    private ClockStop ClassifyStop(
        RouteStop stop,
        IReadOnlyList<Destination> schools,
        IReadOnlyList<Student> riders,
        bool afternoon)
    {
        if (IsDepotStop(stop))
        {
            return new ClockStop(ClockStopKind.Depot, null, null, Array.Empty<int>());
        }

        var school = schools
            .Where(candidate => StopMatchesSchool(stop, candidate))
            .OrderByDescending(candidate => candidate.Name?.Length ?? 0)
            .FirstOrDefault();
        if (school is not null)
        {
            var bell = afternoon ? school.DismissalTime : school.StartTime;
            return new ClockStop(ClockStopKind.School, school.DestinationId, bell, Array.Empty<int>());
        }

        return new ClockStop(ClockStopKind.Pickup, null, null, RiderSchoolIds(stop, riders));
    }

    private bool IsDepotStop(RouteStop stop)
    {
        var district = District;
        if (NamesEqual(stop.StopName, DistrictDepot.GetDisplayName(district))
            || NamesEqual(stop.StopName, district.DepotName)
            || AddressesOverlap(stop.StopAddress, district.DepotAddress))
        {
            return true;
        }

        if (!DistrictDepot.TryGetCoordinates(district, out var lat, out var lon)
            || stop.Latitude is not decimal stopLat
            || stop.Longitude is not decimal stopLon)
        {
            return false;
        }

        return Math.Abs((double)stopLat - lat) <= 0.0005
            && Math.Abs((double)stopLon - lon) <= 0.0005;
    }

    private static bool StopMatchesSchool(RouteStop stop, Destination school) =>
        NamesEqual(stop.StopName, school.Name) || AddressesOverlap(stop.StopAddress, school.Address);

    private static List<int> RiderSchoolIds(RouteStop stop, IReadOnlyList<Student> riders)
    {
        var noteIds = StudentIdsFromNotes(stop.Notes);
        var schools = new HashSet<int>();
        foreach (var rider in riders)
        {
            var matched = noteIds.Contains(rider.StudentId)
                || NamesEqual(stop.StopName, rider.StudentName)
                || AddressesOverlap(stop.StopAddress, rider.HomeAddress);
            if (matched && rider.DestinationId is int destinationId)
            {
                schools.Add(destinationId);
            }
        }

        return schools.ToList();
    }

    private static HashSet<int> StudentIdsFromNotes(string? notes)
    {
        var ids = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(notes))
        {
            return ids;
        }

        foreach (Match match in StudentIdNotes.Matches(notes))
        {
            foreach (var piece in match.Groups[1].Value.Split(','))
            {
                if (int.TryParse(piece.Trim(), out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>Null when a special-needs route mixes morning and afternoon riders.</summary>
    private static bool? ResolveAfternoon(Route route, IReadOnlyList<Student> riders)
    {
        var session = RouteSession.Canonical(route.Session);
        if (session == RouteSession.PM)
        {
            return true;
        }

        if (session is RouteSession.AM or RouteSession.Transfer)
        {
            return false;
        }

        var morning = riders.Any(r => r.RidesAm && !r.RidesPm);
        var afternoon = riders.Any(r => r.RidesPm && !r.RidesAm);
        var mixed = riders.Any(r => r.RidesAm && r.RidesPm) || (morning && afternoon);
        if (mixed)
        {
            return null;
        }

        if (afternoon)
        {
            return true;
        }

        if (morning || riders.Count > 0)
        {
            return false;
        }

        if (route.PMBeginTime is not null && route.AMBeginTime is null)
        {
            return true;
        }

        return false;
    }

    private static bool HasPoint(RouteStop stop) =>
        TryPoint(stop, out _, out _);

    private static bool TryPoint(RouteStop stop, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (stop.Latitude is not decimal lat || stop.Longitude is not decimal lon
            || !RouteStop.IsValidatedCoordinate(lat, lon))
        {
            return false;
        }

        latitude = (double)lat;
        longitude = (double)lon;
        return true;
    }

    private static bool NamesEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool AddressesOverlap(string? left, string? right)
    {
        var a = left?.Trim();
        var b = right?.Trim();
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || a.Length < 6 || b.Length < 6)
        {
            return false;
        }

        return a.Contains(b, StringComparison.OrdinalIgnoreCase)
            || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstWarning(IReadOnlyList<string> warnings) =>
        warnings.Count == 0 ? "Clock plan failed." : warnings[0];

    private static RouteGenerationResult ClockFailure(string error, IReadOnlyList<string>? warnings = null) =>
        new()
        {
            OperationId = Guid.NewGuid(),
            Success = false,
            RoutesUpdated = 0,
            Error = error,
            Warnings = warnings?.ToList() ?? new List<string> { error },
            FleetKind = FleetKind.HomeToSchool
        };
}
