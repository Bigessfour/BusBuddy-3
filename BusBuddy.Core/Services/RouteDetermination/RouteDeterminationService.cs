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

    public RouteDeterminationService(
        IBusBuddyDbContextFactory contextFactory,
        IRouteService routeService,
        IOptions<RoutingDistrictSettings>? settings = null,
        AssignFitnessEvaluator? fitnessEvaluator = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _settings = settings?.Value ?? new RoutingDistrictSettings();
        _fitnessEvaluator = fitnessEvaluator
            ?? new AssignFitnessEvaluator(contextFactory, settings);
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
            return Fail(opId, schoolDestinationId, fleetKind, "Transfer fleet generation is not in MVP (see US4 / T033).");
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

        var students = await context.Students.AsNoTracking()
            .Where(s => s.DestinationId == schoolDestinationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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
                    school.Name,
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
                        school.Name,
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

    private async Task<int> ClearExistingDraftsAsync(
        string schoolSlug,
        string schoolDisplayName,
        CancellationToken cancellationToken)
    {
        var prefix = $"Draft-{schoolSlug}-";
        await using var context = _contextFactory.CreateWriteDbContext();
        var drafts = await context.Routes.AsTracking()
            .Where(r => r.RouteName.StartsWith(prefix) ||
                        (r.School == schoolDisplayName && r.RouteName.StartsWith("Draft-")))
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

        context.Routes.RemoveRange(drafts);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Logger.Information(
            "Cleared {Count} Draft route(s) for school slug={Slug} before regenerate",
            drafts.Count, schoolSlug);
        return drafts.Count;
    }

    private async Task<MaterializeResult> MaterializeProposalAsync(
        string routeName,
        string schoolDisplayName,
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
            // OrderedStudentIds on the PM DTO retain occasional-rider stops for AM-only.
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
