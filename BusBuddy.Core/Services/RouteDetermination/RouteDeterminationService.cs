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
    private readonly AssignFitnessEvaluator _fitnessEvaluator;
    private readonly IRouteWaypointRebuildService? _waypointRebuild;

    public RouteDeterminationService(
        IBusBuddyDbContextFactory contextFactory,
        IRouteService routeService,
        IOptions<RoutingDistrictSettings>? settings = null,
        AssignFitnessEvaluator? fitnessEvaluator = null,
        IRouteWaypointRebuildService? waypointRebuild = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _settings = settings?.Value ?? new RoutingDistrictSettings();
        _fitnessEvaluator = fitnessEvaluator
            ?? new AssignFitnessEvaluator(contextFactory, settings);
        _waypointRebuild = waypointRebuild;
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

        var cells = DensityCellBuilder.Build(riders, _settings);
        var packed = new List<(DensityCell Cell, PackedRoute Pack)>();
        foreach (var cell in cells)
        {
            foreach (var pack in RoutePacker.PackCell(cell, seating, _settings))
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

        var mode = StudentRideModeHelper.FromStudent(student);
        var remove = await _routeService.RemoveStudentFromRouteAsync(studentId, fromRouteId, timeSlot)
            .ConfigureAwait(false);
        if (!remove.IsSuccess)
        {
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

        if (school.Latitude is not decimal schLat || school.Longitude is not decimal schLon)
        {
            return Fail(opId, schoolDestinationId, FleetKind.HomeToSchool, "School GPS required for schedule regen");
        }

        var routes = await context.Routes.AsNoTracking()
            .Where(r => r.IsActive && r.School == school.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var updated = 0;
        var failures = new List<string>();
        foreach (var route in routes)
        {
            var stopsResult = await _routeService.GetRouteStopsAsync(route.RouteId).ConfigureAwait(false);
            if (!stopsResult.IsSuccess || stopsResult.Value is null || !stopsResult.Value.Any())
            {
                continue;
            }

            var ordered = stopsResult.Value.OrderBy(s => s.StopOrder).ToList();
            var coords = ordered
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
                .ToList();
            if (coords.Count == 0)
            {
                continue;
            }

            IReadOnlyList<TimeSpan> arrivals;
            if (route.RouteName.EndsWith("-PM", StringComparison.OrdinalIgnoreCase) &&
                school.DismissalTime is TimeSpan dismissal)
            {
                arrivals = PickupScheduleCalculator.ComputePmDropoffArrivals(
                    coords, (double)schLat, (double)schLon, dismissal, _settings);
            }
            else if (school.StartTime is TimeSpan start)
            {
                arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
                    coords, (double)schLat, (double)schLon, start, _settings);
            }
            else
            {
                failures.Add($"Route {route.RouteName}: missing StartTime/DismissalTime");
                continue;
            }

            var timed = new List<RouteStop>();
            var ai = 0;
            foreach (var stop in ordered)
            {
                if (!stop.Latitude.HasValue)
                {
                    timed.Add(stop);
                    continue;
                }

                if (ai < arrivals.Count)
                {
                    stop.ScheduledArrival = arrivals[ai];
                    stop.ScheduledDeparture = arrivals[ai] + PickupScheduleCalculator.DefaultDwell;
                    ai++;
                }

                timed.Add(stop);
            }

            var persist = await _routeService.UpdateRouteStopsTimingAsync(route.RouteId, timed)
                .ConfigureAwait(false);
            if (persist.IsSuccess)
            {
                updated++;
            }
            else
            {
                failures.Add(persist.Error ?? $"Timing persist failed for {route.RouteName}");
            }
        }

        Logger.Information(
            "Schedule regen School={SchoolId} RoutesUpdated={N} OpId={OpId}",
            schoolDestinationId, updated, opId);

        return new RouteGenerationResult
        {
            OperationId = opId,
            SchoolDestinationId = schoolDestinationId,
            FleetKind = FleetKind.HomeToSchool,
            Success = failures.Count == 0,
            AssignedStudentCount = updated,
            Warnings = failures,
            Error = failures.Count == 0 ? null : string.Join("; ", failures.Take(3))
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
        var cells = DensityCellBuilder.Build(riders, _settings);
        var packed = cells.SelectMany(c => RoutePacker.PackCell(c, seating, _settings)).ToList();
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
        var proposals = new List<RouteProposalDto>();
        var hardFailures = new List<string>();
        var assigned = 0;
        var idx = 0;
        foreach (var pack in packed)
        {
            idx++;
            var cellId = pack.CellId;
            var name = $"{prefix}{cellId}-{idx}";
            var result = await MaterializeProposalAsync(
                    name,
                    school,
                    pack,
                    slot == RouteTimeSlotKind.PM ? RouteTimeSlotKind.PM : RouteTimeSlotKind.AM,
                    FleetKind.Transfer,
                    options.DryRun,
                    assignAm: slot is not RouteTimeSlotKind.PM,
                    priorMode,
                    cancellationToken,
                    assignPmMirror: !options.DryRun && slot == RouteTimeSlotKind.Both)
                .ConfigureAwait(false);
            proposals.Add(result.Dto);
            hardFailures.AddRange(result.Failures);
            assigned += result.AssignedCount;

            if (!options.DryRun && result.Dto.PersistedRouteId is int rid && _waypointRebuild is not null)
            {
                try
                {
                    await _waypointRebuild.RebuildAndPersistAsync(rid, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Transfer waypoint rebuild failed RouteId={Id}", rid);
                }
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
        bool assignPmMirror = false)
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
            Date = DateTime.Today,
            Description = $"008 {fleetKind} {slot} cell {pack.CellId}",
            IsActive = true,
            School = schoolDisplayName,
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

        await PersistScheduledStopsAsync(routeId, school, pack, slot, failures, cancellationToken)
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
        List<string> failures,
        CancellationToken cancellationToken)
    {
        if (school.Latitude is not decimal schLat || school.Longitude is not decimal schLon)
        {
            failures.Add("School GPS missing — stop times not persisted");
            return;
        }

        await using var ctx = _contextFactory.CreateDbContext();
        var students = await ctx.Students.AsNoTracking()
            .Where(s => pack.OrderedStudentIds.Contains(s.StudentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byId = students.ToDictionary(s => s.StudentId);

        var coords = new List<(double Lat, double Lon)>();
        var meta = new List<(int StudentId, string Name, string Address, decimal? Lat, decimal? Lon)>();
        foreach (var id in pack.OrderedStudentIds)
        {
            if (!byId.TryGetValue(id, out var s) || s.Latitude is null || s.Longitude is null)
            {
                continue;
            }

            coords.Add(((double)s.Latitude.Value, (double)s.Longitude.Value));
            meta.Add((id, s.StudentName ?? $"Student {id}", s.HomeAddress ?? string.Empty, s.Latitude, s.Longitude));
        }

        if (coords.Count == 0)
        {
            return;
        }

        IReadOnlyList<TimeSpan> arrivals;
        if (slot == RouteTimeSlotKind.PM && school.DismissalTime is TimeSpan dismissal)
        {
            arrivals = PickupScheduleCalculator.ComputePmDropoffArrivals(
                coords, (double)schLat, (double)schLon, dismissal, _settings);
        }
        else if (school.StartTime is TimeSpan start)
        {
            arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
                coords, (double)schLat, (double)schLon, start, _settings);
        }
        else
        {
            arrivals = Enumerable.Range(0, coords.Count).Select(_ => TimeSpan.FromHours(7)).ToList();
        }

        for (var i = 0; i < meta.Count; i++)
        {
            var m = meta[i];
            var arrival = i < arrivals.Count ? arrivals[i] : TimeSpan.FromHours(7);
            var stop = new RouteStop
            {
                RouteId = routeId,
                StopName = m.Name,
                StopAddress = m.Address,
                Latitude = m.Lat,
                Longitude = m.Lon,
                StopOrder = i + 1,
                ScheduledArrival = arrival,
                ScheduledDeparture = arrival + PickupScheduleCalculator.DefaultDwell,
                Notes = $"StudentId={m.StudentId}",
                CreatedDate = DateTime.UtcNow,
                EstimatedArrivalTime = DateTime.Today.Add(arrival),
                EstimatedDepartureTime = DateTime.Today.Add(arrival + PickupScheduleCalculator.DefaultDwell)
            };
            var add = await _routeService.AddStopToRouteAsync(routeId, stop).ConfigureAwait(false);
            if (!add.IsSuccess)
            {
                failures.Add($"Stop '{m.Name}': {add.Error}");
            }
        }
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
