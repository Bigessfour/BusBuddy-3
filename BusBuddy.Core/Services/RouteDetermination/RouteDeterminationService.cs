using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Spec 008 year-start generate/assign and clerk override.</summary>
public sealed partial class RouteDeterminationService : IRouteDeterminationService
{
    private static readonly ILogger Logger = Log.ForContext<RouteDeterminationService>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IRouteService _routeService;
    private readonly IDistrictSettingsAccessor? _districtAccessor;
    private readonly RoutingDistrictSettings _settingsFallback;
    private readonly AssignFitnessEvaluator _fitnessEvaluator;
    private readonly IRouteWaypointRebuildService? _waypointRebuild;
    private readonly IRouteOptimizationService? _routeOptimization;
    private readonly IRoutingService? _routing;

    public RouteDeterminationService(
        IBusBuddyDbContextFactory contextFactory,
        IRouteService routeService,
        IOptions<RoutingDistrictSettings>? settings = null,
        AssignFitnessEvaluator? fitnessEvaluator = null,
        IRouteWaypointRebuildService? waypointRebuild = null,
        IDistrictSettingsAccessor? districtAccessor = null,
        IRouteOptimizationService? routeOptimization = null,
        IRoutingService? routing = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _settingsFallback = settings?.Value ?? new RoutingDistrictSettings();
        _districtAccessor = districtAccessor;
        _fitnessEvaluator = fitnessEvaluator
            ?? new AssignFitnessEvaluator(contextFactory, settings, districtAccessor);
        _waypointRebuild = waypointRebuild;
        _routeOptimization = routeOptimization;
        _routing = routing;
    }

    private RoutingDistrictSettings District =>
        _districtAccessor?.Current ?? _settingsFallback;

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
            return await GenerateTransferFleetAsync(
                    schoolDestinationId, slot, options, opId, cancellationToken)
                .ConfigureAwait(false);
        }

        await using var context = _contextFactory.CreateDbContext();
        var school = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == schoolDestinationId, cancellationToken)
            .ConfigureAwait(false);

        if (school is null)
        {
            return Fail(opId, schoolDestinationId, fleetKind, $"School destination {schoolDestinationId} not found");
        }

        if ((slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both) && school.StartTime is null)
        {
            return Fail(opId, schoolDestinationId, fleetKind,
                $"School '{school.Name}' is missing StartTime required for AM generation");
        }

        if ((slot is RouteTimeSlotKind.PM or RouteTimeSlotKind.Both) && school.DismissalTime is null)
        {
            warnings.Add($"School '{school.Name}' has no DismissalTime; PM mirror will still create stop structure");
        }

        var transferStudentIds = await context.StudentSchoolTransfers.AsNoTracking()
            .Where(t => t.IsActive && (t.FromDestinationId == schoolDestinationId || t.ToDestinationId == schoolDestinationId))
            .Select(t => t.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var transferSet = transferStudentIds.ToHashSet();

        var students = await context.Students.AsNoTracking()
            .Where(s => s.DestinationId == schoolDestinationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // HomeToSchool excludes active transfer riders (separate fleet pool — Q1:A / T034).
        students = students.Where(s => !transferSet.Contains(s.StudentId)).ToList();
        if (transferSet.Count > 0)
        {
            warnings.Add($"{transferSet.Count} transfer student(s) excluded from HomeToSchool packing");
        }

        var specialNeedsCount = students.Count(StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport);
        if (specialNeedsCount > 0)
        {
            students = students.Where(s => !StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(s)).ToList();
            warnings.Add($"{specialNeedsCount} special-needs student(s) excluded from auto-generation — assign manually to a special-needs route");
        }

        // Snapshot ride mode before we rewrite AM/PM assignments (AM-only must keep PMRoute empty).
        var priorModeByStudent = students.ToDictionary(
            s => s.StudentId,
            s => StudentRideModeHelper.FromStudent(s));

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

        var cells = DensityCellBuilder.Build(riders, District);
        var packed = new List<(DensityCell Cell, PackedRoute Pack)>();
        foreach (var cell in cells)
        {
            foreach (var pack in RoutePacker.PackCell(cell, seating, District))
            {
                packed.Add((cell, pack));
            }
        }

        var schoolSlug = SanitizeName(school.Name);
        if (!options.DryRun)
        {
            var cleared = await ClearExistingDraftsAsync(schoolSlug, school.Name, cancellationToken)
                .ConfigureAwait(false);
            if (cleared > 0)
            {
                warnings.Add($"Replaced {cleared} existing Draft route(s) for this school");
            }
        }

        var proposals = new List<RouteProposalDto>();
        var assigned = 0;
        var hardFailures = new List<string>();
        var packIndex = 0;

        foreach (var (cell, pack) in packed)
        {
            packIndex++;
            var amName = $"Draft-{schoolSlug}-{cell.CellId}-{packIndex}";
            var amProposal = await MaterializeProposalAsync(
                    amName,
                    school,
                    pack,
                    RouteTimeSlotKind.AM,
                    fleetKind,
                    options.DryRun,
                    assignAm: slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both,
                    priorModeByStudent,
                    cancellationToken)
                .ConfigureAwait(false);
            proposals.Add(amProposal.Dto);
            hardFailures.AddRange(amProposal.Failures);
            if (amProposal.Dto.PersistedRouteId is not null &&
                slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both)
            {
                assigned += amProposal.AssignedCount;
            }

            if (slot is RouteTimeSlotKind.PM or RouteTimeSlotKind.Both)
            {
                var pmName = $"{amName}-PM";
                var pmProposal = await MaterializeProposalAsync(
                        pmName,
                        school,
                        pack,
                        RouteTimeSlotKind.PM,
                        fleetKind,
                        options.DryRun,
                        assignAm: false,
                        priorModeByStudent,
                        cancellationToken,
                        assignPmMirror: !options.DryRun && slot == RouteTimeSlotKind.Both)
                    .ConfigureAwait(false);
                proposals.Add(pmProposal.Dto);
                hardFailures.AddRange(pmProposal.Failures);
            }
        }

        var rejected = proposals.Count(p => p.Status == "Rejected");
        var success = hardFailures.Count == 0 && rejected == 0;
        if (!success)
        {
            warnings.AddRange(hardFailures.Take(5));
            if (hardFailures.Count > 5)
            {
                warnings.Add($"…and {hardFailures.Count - 5} more failure(s)");
            }
        }

        Logger.Information(
            "Route generation completed School={SchoolId} Fleet={Fleet} Routes={N} Students={S} Success={Success} OpId={OpId}",
            schoolDestinationId,
            fleetKind,
            proposals.Count,
            assigned,
            success,
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
            Success = success,
            Error = success
                ? null
                : $"Generation incomplete: {rejected} rejected proposal(s), {hardFailures.Count} assign/create failure(s)"
        };
    }

    public Task<AssignFitnessResult> RecalculateOnAssignAsync(
        int studentId,
        int routeId,
        RouteTimeSlotKind slot,
        bool overrideSeating = false,
        CancellationToken cancellationToken = default) =>
        _fitnessEvaluator.EvaluateAsync(studentId, routeId, slot, overrideSeating, cancellationToken);

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

        if (fromRouteId <= 0)
        {
            var catalog = await context.Routes.AsNoTracking()
                .Select(r => new { r.RouteId, r.RouteName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            fromRouteId = StudentRouteAssignment.CurrentRouteId(
                student,
                timeSlot,
                catalog.Select(r => (r.RouteId, (string?)r.RouteName)));
        }

        var mode = StudentRideModeHelper.FromStudent(student);
        var remove = await _routeService.RemoveStudentFromRouteAsync(studentId, fromRouteId, timeSlot)
            .ConfigureAwait(false);
        if (!remove.IsSuccess)
        {
            Logger.Warning("Override remove soft-fail Student={Id}: {Error}", studentId, remove.Error);
        }

        var assign = await _routeService.AssignStudentToRouteAsync(
                studentId,
                toRouteId,
                timeSlot,
                overrideSeating: true)
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

    public async Task<RouteGenerationResult> RegenerateSchedulesForSchoolAsync(
        int schoolDestinationId,
        CancellationToken cancellationToken = default)
    {
        var opId = Guid.NewGuid();
        await using var context = _contextFactory.CreateDbContext();
        var school = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == schoolDestinationId, cancellationToken)
            .ConfigureAwait(false);
        if (school is null)
        {
            return Fail(opId, schoolDestinationId, FleetKind.HomeToSchool, "School not found");
        }

        var routes = await context.Routes.AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var updated = 0;
        var warnings = new List<string>();
        foreach (var route in routes)
        {
            if (!await RouteServesSchoolAsync(context, route, school, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var applied = await ApplyPublishedClocksAsync(route.RouteId, cancellationToken).ConfigureAwait(false);
            if (applied.RoutesUpdated > 0)
            {
                updated++;
                warnings.AddRange(applied.Warnings.Select(w => $"{route.RouteName}: {w}"));
            }
            else if (!string.IsNullOrWhiteSpace(applied.Error))
            {
                warnings.Add($"{route.RouteName}: {applied.Error}");
            }
        }

        Logger.Information(
            "Schedule regen School={SchoolId} RoutesUpdated={N} OpId={OpId}",
            schoolDestinationId, updated, opId);

        var missed = warnings.Where(w => !w.Contains("straight-line", StringComparison.OrdinalIgnoreCase)).ToList();
        return new RouteGenerationResult
        {
            OperationId = opId,
            SchoolDestinationId = schoolDestinationId,
            FleetKind = FleetKind.HomeToSchool,
            Success = missed.Count == 0 || updated > 0,
            AssignedStudentCount = 0,
            RoutesUpdated = updated,
            Warnings = warnings,
            Error = missed.Count == 0 ? null : string.Join("; ", missed.Take(3))
        };
    }

    private async Task<RouteGenerationResult> GenerateTransferFleetAsync(
        int schoolDestinationId,
        RouteTimeSlotKind slot,
        RouteGenerationOptions options,
        Guid opId,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        await using var context = _contextFactory.CreateDbContext();
        var school = await context.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DestinationId == schoolDestinationId, cancellationToken)
            .ConfigureAwait(false);
        if (school is null)
        {
            return Fail(opId, schoolDestinationId, FleetKind.Transfer, "School destination not found");
        }

        var transfers = await context.StudentSchoolTransfers.AsNoTracking()
            .Where(t => t.IsActive &&
                        (t.ToDestinationId == schoolDestinationId || t.FromDestinationId == schoolDestinationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var riders = new List<RiderPoint>();
        var unclustered = new List<int>();
        foreach (var t in transfers)
        {
            if (t.PickupLatitude is decimal plat && t.PickupLongitude is decimal plon)
            {
                riders.Add(new RiderPoint(t.StudentId, (double)plat, (double)plon));
            }
            else
            {
                unclustered.Add(t.StudentId);
            }
        }

        if (riders.Count == 0)
        {
            return Fail(opId, schoolDestinationId, FleetKind.Transfer,
                "No active transfers with pickup coordinates for this school");
        }

        var seating = await ResolveDefaultSeatingAsync(context, options.DefaultSeatingCapacity, cancellationToken)
            .ConfigureAwait(false);
        var cells = DensityCellBuilder.Build(riders, District);
        var packed = cells.SelectMany(c => RoutePacker.PackCell(c, seating, District)).ToList();
        var schoolSlug = SanitizeName(school.Name);
        var prefix = $"Draft-Xfer-{schoolSlug}-";

        if (!options.DryRun)
        {
            var cleared = await ClearExistingDraftsAsync(schoolSlug, school.Name, cancellationToken, xferOnly: true)
                .ConfigureAwait(false);
            if (cleared > 0)
            {
                warnings.Add($"Replaced {cleared} existing transfer Draft route(s)");
            }
        }

        var priorMode = transfers.ToDictionary(t => t.StudentId, _ => StudentRideMode.Both);
        var transferByStudent = transfers
            .GroupBy(t => t.StudentId)
            .ToDictionary(g => g.Key, g => g.First());
        var proposals = new List<RouteProposalDto>();
        var hardFailures = new List<string>();
        var assigned = 0;
        var idx = 0;
        foreach (var pack in packed)
        {
            idx++;
            var cellId = pack.CellId;
            var amName = $"{prefix}{cellId}-{idx}";

            if (slot is RouteTimeSlotKind.AM or RouteTimeSlotKind.Both)
            {
                var amResult = await MaterializeProposalAsync(
                        amName,
                        school,
                        pack,
                        RouteTimeSlotKind.AM,
                        FleetKind.Transfer,
                        options.DryRun,
                        assignAm: true,
                        priorMode,
                        cancellationToken,
                        transferStopsByStudent: transferByStudent)
                    .ConfigureAwait(false);
                proposals.Add(amResult.Dto);
                hardFailures.AddRange(amResult.Failures);
                assigned += amResult.AssignedCount;
                await TryRebuildWaypointsAsync(amResult.Dto.PersistedRouteId, options.DryRun, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (slot is RouteTimeSlotKind.PM or RouteTimeSlotKind.Both)
            {
                var pmName = $"{amName}-PM";
                var pmResult = await MaterializeProposalAsync(
                        pmName,
                        school,
                        pack,
                        RouteTimeSlotKind.PM,
                        FleetKind.Transfer,
                        options.DryRun,
                        assignAm: false,
                        priorMode,
                        cancellationToken,
                        assignPmMirror: !options.DryRun,
                        transferStopsByStudent: transferByStudent)
                    .ConfigureAwait(false);
                proposals.Add(pmResult.Dto);
                hardFailures.AddRange(pmResult.Failures);
                await TryRebuildWaypointsAsync(pmResult.Dto.PersistedRouteId, options.DryRun, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var success = hardFailures.Count == 0 && proposals.All(p => p.Status != "Rejected");
        Logger.Information(
            "Route generation completed School={SchoolId} Fleet={Fleet} Routes={N} Students={S} Success={Success} OpId={OpId}",
            schoolDestinationId, FleetKind.Transfer, proposals.Count, assigned, success, opId);

        return new RouteGenerationResult
        {
            OperationId = opId,
            SchoolDestinationId = schoolDestinationId,
            FleetKind = FleetKind.Transfer,
            Proposals = proposals,
            UnclusteredStudentIds = unclustered,
            Warnings = warnings,
            AssignedStudentCount = assigned,
            Success = success,
            Error = success ? null : string.Join("; ", hardFailures.Take(3))
        };
    }

    private async Task TryRebuildWaypointsAsync(
        int? routeId,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (dryRun || routeId is not int rid || _waypointRebuild is null)
        {
            return;
        }

        try
        {
            await _waypointRebuild.RebuildAndPersistAsync(rid, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Transfer waypoint rebuild failed RouteId={Id}", rid);
        }
    }

    private async Task<int> ClearExistingDraftsAsync(
        string schoolSlug,
        string schoolDisplayName,
        CancellationToken cancellationToken,
        bool xferOnly = false)
    {
        var homePrefix = $"Draft-{schoolSlug}-";
        var xferPrefix = $"Draft-Xfer-{schoolSlug}-";
        await using var context = _contextFactory.CreateWriteDbContext();
        var drafts = await context.Routes.AsTracking()
            .Where(r => xferOnly
                ? r.RouteName.StartsWith(xferPrefix)
                : (r.RouteName.StartsWith(homePrefix) && !r.RouteName.StartsWith("Draft-Xfer-")) ||
                  (r.School == schoolDisplayName && r.RouteName.StartsWith("Draft-") && !r.RouteName.StartsWith("Draft-Xfer-")))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (drafts.Count == 0)
        {
            return 0;
        }

        var draftNames = drafts.Select(d => d.RouteName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var students = await context.Students.AsTracking()
            .Where(s =>
                (s.AMRoute != null && draftNames.Contains(s.AMRoute)) ||
                (s.PMRoute != null && draftNames.Contains(s.PMRoute)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var student in students)
        {
            if (student.AMRoute is not null && draftNames.Contains(student.AMRoute))
            {
                student.AMRoute = null;
            }

            if (student.PMRoute is not null && draftNames.Contains(student.PMRoute))
            {
                student.PMRoute = null;
            }
        }

        var draftIds = drafts.Select(d => d.RouteId).ToList();
        var stops = await context.RouteStops.AsTracking()
            .Where(s => draftIds.Contains(s.RouteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.RouteStops.RemoveRange(stops);
        context.Routes.RemoveRange(drafts);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Cleared {Count} Draft route(s) slug={Slug} XferOnly={Xfer}",
            drafts.Count, schoolSlug, xferOnly);
        return drafts.Count;
    }

    private async Task<MaterializeResult> MaterializeProposalAsync(
        string routeName,
        Destination school,
        PackedRoute pack,
        RouteTimeSlotKind slot,
        FleetKind fleetKind,
        bool dryRun,
        bool assignAm,
        IReadOnlyDictionary<int, StudentRideMode> priorModeByStudent,
        CancellationToken cancellationToken,
        bool assignPmMirror = false,
        IReadOnlyDictionary<int, StudentSchoolTransfer>? transferStopsByStudent = null)
    {
        var failures = new List<string>();
        var assignedCount = 0;
        var schoolDisplayName = school.Name;
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
            return new MaterializeResult(dto, failures, 0);
        }

        var create = await _routeService.CreateRouteAsync(new Route
        {
            RouteName = routeName,
            // Same UTC calendar day as DistrictWallClock stamps on the stop ETAs; DateTime.Today is the
            // local day, so an evening generation run dated the route one day off its own stops.
            Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            Description = $"008 {fleetKind} {slot} cell {pack.CellId}",
            IsActive = true,
            School = schoolDisplayName,
            Session = RouteSession.Infer(
                routeName,
                isSpecialNeedsRoute: false,
                description: $"008 {fleetKind} {slot} cell {pack.CellId}"),
            AMRiders = slot == RouteTimeSlotKind.AM ? pack.OrderedStudentIds.Count : null,
            PMRiders = slot == RouteTimeSlotKind.PM ? pack.OrderedStudentIds.Count : null
        }).ConfigureAwait(false);

        if (!create.IsSuccess || create.Value is null)
        {
            dto.Status = "Rejected";
            var msg = $"Create '{routeName}' failed: {create.Error}";
            failures.Add(msg);
            Logger.Warning("{Message}", msg);
            return new MaterializeResult(dto, failures, 0);
        }

        dto.PersistedRouteId = create.Value.RouteId;
        var routeId = create.Value.RouteId;

        await PersistScheduledStopsAsync(
                routeId, school, pack, slot, fleetKind, failures, cancellationToken, transferStopsByStudent)
            .ConfigureAwait(false);

        if (assignAm && slot == RouteTimeSlotKind.AM)
        {
            foreach (var studentId in pack.OrderedStudentIds)
            {
                var result = await _routeService.AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.AM)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    var msg = $"Assign AM Student={studentId} Route={routeName}: {result.Error}";
                    failures.Add(msg);
                    Logger.Warning("{Message}", msg);
                }
                else
                {
                    assignedCount++;
                }
            }
        }

        if (assignPmMirror && slot == RouteTimeSlotKind.PM)
        {
            foreach (var studentId in pack.OrderedStudentIds)
            {
                priorModeByStudent.TryGetValue(studentId, out var priorMode);
                if (!StudentRideModeHelper.ShouldAssignPmMirror(priorMode))
                {
                    Logger.Debug(
                        "PM mirror stop retained without PMRoute Student={Id} PriorMode={Mode}",
                        studentId, priorMode);
                    continue;
                }

                var result = await _routeService.AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.PM)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    var msg = $"Assign PM Student={studentId} Route={routeName}: {result.Error}";
                    failures.Add(msg);
                    Logger.Warning("{Message}", msg);
                }
            }
        }

        return new MaterializeResult(dto, failures, assignedCount);
    }

    private async Task PersistScheduledStopsAsync(
        int routeId,
        Destination school,
        PackedRoute pack,
        RouteTimeSlotKind slot,
        FleetKind fleetKind,
        List<string> failures,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, StudentSchoolTransfer>? transferStopsByStudent = null)
    {
        if (!LocationCoordinate.IsValidated(school.Latitude, school.Longitude))
        {
            failures.Add("School GPS missing — stop times not persisted");
            return;
        }

        var schLat = school.Latitude!.Value;
        var schLon = school.Longitude!.Value;

        await using var ctx = _contextFactory.CreateDbContext();
        var students = await ctx.Students.AsNoTracking()
            .Where(s => pack.OrderedStudentIds.Contains(s.StudentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byId = students.ToDictionary(s => s.StudentId);

        var pickupStopIds = students
            .Where(s => s.PickupStopId.HasValue)
            .Select(s => s.PickupStopId!.Value)
            .Distinct()
            .ToList();
        var pickupStops = pickupStopIds.Count == 0
            ? new Dictionary<int, PickupStop>()
            : await ctx.PickupStops.AsNoTracking()
                .Where(p => pickupStopIds.Contains(p.PickupStopId))
                .ToDictionaryAsync(p => p.PickupStopId, cancellationToken)
                .ConfigureAwait(false);

        var coords = new List<(double Lat, double Lon)>();
        var meta = new List<(List<int> StudentIds, string Name, string Address, decimal Lat, decimal Lon)>();
        var stopKeyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in pack.OrderedStudentIds)
        {
            if (fleetKind == FleetKind.Transfer &&
                transferStopsByStudent is not null &&
                transferStopsByStudent.TryGetValue(id, out var transfer) &&
                PickupScheduleCalculator.TryResolveTransferStop(
                    transfer, slot, out var tLat, out var tLon, out var tAddr))
            {
                byId.TryGetValue(id, out var student);
                var name = student?.StudentName ?? $"Student {id}";
                var transferKey = $"transfer:{id}";
                if (stopKeyIndex.TryGetValue(transferKey, out var tIdx))
                {
                    meta[tIdx].StudentIds.Add(id);
                    continue;
                }

                if (!RouteStop.IsValidatedCoordinate(tLat, tLon))
                {
                    failures.Add($"Transfer stop for {name} skipped — location is not validated");
                    continue;
                }

                stopKeyIndex[transferKey] = meta.Count;
                coords.Add(((double)tLat, (double)tLon));
                meta.Add((new List<int> { id }, name, tAddr, tLat, tLon));
                continue;
            }

            if (!byId.TryGetValue(id, out var s))
            {
                continue;
            }

            string stopKey;
            decimal lat;
            decimal lon;
            string stopName;
            string stopAddress;

            var specialNeedsHome = StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(s);
            if (AssignedHomeStopSync.UseCatalogPickup(s)
                && s.PickupStopId is int psId
                && pickupStops.TryGetValue(psId, out var catalogStop))
            {
                stopKey = $"pickup:{psId}";
                lat = catalogStop.Latitude;
                lon = catalogStop.Longitude;
                stopName = catalogStop.Name;
                stopAddress = catalogStop.Address ?? string.Empty;
            }
            else if (s.Latitude is not null && s.Longitude is not null)
            {
                stopKey = $"home:{id}";
                lat = s.Latitude.Value;
                lon = s.Longitude.Value;
                stopName = s.StudentName ?? $"Student {id}";
                stopAddress = s.HomeAddress ?? string.Empty;
            }
            else
            {
                if (specialNeedsHome)
                {
                    failures.Add($"Student {id} skipped — special-needs home is not validated");
                }

                continue;
            }

            if (!RouteStop.IsValidatedCoordinate(lat, lon))
            {
                failures.Add($"Stop '{stopName}' skipped — location is not validated");
                continue;
            }

            if (stopKeyIndex.TryGetValue(stopKey, out var idx))
            {
                meta[idx].StudentIds.Add(id);
                continue;
            }

            stopKeyIndex[stopKey] = meta.Count;
            coords.Add(((double)lat, (double)lon));
            meta.Add((new List<int> { id }, stopName, stopAddress, lat, lon));
        }

        if (coords.Count == 0)
        {
            return;
        }

        (_, meta) = await TryOptimizePickupOrderAsync(
                coords, meta, school, pack.SeatingCapacity, slot, cancellationToken)
            .ConfigureAwait(false);

        var afternoon = slot == RouteTimeSlotKind.PM;
        var built = new List<RouteStop>();
        var clocks = new List<ClockStop>();
        var dwell = TimeSpan.FromMinutes(DwellMinutes());

        void Append(ClockStop clock, string name, string address, decimal lat, decimal lon, string? notes)
        {
            clocks.Add(clock);
            built.Add(new RouteStop
            {
                RouteId = routeId,
                StopName = name,
                StopAddress = address,
                Latitude = lat,
                Longitude = lon,
                Notes = notes,
                CreatedDate = DateTime.UtcNow
            });
        }

        void AppendDepot()
        {
            if (!DistrictDepot.TryGetCoordinates(District, out var depotLat, out var depotLon))
            {
                return;
            }

            Append(
                new ClockStop(ClockStopKind.Depot, null, null, Array.Empty<int>()),
                DistrictDepot.GetDisplayName(District),
                DistrictDepot.GetDisplayAddress(District),
                (decimal)depotLat,
                (decimal)depotLon,
                null);
        }

        void AppendSchool()
        {
            Append(
                new ClockStop(
                    ClockStopKind.School,
                    school.DestinationId,
                    afternoon ? school.DismissalTime : school.StartTime,
                    Array.Empty<int>()),
                school.Name,
                school.Address ?? string.Empty,
                schLat,
                schLon,
                null);
        }

        AppendDepot();
        if (afternoon)
        {
            AppendSchool();
        }

        foreach (var m in meta)
        {
            var riderSchools = new HashSet<int>();
            foreach (var studentId in m.StudentIds)
            {
                if (byId.TryGetValue(studentId, out var rider) && rider.DestinationId is int destinationId)
                {
                    riderSchools.Add(destinationId);
                }
            }

            if (riderSchools.Count == 0)
            {
                riderSchools.Add(school.DestinationId);
            }

            var notes = m.StudentIds.Count == 1
                ? $"StudentId={m.StudentIds[0]}"
                : $"StudentIds={string.Join(",", m.StudentIds)}";
            Append(
                new ClockStop(ClockStopKind.Pickup, null, null, riderSchools.ToList()),
                m.Name,
                m.Address,
                m.Lat,
                m.Lon,
                notes);
        }

        if (!afternoon)
        {
            AppendSchool();
        }

        AppendDepot();

        var (legs, estimated) = await LegSecondsAsync(built, routeId, cancellationToken).ConfigureAwait(false);
        var plan = PublishedClockPlanner.Plan(clocks, legs, dwell, afternoon, estimated);
        if (!plan.Success || plan.BeginTime is not TimeSpan begin)
        {
            failures.Add($"Route {routeId}: {FirstWarning(plan.Warnings)}");
            return;
        }

        await using (var write = _contextFactory.CreateWriteDbContext())
        {
            var route = await write.Routes
                .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
                .ConfigureAwait(false);
            if (route is not null)
            {
                if (afternoon)
                {
                    route.PMBeginTime = begin;
                }
                else
                {
                    route.AMBeginTime = begin;
                }

                await write.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        for (var i = 0; i < built.Count; i++)
        {
            var stop = built[i];
            var arrival = plan.Arrivals[i];
            var departure = plan.Departures[i];
            stop.StopOrder = i + 1;
            stop.ScheduledArrival = arrival;
            stop.ScheduledDeparture = departure;
            stop.EstimatedArrivalTime = DistrictWallClock(arrival);
            stop.EstimatedDepartureTime = DistrictWallClock(departure);
            var add = await _routeService.AddStopToRouteAsync(routeId, stop).ConfigureAwait(false);
            if (!add.IsSuccess)
            {
                failures.Add($"Stop '{stop.StopName}': {add.Error}");
            }
        }
    }

    private async Task<(
        List<(double Lat, double Lon)> Coords,
        List<(List<int> StudentIds, string Name, string Address, decimal Lat, decimal Lon)> Meta)>
        TryOptimizePickupOrderAsync(
            List<(double Lat, double Lon)> coords,
            List<(List<int> StudentIds, string Name, string Address, decimal Lat, decimal Lon)> meta,
            Destination school,
            int seatingCapacity,
            RouteTimeSlotKind slot,
            CancellationToken cancellationToken)
    {
        if (_routeOptimization is not { IsConfigured: true } ||
            meta.Count < 2 ||
            !LocationCoordinate.IsValidated(school.Latitude, school.Longitude))
        {
            return (coords, meta);
        }

        try
        {
            var depotLat = (double)meta[0].Lat;
            var depotLon = (double)meta[0].Lon;
            if (DistrictDepot.TryGetCoordinates(District, out var barnLat, out var barnLon))
            {
                depotLat = barnLat;
                depotLon = barnLon;
            }

            var schoolStop = new RouteOptimizationStop
            {
                Label = "school",
                Latitude = (double)school.Latitude!,
                Longitude = (double)school.Longitude!,
            };
            var depotStop = new RouteOptimizationStop
            {
                Label = "depot",
                Latitude = depotLat,
                Longitude = depotLon,
            };
            var homes = meta.Select((m, i) => new RouteOptimizationStop
            {
                Label = i.ToString(),
                Latitude = (double)m.Lat,
                Longitude = (double)m.Lon,
                Load = Math.Max(1, m.StudentIds.Count),
            }).ToList();

            var isPm = slot == RouteTimeSlotKind.PM;
            var problem = isPm
                ? RouteOptimizationVisitOrder.ForDropoffRun(
                    schoolStop, homes, depotStop, seatingCapacity, DateTime.UtcNow)
                : RouteOptimizationVisitOrder.ForPickupRun(
                    depotStop, homes, schoolStop, seatingCapacity, DateTime.UtcNow);
            var result = await _routeOptimization.OptimizeToursAsync(problem, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                Logger.Warning("Route Optimization skipped for generation: {Error}", result.Error);
                return (coords, meta);
            }

            var original = new List<string> { isPm ? "school" : "depot" };
            original.AddRange(homes.Select(p => p.Label));
            original.Add(isPm ? "depot" : "school");
            var merged = RouteOptimizationVisitOrder.MergePinnedOrder(original, result.Visits);
            var newMeta = new List<(List<int> StudentIds, string Name, string Address, decimal Lat, decimal Lon)>(meta.Count);
            var newCoords = new List<(double Lat, double Lon)>(meta.Count);
            foreach (var label in merged)
            {
                if (!int.TryParse(label, out var idx) || idx < 0 || idx >= meta.Count)
                {
                    continue;
                }

                newMeta.Add(meta[idx]);
                newCoords.Add(coords[idx]);
            }

            if (newMeta.Count != meta.Count)
            {
                return (coords, meta);
            }

            Logger.Information(
                "Route Optimization reordered {Count} stops for {Slot} generation",
                newMeta.Count,
                slot);
            return (newCoords, newMeta);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Route Optimization skipped — keeping heuristic stop order");
            return (coords, meta);
        }
    }

    private static string SanitizeName(string name)
    {
        var cleaned = new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray())
            .Trim()
            .Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "School" : cleaned;
    }

    /// <summary>
    /// District wall-clock time for stop ETA display (<c>HH:mm</c>). A stop ETA is a face time —
    /// 07:00 means seven in the morning at the stop — so the time of day is stored as-is.
    /// <para>
    /// This deliberately no longer converts Mountain time to UTC. That conversion existed only to
    /// cancel out the old <c>Npgsql.EnableLegacyTimestampBehavior</c> readback, which returned
    /// <c>timestamptz</c> values as Local and turned an unconverted 07:00 into 01:00. With that switch
    /// gone, values read back exactly as written, so converting here would store 07:00 as 13:00 and
    /// display it that way. The UTC label satisfies Npgsql's <c>timestamptz</c> requirement and matches
    /// the Kind converters in <c>BusBuddyDbContext</c>.
    /// </para>
    /// </summary>
    private static DateTime DistrictWallClock(TimeSpan timeOfDay)
    {
        // UTC calendar day to match Route.Date, which normalises on DateTime.UtcNow.Date.
        return DateTime.SpecifyKind(DateTime.UtcNow.Date.Add(timeOfDay), DateTimeKind.Utc);
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

    private readonly record struct MaterializeResult(
        RouteProposalDto Dto,
        List<string> Failures,
        int AssignedCount);
}
