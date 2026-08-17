using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Assign-time seating / time / geo fitness (spec 008 US2, Q2:B).</summary>
public sealed class AssignFitnessEvaluator
{
    private static readonly ILogger Logger = Log.ForContext<AssignFitnessEvaluator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly RoutingDistrictSettings _settings;

    public AssignFitnessEvaluator(
        IBusBuddyDbContextFactory contextFactory,
        IOptions<RoutingDistrictSettings>? settings = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _settings = settings?.Value ?? new RoutingDistrictSettings();
    }

    public async Task<AssignFitnessResult> EvaluateAsync(
        int studentId,
        int routeId,
        RouteTimeSlotKind slot,
        bool overrideSeating = false,
        CancellationToken cancellationToken = default)
    {
        if (slot == RouteTimeSlotKind.Both)
        {
            return Blocked("Specify AM or PM for assign fitness");
        }

        await using var context = _contextFactory.CreateDbContext();
        var student = await context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            .ConfigureAwait(false);
        if (student is null)
        {
            return Blocked($"Student {studentId} not found");
        }

        var route = await context.Routes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return Blocked($"Route {routeId} not found");
        }

        var timeSlot = slot == RouteTimeSlotKind.AM ? RouteTimeSlot.AM : RouteTimeSlot.PM;
        var capacity = await ResolveCapacityAsync(context, route, timeSlot, cancellationToken)
            .ConfigureAwait(false);
        var assigned = timeSlot == RouteTimeSlot.AM
            ? await context.Students.AsNoTracking().CountAsync(s => s.AMRoute == route.RouteName, cancellationToken)
                .ConfigureAwait(false)
            : await context.Students.AsNoTracking().CountAsync(s => s.PMRoute == route.RouteName, cancellationToken)
                .ConfigureAwait(false);

        var reasons = new List<string>();
        var severity = AssignFitnessSeverity.None;
        var suggestNew = false;
        var allowed = true;

        if (capacity > 0 && assigned + 1 > capacity)
        {
            var msg = $"Seating capacity {capacity} would be exceeded ({assigned} already assigned)";
            if (overrideSeating && _settings.AllowSeatingOverride)
            {
                reasons.Add(msg + " (override recorded)");
                severity = AssignFitnessSeverity.Warn;
                Logger.Information(
                    "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons} Override=true",
                    studentId, routeId, msg);
            }
            else
            {
                reasons.Add(msg);
                severity = AssignFitnessSeverity.Block;
                allowed = false;
                suggestNew = true;
                Logger.Information(
                    "Assign fitness Blocked Student={Id} Route={RouteId} Reasons={Reasons}",
                    studentId, routeId, msg);
            }
        }

        // Soft: ride-time comfort (Haversine to school campus when available)
        if (student.Latitude is decimal sLat && student.Longitude is decimal sLon)
        {
            Destination? school = null;
            if (student.DestinationId is int destId)
            {
                school = await context.Destinations.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DestinationId == destId, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (school?.Latitude is decimal schLat && school.Longitude is decimal schLon)
            {
                var miles = RoutePacker.HaversineMiles(
                    (double)sLat, (double)sLon, (double)schLat, (double)schLon);
                var minutes = _settings.AverageSpeedMph <= 0
                    ? 0
                    : miles / _settings.AverageSpeedMph * 60.0;
                if (_settings.MaxRideMinutes is int maxRide && minutes > maxRide)
                {
                    reasons.Add($"Estimated ride ~{minutes:0} min exceeds soft max {maxRide} min");
                    if (severity == AssignFitnessSeverity.None)
                    {
                        severity = AssignFitnessSeverity.Warn;
                    }

                    Logger.Information(
                        "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons}",
                        studentId, routeId, reasons[^1]);
                }

                if (slot == RouteTimeSlotKind.AM && school.StartTime is TimeSpan start)
                {
                    // Backward estimate: leave home at StartTime - ride; warn if ride alone exceeds soft max already covered.
                    // Arrival risk: if current time-of-day simulation N/A, flag when estimated minutes leave less than 5 min buffer before start from a nominal 7:00 depot — use MaxRideMinutes only for MVP.
                    _ = start;
                }
            }

            // Geo outlier vs existing assignees on this route/slot
            var peers = timeSlot == RouteTimeSlot.AM
                ? await context.Students.AsNoTracking()
                    .Where(s => s.AMRoute == route.RouteName && s.StudentId != studentId &&
                                s.Latitude != null && s.Longitude != null)
                    .ToListAsync(cancellationToken).ConfigureAwait(false)
                : await context.Students.AsNoTracking()
                    .Where(s => s.PMRoute == route.RouteName && s.StudentId != studentId &&
                                s.Latitude != null && s.Longitude != null)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (peers.Count > 0)
            {
                var cLat = peers.Average(p => (double)p.Latitude!.Value);
                var cLon = peers.Average(p => (double)p.Longitude!.Value);
                var gapMiles = RoutePacker.HaversineMiles(cLat, cLon, (double)sLat, (double)sLon);
                var gapMinutes = _settings.AverageSpeedMph <= 0
                    ? 0
                    : gapMiles / _settings.AverageSpeedMph * 60.0;
                if (gapMinutes > _settings.MaxPickupGapMinutes)
                {
                    reasons.Add(
                        $"Geo outlier vs route cluster (~{gapMinutes:0} min gap > {_settings.MaxPickupGapMinutes} min)");
                    if (severity == AssignFitnessSeverity.None)
                    {
                        severity = AssignFitnessSeverity.Warn;
                    }

                    suggestNew = suggestNew || gapMinutes > _settings.MaxPickupGapMinutes * 2;
                    Logger.Information(
                        "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons}",
                        studentId, routeId, reasons[^1]);
                }
            }
        }

        IReadOnlyList<int> suggested = Array.Empty<int>();
        if (!allowed || suggestNew)
        {
            suggested = await SuggestAlternateRoutesAsync(context, student, routeId, timeSlot, capacity, cancellationToken)
                .ConfigureAwait(false);
            if (suggested.Count == 0 && !allowed)
            {
                suggestNew = true;
            }
        }

        return new AssignFitnessResult
        {
            Allowed = allowed,
            Severity = severity,
            Reasons = reasons,
            SuggestedRouteIds = suggested,
            SuggestNewRoute = suggestNew
        };
    }

    private static async Task<IReadOnlyList<int>> SuggestAlternateRoutesAsync(
        BusBuddyDbContext context,
        Student student,
        int excludeRouteId,
        RouteTimeSlot timeSlot,
        int neededCapacityHint,
        CancellationToken cancellationToken)
    {
        var routes = await context.Routes.AsNoTracking()
            .Where(r => r.IsActive && r.RouteId != excludeRouteId)
            .OrderBy(r => r.RouteName)
            .Take(40)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var suggestions = new List<int>();
        foreach (var route in routes)
        {
            var cap = await ResolveCapacityAsync(context, route, timeSlot, cancellationToken)
                .ConfigureAwait(false);
            var count = timeSlot == RouteTimeSlot.AM
                ? await context.Students.AsNoTracking().CountAsync(s => s.AMRoute == route.RouteName, cancellationToken)
                    .ConfigureAwait(false)
                : await context.Students.AsNoTracking().CountAsync(s => s.PMRoute == route.RouteName, cancellationToken)
                    .ConfigureAwait(false);
            if (cap <= 0 || count + 1 <= cap)
            {
                suggestions.Add(route.RouteId);
            }

            if (suggestions.Count >= 5)
            {
                break;
            }
        }

        _ = student;
        _ = neededCapacityHint;
        return suggestions;
    }

    private static async Task<int> ResolveCapacityAsync(
        BusBuddyDbContext context,
        Route route,
        RouteTimeSlot timeSlot,
        CancellationToken cancellationToken)
    {
        var vehicleId = timeSlot == RouteTimeSlot.AM ? route.AMVehicleId : route.PMVehicleId;
        if (vehicleId is int id)
        {
            var bus = await context.Buses.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusId == id, cancellationToken)
                .ConfigureAwait(false);
            if (bus is not null && bus.SeatingCapacity > 0)
            {
                return bus.SeatingCapacity;
            }
        }

        var largest = await context.Buses.AsNoTracking()
            .Where(b => b.Status == "Active" && b.SeatingCapacity > 0)
            .OrderByDescending(b => b.SeatingCapacity)
            .Select(b => b.SeatingCapacity)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return largest > 0 ? largest : 72;
    }

    private static AssignFitnessResult Blocked(string reason) =>
        new()
        {
            Allowed = false,
            Severity = AssignFitnessSeverity.Block,
            Reasons = new[] { reason }
        };
}
