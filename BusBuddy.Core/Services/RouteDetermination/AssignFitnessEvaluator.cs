using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Assign-time seating / time / geo fitness (spec 008 US2, Q2:B).</summary>
public sealed class AssignFitnessEvaluator
{
    private static readonly ILogger Logger = Log.ForContext<AssignFitnessEvaluator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IDistrictSettingsAccessor? _districtAccessor;
    private readonly RoutingDistrictSettings _settingsFallback;

    public AssignFitnessEvaluator(
        IBusBuddyDbContextFactory contextFactory,
        IOptions<RoutingDistrictSettings>? settings = null,
        IDistrictSettingsAccessor? districtAccessor = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _settingsFallback = settings?.Value ?? new RoutingDistrictSettings();
        _districtAccessor = districtAccessor;
    }

    private RoutingDistrictSettings District =>
        _districtAccessor?.Current ?? _settingsFallback;

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
        var busCapacity = await RouteBusCapacity.ForCandidateAsync(
                context,
                route,
                timeSlot,
                student.StudentId,
                student.RequiresWheelchair,
                overrideSeating && District.AllowSeatingOverride,
                cancellationToken)
            .ConfigureAwait(false);

        var reasons = new List<string>();
        var severity = AssignFitnessSeverity.None;
        var suggestNew = false;
        var allowed = true;

        var studentNeedsSpecial = StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student);
        var routeIsSpecial = StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route);
        if (studentNeedsSpecial != routeIsSpecial)
        {
            var msg = studentNeedsSpecial
                ? "Student requires a special-needs route"
                : "Route is designated for special-needs students only";
            reasons.Add(msg);
            severity = AssignFitnessSeverity.Block;
            allowed = false;
            suggestNew = true;
            Logger.Information(
                "Assign fitness Blocked Student={Id} Route={RouteId} Reasons={Reasons}",
                studentId, routeId, msg);
        }

        if (busCapacity.Blocked)
        {
            var msg = busCapacity.Message ?? "Seating capacity would be exceeded";
            reasons.Add(msg);
            severity = AssignFitnessSeverity.Block;
            allowed = false;
            suggestNew = true;
            Logger.Information(
                "Assign fitness Blocked Student={Id} Route={RouteId} Reasons={Reasons}",
                studentId, routeId, msg);
        }
        else if (busCapacity.Message is not null)
        {
            reasons.Add(busCapacity.Message);
            if (severity == AssignFitnessSeverity.None)
            {
                severity = AssignFitnessSeverity.Warn;
            }

            Logger.Information(
                "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons} Override=true",
                studentId, routeId, busCapacity.Message);
        }
        else if (busCapacity.Warning is not null)
        {
            reasons.Add(busCapacity.Warning);
            if (severity == AssignFitnessSeverity.None)
            {
                severity = AssignFitnessSeverity.Warn;
            }

            Logger.Information(
                "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons}",
                studentId, routeId, busCapacity.Warning);
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
                var minutes = District.AverageSpeedMph <= 0
                    ? 0
                    : miles / District.AverageSpeedMph * 60.0;
                if (District.MaxRideMinutes is int maxRide && minutes > maxRide)
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
            }

            // Geo outlier vs existing assignees on this route/slot
            var peers = await context.Students.AsNoTracking()
                .WhereOnSlot(route.RouteId, route.RouteName, timeSlot)
                .Where(s => s.StudentId != studentId && s.Latitude != null && s.Longitude != null)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (peers.Count > 0)
            {
                var cLat = peers.Average(p => (double)p.Latitude!.Value);
                var cLon = peers.Average(p => (double)p.Longitude!.Value);
                var gapMiles = RoutePacker.HaversineMiles(cLat, cLon, (double)sLat, (double)sLon);
                var gapMinutes = District.AverageSpeedMph <= 0
                    ? 0
                    : gapMiles / District.AverageSpeedMph * 60.0;
                if (gapMinutes > District.MaxPickupGapMinutes)
                {
                    reasons.Add(
                        $"Geo outlier vs route cluster (~{gapMinutes:0} min gap > {District.MaxPickupGapMinutes} min)");
                    if (severity == AssignFitnessSeverity.None)
                    {
                        severity = AssignFitnessSeverity.Warn;
                    }

                    suggestNew = suggestNew || gapMinutes > District.MaxPickupGapMinutes * 2;
                    Logger.Information(
                        "Assign fitness Warned Student={Id} Route={RouteId} Reasons={Reasons}",
                        studentId, routeId, reasons[^1]);
                }
            }
        }

        IReadOnlyList<int> suggested = Array.Empty<int>();
        if (!allowed || suggestNew)
        {
            suggested = await SuggestAlternateRoutesAsync(context, student, routeId, timeSlot, cancellationToken)
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
        CancellationToken cancellationToken)
    {
        var routes = await context.Routes.AsNoTracking()
            .Where(r => r.IsActive && r.RouteId != excludeRouteId)
            .OrderBy(r => r.RouteName)
            .Take(40)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentNeedsSpecial = StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student);

        var suggestions = new List<int>();
        foreach (var route in routes)
        {
            if (StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route) != studentNeedsSpecial)
            {
                continue;
            }

            var room = await RouteBusCapacity.ForCandidateAsync(
                    context,
                    route,
                    timeSlot,
                    student.StudentId,
                    student.RequiresWheelchair,
                    overrideSeating: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!room.Blocked && room.Warning is null)
            {
                suggestions.Add(route.RouteId);
            }

            if (suggestions.Count >= 5)
            {
                break;
            }
        }

        return suggestions;
    }

    private static AssignFitnessResult Blocked(string reason) =>
        new()
        {
            Allowed = false,
            Severity = AssignFitnessSeverity.Block,
            Reasons = new[] { reason }
        };
}
