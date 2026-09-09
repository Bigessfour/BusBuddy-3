using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Bulk AM/PM route assignment and route optimization from the students grid.</summary>
public sealed class StudentsBulkRouteCoordinator
{
    private const int MaxBatch = 500;

    private static readonly ILogger Logger = Log.ForContext<StudentsBulkRouteCoordinator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IStudentService? _studentService;

    public StudentsBulkRouteCoordinator(IBusBuddyDbContextFactory contextFactory, IStudentService? studentService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _studentService = studentService;
    }

    /// <summary>
    /// Students a bulk assignment should touch: the selected row when there is one, otherwise every
    /// visible student still missing an AM or PM route, capped at <see cref="MaxBatch"/>.
    /// </summary>
    public static IReadOnlyList<StudentModel> SelectCandidates(
        IReadOnlyList<StudentModel> visibleStudents,
        StudentModel? selectedStudent)
    {
        List<StudentModel> candidates = selectedStudent is not null
            ? [selectedStudent]
            : visibleStudents
                .Where(s => string.IsNullOrWhiteSpace(s.AMRoute) || string.IsNullOrWhiteSpace(s.PMRoute))
                .ToList();

        if (candidates.Count > MaxBatch)
        {
            candidates = candidates.Take(MaxBatch).ToList();
            Logger.Warning("Bulk assignment candidate list truncated to {MaxBatch}", MaxBatch);
        }

        return candidates;
    }

    /// <summary>
    /// Assigns unassigned students to active routes, then asks local Ollama (or mock AI) for
    /// commentary. Routes are daily published runs, never trips (specs/routes.md).
    /// </summary>
    public async Task<StudentRouteOptimizeResult> OptimizeUnassignedAsync()
    {
        var optimizer = App.ServiceProvider?.GetService<IStudentRouteOptimizer>()
            ?? new StudentRouteOptimizer(new RouteService(_contextFactory));
        return await optimizer.OptimizeUnassignedAsync();
    }

    public async Task<(int Affected, int Errors, string RouteName)> AssignAsync(
        IReadOnlyList<RouteModel> routeCatalog,
        IReadOnlyList<StudentModel> candidates,
        StudentModel? selectedStudent)
    {
        if (routeCatalog.Count == 0)
        {
            return (0, 0, string.Empty);
        }

        var targetRoute = routeCatalog.FirstOrDefault(r => r.IsActive) ?? routeCatalog[0];
        if (candidates.Count == 0)
        {
            return (0, 0, targetRoute.RouteName);
        }

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        var affected = 0;
        var errors = 0;

        foreach (var student in candidates)
        {
            if (selectedStudent is not null
                && student.StudentId == selectedStudent.StudentId
                && !string.IsNullOrWhiteSpace(student.AMRoute)
                && !string.IsNullOrWhiteSpace(student.PMRoute))
            {
                student.AMRoute = targetRoute.RouteName;
            }
            else if (string.IsNullOrWhiteSpace(student.AMRoute))
            {
                student.AMRoute = targetRoute.RouteName;
            }
            else if (string.IsNullOrWhiteSpace(student.PMRoute))
            {
                student.PMRoute = targetRoute.RouteName;
            }
            else
            {
                continue;
            }

            StudentRecordNormalizer.NormalizeForPersistence(student);

            try
            {
                if (studentService is not null)
                {
                    await studentService.UpdateStudentAsync(student).ConfigureAwait(true);
                }
                else
                {
                    using var context = _contextFactory.CreateWriteDbContext();
                    context.Students.Update(student);
                    await context.SaveChangesAsync().ConfigureAwait(true);
                }

                affected++;
            }
            catch (Exception ex)
            {
                errors++;
                Logger.Warning(ex, "Bulk route assign failed for student {StudentId}", student.StudentId);
            }
        }

        if (affected > 0)
        {
            await RecomputeRouteStudentCountAsync(targetRoute).ConfigureAwait(true);
        }

        Logger.Information(
            "Bulk route assignment completed: Route {RouteId}:{RouteName} applied to {Count} students ({Errors} errors)",
            targetRoute.RouteId,
            targetRoute.RouteName,
            affected,
            errors);
        return (affected, errors, targetRoute.RouteName);
    }

    private async Task RecomputeRouteStudentCountAsync(RouteModel targetRoute)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var routeEntity = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteId == targetRoute.RouteId)
                .ConfigureAwait(true);
            if (routeEntity is null)
            {
                return;
            }

            var routeName = routeEntity.RouteName;
            routeEntity.StudentCount = await context.Students.CountAsync(
                s => s.AMRoute == routeName || s.PMRoute == routeName).ConfigureAwait(true);
            await context.SaveChangesAsync().ConfigureAwait(true);
            Logger.Information(
                "Route.StudentCount recomputed — RouteId={RouteId}, StudentCount={StudentCount}",
                routeEntity.RouteId,
                routeEntity.StudentCount);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed recomputing Route.StudentCount after bulk assignment");
        }
    }
}
