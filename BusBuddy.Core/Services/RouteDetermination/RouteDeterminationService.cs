using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Spec 008 year-start generate/assign and clerk override.</summary>
public sealed class RouteDeterminationService : IRouteDeterminationService
{
    private static readonly ILogger Logger = Log.ForContext<RouteDeterminationService>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IRouteService _routeService;
    private readonly RoutingDistrictSettings _settings;

    public RouteDeterminationService(
        IBusBuddyDbContextFactory contextFactory,
        IRouteService routeService,
        IOptions<RoutingDistrictSettings>? settings = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _settings = settings?.Value ?? new RoutingDistrictSettings();
    }

    public async Task<RouteGenerationResult> GenerateAndAssignAsync(
        int schoolDestinationId,
        RouteTimeSlotKind slot,
        FleetKind fleetKind,
        RouteGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opId = Guid.NewGuid();
        options ??= new RouteGenerationOptions();
        var warnings = new List<string>();

        if (fleetKind == FleetKind.Transfer)
        {
            return new RouteGenerationResult
            {
                OperationId = opId,
                SchoolDestinationId = schoolDestinationId,
                FleetKind = fleetKind,
                Success = false,
                Error = "Transfer fleet generation is not in MVP (see US4 / T033)."
            };
        }

        await using var context = _contextFactory.CreateDbContext();
        var school = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == schoolDestinationId, cancellationToken)
            .ConfigureAwait(false);

        if (school is null)
        {
            return Fail(opId, schoolDestinationId, fleetKind, $"School destination {schoolDestinationId} not found");
        }

        if (slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both && school.StartTime is null)
        {
            return Fail(opId, schoolDestinationId, fleetKind,
                $"School '{school.Name}' is missing StartTime required for AM generation");
        }

        if (slot is RouteTimeSlotKind.PM or RouteTimeSlotKind.Both && school.DismissalTime is null)
        {
            warnings.Add($"School '{school.Name}' has no DismissalTime; PM mirror will still create stop structure");
        }

        var students = await context.Students.AsNoTracking()
            .Where(s => s.DestinationId == schoolDestinationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var riders = new List<RiderPoint>();
        var unclustered = new List<int>();
        foreach (var s in students)
        {
            if (s.Latitude is decimal lat && s.Longitude is decimal lon)
            {
                riders.Add(new RiderPoint(s.StudentId, (double)lat, (double)lon));
            }
            else
            {
                unclustered.Add(s.StudentId);
            }
        }

        if (unclustered.Count > 0)
        {
            warnings.Add($"{unclustered.Count} student(s) lack coordinates and were left unclustered");
        }

        var seating = await ResolveDefaultSeatingAsync(context, options.DefaultSeatingCapacity, cancellationToken)
            .ConfigureAwait(false);

        var cells = DensityCellBuilder.Build(riders, _settings);
        var packed = new List<(DensityCell Cell, PackedRoute Pack)>();
        foreach (var cell in cells)
        {
            foreach (var pack in RoutePacker.PackCell(cell, seating, _settings))
            {
                packed.Add((cell, pack));
            }
        }

        var proposals = new List<RouteProposalDto>();
        var assigned = 0;
        var schoolSlug = SanitizeName(school.Name);
        var packIndex = 0;

        foreach (var (cell, pack) in packed)
        {
            packIndex++;
            var amName = $"Draft-{schoolSlug}-{cell.CellId}-{packIndex}";
            var amProposal = await MaterializeProposalAsync(
                    amName,
                    pack,
                    RouteTimeSlotKind.AM,
                    fleetKind,
                    options.DryRun,
                    assignAm: slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both,
                    cancellationToken)
                .ConfigureAwait(false);
            proposals.Add(amProposal);
            if (amProposal.PersistedRouteId is not null &&
                slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both)
            {
                assigned += pack.OrderedStudentIds.Count;
            }

            if (slot is RouteTimeSlotKind.PM or RouteTimeSlotKind.Both)
            {
                var pmName = $"{amName}-PM";
                var pmProposal = await MaterializeProposalAsync(
                        pmName,
                        pack,
                        RouteTimeSlotKind.PM,
                        fleetKind,
                        options.DryRun,
                        assignAm: false,
                        cancellationToken,
                        assignPmMirror: !options.DryRun && slot == RouteTimeSlotKind.Both,
                        amRouteNameForMirror: amName)
                    .ConfigureAwait(false);
                proposals.Add(pmProposal);
            }
        }

        Logger.Information(
            "Route generation completed School={SchoolId} Fleet={Fleet} Routes={N} Students={S} OpId={OpId}",
            schoolDestinationId,
            fleetKind,
            proposals.Count,
            assigned,
            opId);

        return new RouteGenerationResult
        {
            OperationId = opId,
            SchoolDestinationId = schoolDestinationId,
            FleetKind = fleetKind,
            Proposals = proposals,
            UnclusteredStudentIds = unclustered,
            Warnings = warnings,
            AssignedStudentCount = assigned,
            Success = true
        };
    }

    public async Task<AssignFitnessResult> RecalculateOnAssignAsync(
        int studentId,
        int routeId,
        RouteTimeSlotKind slot,
        bool overrideSeating = false,
        CancellationToken cancellationToken = default)
    {
        // Full toast policy is US2; MVP exposes seating hard-block for callers.
        if (slot == RouteTimeSlotKind.Both)
        {
            return new AssignFitnessResult
            {
                Allowed = false,
                Severity = AssignFitnessSeverity.Block,
                Reasons = new[] { "Specify AM or PM for assign fitness" }
            };
        }

        await using var context = _contextFactory.CreateDbContext();
        var route = await context.Routes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return new AssignFitnessResult
            {
                Allowed = false,
                Severity = AssignFitnessSeverity.Block,
                Reasons = new[] { $"Route {routeId} not found" }
            };
        }

        var timeSlot = slot == RouteTimeSlotKind.AM ? RouteTimeSlot.AM : RouteTimeSlot.PM;
        var capacity = await GetCapacityAsync(context, route, timeSlot, cancellationToken).ConfigureAwait(false);
        var assigned = await CountAssignedAsync(context, route.RouteName, timeSlot, cancellationToken)
            .ConfigureAwait(false);

        if (capacity > 0 && assigned + 1 > capacity && !overrideSeating)
        {
            var reasons = new[] { $"Seating capacity {capacity} would be exceeded ({assigned} already assigned)" };
            Logger.Information(
                "Assign fitness Blocked Student={Id} Route={RouteId} Reasons={Reasons}",
                studentId, routeId, string.Join("; ", reasons));
            return new AssignFitnessResult
            {
                Allowed = false,
                Severity = AssignFitnessSeverity.Block,
                Reasons = reasons,
                SuggestNewRoute = true
            };
        }

        return new AssignFitnessResult
        {
            Allowed = true,
            Severity = AssignFitnessSeverity.None
        };
    }

    public async Task<ClerkOverrideResult> ApplyClerkOverrideAsync(
        int studentId,
        int fromRouteId,
        int toRouteId,
        RouteTimeSlotKind slot,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (slot == RouteTimeSlotKind.Both)
        {
            return new ClerkOverrideResult { Success = false, Error = "Specify AM or PM for override" };
        }

        var timeSlot = slot == RouteTimeSlotKind.AM ? RouteTimeSlot.AM : RouteTimeSlot.PM;

        await using var context = _contextFactory.CreateDbContext();
        var student = await context.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            .ConfigureAwait(false);
        if (student is null)
        {
            return new ClerkOverrideResult { Success = false, Error = $"Student {studentId} not found" };
        }

        var mode = StudentRideModeHelper.FromStudent(student);
        var remove = await _routeService.RemoveStudentFromRouteAsync(studentId, fromRouteId, timeSlot)
            .ConfigureAwait(false);
        if (!remove.IsSuccess)
        {
            // Allow override when student was not on from-route (already moved)
            Logger.Warning("Override remove soft-fail Student={Id}: {Error}", studentId, remove.Error);
        }

        var assign = await _routeService.AssignStudentToRouteAsync(studentId, toRouteId, timeSlot)
            .ConfigureAwait(false);
        if (!assign.IsSuccess)
        {
            return new ClerkOverrideResult { Success = false, Error = assign.Error };
        }

        var retainMirror = slot == RouteTimeSlotKind.AM
            ? StudentRideModeHelper.RetainStopOnPmMirror(mode)
            : StudentRideModeHelper.RetainStopOnAmMirror(mode);

        Logger.Information(
            "Clerk override Student={StudentId} From={From} To={To} Slot={Slot} Reason={Reason} RetainMirror={Retain}",
            studentId, fromRouteId, toRouteId, slot, reason ?? "(none)", retainMirror);

        return new ClerkOverrideResult { Success = true, RetainedMirrorStop = retainMirror };
    }

    private async Task<RouteProposalDto> MaterializeProposalAsync(
        string routeName,
        PackedRoute pack,
        RouteTimeSlotKind slot,
        FleetKind fleetKind,
        bool dryRun,
        bool assignAm,
        CancellationToken cancellationToken,
        bool assignPmMirror = false,
        string? amRouteNameForMirror = null)
    {
        var dto = new RouteProposalDto
        {
            ProposalKey = $"{routeName}:{slot}",
            SuggestedRouteName = routeName,
            Slot = slot,
            FleetKind = fleetKind,
            CellId = pack.CellId,
            OrderedStudentIds = pack.OrderedStudentIds.ToList(),
            SuggestedBusSeatingCapacity = pack.SeatingCapacity,
            EstimatedMiles = pack.EstimatedMiles,
            EstimatedMinutes = pack.EstimatedMinutes,
            Status = dryRun ? "Draft" : "Accepted"
        };

        if (dryRun)
        {
            return dto;
        }

        var create = await _routeService.CreateRouteAsync(new Route
        {
            RouteName = routeName,
            Date = DateTime.Today,
            Description = $"008 {fleetKind} {slot} cell {pack.CellId}",
            IsActive = true,
            School = routeName.StartsWith("Draft-", StringComparison.Ordinal)
                ? ExtractSchoolFromDraft(routeName)
                : null,
            AMRiders = slot == RouteTimeSlotKind.AM ? pack.OrderedStudentIds.Count : null,
            PMRiders = slot == RouteTimeSlotKind.PM ? pack.OrderedStudentIds.Count : null
        }).ConfigureAwait(false);

        if (!create.IsSuccess || create.Value is null)
        {
            dto.Status = "Rejected";
            Logger.Warning("Failed to persist draft route {Name}: {Error}", routeName, create.Error);
            return dto;
        }

        dto.PersistedRouteId = create.Value.RouteId;
        var routeId = create.Value.RouteId;

        if (assignAm && slot == RouteTimeSlotKind.AM)
        {
            foreach (var studentId in pack.OrderedStudentIds)
            {
                var result = await _routeService.AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.AM)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    Logger.Warning("Assign AM failed Student={Id} Route={Route}: {Error}",
                        studentId, routeName, result.Error);
                }
            }
        }

        if (assignPmMirror && slot == RouteTimeSlotKind.PM)
        {
            // Mirror: assign PM for Both riders; keep AM-only as stop-retained (AM assignment already set).
            await using var ctx = _contextFactory.CreateDbContext();
            foreach (var studentId in pack.OrderedStudentIds)
            {
                var student = await ctx.Students.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
                    .ConfigureAwait(false);
                if (student is null)
                {
                    continue;
                }

                var mode = StudentRideModeHelper.FromRouteNames(
                    string.IsNullOrWhiteSpace(student.AMRoute) ? amRouteNameForMirror : student.AMRoute,
                    student.PMRoute);

                // Year-start: treat newly AM-assigned as Both unless previously PM-only preference existed.
                // AM-only retention: do not clear AM; assign PM only when mode is Both or Neither (new).
                if (mode is StudentRideMode.PM)
                {
                    continue;
                }

                // Default year-start: assign PM mirror for all packed AM students (Both).
                // AM-only students still retain presence via ordered stop list on this PM draft.
                var result = await _routeService.AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.PM)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    Logger.Debug(
                        "PM mirror assign skipped/failed Student={Id} (may be AM-only retention): {Error}",
                        studentId, result.Error);
                }
            }
        }

        return dto;
    }

    private static string ExtractSchoolFromDraft(string routeName)
    {
        // Draft-{School}-{Cell}-{n} or Draft-{School}-{Cell}-{n}-PM
        var parts = routeName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : routeName;
    }

    private static string SanitizeName(string name)
    {
        var cleaned = new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray())
            .Trim()
            .Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "School" : cleaned;
    }

    private static async Task<int> ResolveDefaultSeatingAsync(
        BusBuddyDbContext context,
        int fallback,
        CancellationToken cancellationToken)
    {
        var bus = await context.Buses.AsNoTracking()
            .Where(b => b.Status == "Active" && b.SeatingCapacity > 0)
            .OrderByDescending(b => b.SeatingCapacity)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return bus?.SeatingCapacity > 0 ? bus.SeatingCapacity : fallback;
    }

    private static async Task<int> GetCapacityAsync(
        BusBuddyDbContext context,
        Route route,
        RouteTimeSlot slot,
        CancellationToken cancellationToken)
    {
        var vehicleId = slot == RouteTimeSlot.AM ? route.AMVehicleId : route.PMVehicleId;
        if (vehicleId is int id)
        {
            var bus = await context.Buses.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusId == id, cancellationToken)
                .ConfigureAwait(false);
            if (bus is not null)
            {
                return bus.SeatingCapacity;
            }
        }

        return 72;
    }

    private static async Task<int> CountAssignedAsync(
        BusBuddyDbContext context,
        string routeName,
        RouteTimeSlot slot,
        CancellationToken cancellationToken)
    {
        if (slot == RouteTimeSlot.AM)
        {
            return await context.Students.AsNoTracking()
                .CountAsync(s => s.AMRoute == routeName, cancellationToken)
                .ConfigureAwait(false);
        }

        return await context.Students.AsNoTracking()
            .CountAsync(s => s.PMRoute == routeName, cancellationToken)
            .ConfigureAwait(false);
    }

    private static RouteGenerationResult Fail(
        Guid opId, int schoolId, FleetKind fleet, string error) =>
        new()
        {
            OperationId = opId,
            SchoolDestinationId = schoolId,
            FleetKind = fleet,
            Success = false,
            Error = error
        };
}
