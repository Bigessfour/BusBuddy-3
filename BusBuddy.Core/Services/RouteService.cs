using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics; // Added for Stopwatch timing (basic instrumentation)

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Route Service implementation with comprehensive route management capabilities
    /// Implements Result pattern for robust error handling and logging
    /// Route building implementation
    /// Updated: Uses IBusBuddyDbContextFactory for consistent dependency injection
    /// </summary>
    public partial class RouteService : IRouteService
    {
        private static readonly ILogger Logger = Log.ForContext<RouteService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IRouteWaypointRebuildService? _waypointRebuild;
        private readonly AssignFitnessEvaluator? _fitnessEvaluator;
        private readonly IRoutingService? _routingService;

        // Minimal op timing helper (basic only; can expand later)
        private static (Guid OpId, Stopwatch Sw) StartOp(string name, object? routeId = null)
        {
            var opId = Guid.NewGuid();
            var sw = Stopwatch.StartNew();
            Logger.Debug("BEGIN {Op} OpId={OpId} RouteId={RouteId}", name, opId, routeId);
            return (opId, sw);
        }
        private static void EndOpOk(string name, Guid opId, Stopwatch sw, object? routeId = null, int? count = null)
        {
            sw.Stop();
            if (count.HasValue)
                Logger.Debug("END   {Op} OpId={OpId} RouteId={RouteId} Count={Count} ElapsedMs={Ms}", name, opId, routeId, count.Value, sw.ElapsedMilliseconds);
            else
                Logger.Debug("END   {Op} OpId={OpId} RouteId={RouteId} ElapsedMs={Ms}", name, opId, routeId, sw.ElapsedMilliseconds);
        }

        public RouteService(IBusBuddyDbContextFactory contextFactory)
            : this(contextFactory, null, null, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild)
            : this(contextFactory, waypointRebuild, null, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild,
            AssignFitnessEvaluator? fitnessEvaluator)
            : this(contextFactory, waypointRebuild, fitnessEvaluator, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild,
            AssignFitnessEvaluator? fitnessEvaluator,
            IRoutingService? routingService)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _waypointRebuild = waypointRebuild;
            _fitnessEvaluator = fitnessEvaluator;
            _routingService = routingService;
        }

        // Context helpers: only dispose when using the concrete runtime factory
        // This prevents disposing shared in-memory contexts used by tests.
        private (BusBuddyDbContext Ctx, bool Dispose) GetReadContext()
        {
            var ctx = _contextFactory.CreateDbContext();
            var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
            return (ctx, shouldDispose);
        }

        private (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext()
        {
            var ctx = _contextFactory.CreateWriteDbContext();
            var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
            return (ctx, shouldDispose);
        }

        /// <summary>
        /// Runs a multi-<c>SaveChanges</c> operation as one retriable unit, rolling back when the operation reports
        /// failure so a rejected step cannot leave partial writes committed. Npgsql is configured with
        /// <c>EnableRetryOnFailure</c>, and a retrying execution strategy rejects a transaction started outside it,
        /// so the transaction must be opened inside the delegate.
        /// https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions
        /// </summary>
        private static Task<Result<T>> InTransactionAsync<T>(
            BusBuddyDbContext context,
            Func<Task<Result<T>>> operation)
        {
            return context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);
                var result = await operation().ConfigureAwait(false);
                if (result.IsFailure)
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    return result;
                }

                await transaction.CommitAsync().ConfigureAwait(false);
                return result;
            });
        }

        #region Basic CRUD Operations

        public async Task<Result<IEnumerable<Route>>> GetAllActiveRoutesAsync()
        {
            try
            {
                Logger.Information("Retrieving all active routes");
                var (context, dispose) = GetReadContext();
                try
                {
                    var routes = await context.Routes
                        .Where(r => r.IsActive)
                        .Include(r => r.AMVehicle)
                        .Include(r => r.PMVehicle)
                        .AsNoTracking()
                        .OrderBy(r => r.RouteName)
                        .ToListAsync();

                    await ApplyListMetricsAsync(context, routes);

                    Logger.Information("Retrieved {Count} active routes", routes.Count);
                    return Result.SuccessResult(routes.AsEnumerable());
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving active routes");
                return Result.FailureResult<IEnumerable<Route>>($"Error retrieving routes: {ex.Message}");
            }
        }

        public async Task<Result<IEnumerable<Route>>> GetAllRoutesAsync()
        {
            try
            {
                Logger.Information("Retrieving all routes");
                var (context, dispose) = GetReadContext();
                try
                {
                    var routes = await context.Routes
                        .Include(r => r.AMVehicle)
                        .Include(r => r.PMVehicle)
                        .AsNoTracking()
                        .OrderBy(r => r.RouteName)
                        .ToListAsync();

                    await ApplyListMetricsAsync(context, routes);

                    return Result.SuccessResult(routes.AsEnumerable());
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving all routes");
                return Result.FailureResult<IEnumerable<Route>>($"Error retrieving routes: {ex.Message}");
            }
        }

        public async Task<Result<Route>> GetRouteByIdAsync(int id)
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var route = await context.Routes
                        .Include(r => r.AMVehicle)
                        .Include(r => r.PMVehicle)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.RouteId == id);
                    if (route == null)
                    {
                        return Result.FailureResult<Route>($"Route with ID {id} not found");
                    }

                    await ApplyListMetricsAsync(context, new List<Route> { route });

                    return Result.SuccessResult(route);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving route {RouteId}", id);
                return Result.FailureResult<Route>($"Error retrieving route: {ex.Message}");
            }
        }

        public async Task<Result<Route>> CreateRouteAsync(Route route)
        {
            try
            {
                Logger.Information("Creating new route: {RouteName}", route.RouteName);
                var (context, dispose) = GetWriteContext();
                try
                {
                    if (string.IsNullOrWhiteSpace(route.Session) || !RouteSession.IsKnown(route.Session))
                    {
                        route.Session = RouteSession.Infer(route);
                    }
                    else if (route.Session == RouteSession.AM)
                    {
                        var inferred = RouteSession.Infer(route);
                        if (inferred != RouteSession.AM)
                        {
                            route.Session = inferred;
                        }
                    }

                    context.Routes.Add(route);
                    await context.SaveChangesAsync();

                    Logger.Information("Successfully created route {RouteId}: {RouteName}", route.RouteId, route.RouteName);
                    return Result.SuccessResult(route);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error creating route {RouteName}", route.RouteName);
                return Result.FailureResult<Route>($"Error creating route: {ex.Message}");
            }
        }

        public async Task<Result<Route>> UpdateRouteAsync(Route route)
        {
            try
            {
                Logger.Information("Updating route {RouteId}: {RouteName}", route.RouteId, route.RouteName);
                var (context, dispose) = GetWriteContext();
                try
                {
                    var tracked = await context.Routes
                        .AsTracking()
                        .FirstOrDefaultAsync(r => r.RouteId == route.RouteId);
                    if (tracked is null)
                    {
                        return Result.FailureResult<Route>($"Route with ID {route.RouteId} not found");
                    }

                    var previousName = tracked.RouteName;
                    var previousDescription = tracked.Description;
                    var previousSpecialNeeds = tracked.IsSpecialNeedsRoute;

                    context.Entry(tracked).CurrentValues.SetValues(route);

                    var renamed = !string.Equals(previousName, tracked.RouteName, StringComparison.Ordinal);
                    var sessionInputsChanged = renamed
                        || !string.Equals(previousDescription, tracked.Description, StringComparison.Ordinal)
                        || previousSpecialNeeds != tracked.IsSpecialNeedsRoute;
                    if (sessionInputsChanged
                        || !RouteSession.IsKnown(tracked.Session)
                        || tracked.Session == RouteSession.AM)
                    {
                        tracked.Session = RouteSession.Infer(tracked);
                    }

                    if (renamed)
                    {
                        await CascadeRouteRenameAsync(context, tracked.RouteId, previousName, tracked.RouteName);
                    }

                    await context.SaveChangesAsync();

                    return Result.SuccessResult(tracked);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating route {RouteId}", route.RouteId);
                return Result.FailureResult<Route>($"Error updating route: {ex.Message}");
            }
        }

        /// <summary>
        /// Follows a route rename into the denormalised <see cref="Student.AMRoute"/> /
        /// <see cref="Student.PMRoute"/> name strings. Riders are resolved by
        /// <see cref="Student.AmRouteId"/> / <see cref="Student.PmRouteId"/>, so the rename follows the route's
        /// identity and cannot be confused by another route that happens to share the old name.
        /// </summary>
        private static async Task CascadeRouteRenameAsync(
            BusBuddyDbContext context,
            int routeId,
            string previousName,
            string newName)
        {
            var riders = await context.Students
                .AsTracking()
                .Where(s => s.AmRouteId == routeId || s.PmRouteId == routeId)
                .ToListAsync();

            foreach (var rider in riders)
            {
                if (rider.AmRouteId == routeId)
                {
                    rider.AMRoute = newName;
                }

                if (rider.PmRouteId == routeId)
                {
                    rider.PMRoute = newName;
                }
            }

            if (riders.Count > 0)
            {
                Logger.Information(
                    "Route {RouteId} renamed from {PreviousName} to {NewName}; followed {RiderCount} rider assignments",
                    routeId,
                    previousName,
                    newName,
                    riders.Count);
            }
        }

        public async Task<Result<bool>> DeleteRouteAsync(int id)
        {
            try
            {
                var (context, dispose) = GetWriteContext();
                try
                {
                    context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == id);
                    if (route == null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {id} not found");
                    }

                    var routeName = route.RouteName;
                    var scheduleCount = await context.Schedules.CountAsync(s => s.RouteId == id);
                    var studentFkCount = await context.Students.CountAsync(s =>
                        s.AmRouteId == id || s.PmRouteId == id);
                    var tripCount = await context.TripEvents.CountAsync(t => t.RouteId == id);
                    var blockers = scheduleCount + studentFkCount + tripCount;
                    if (blockers > 0)
                    {
                        route.IsActive = false;
                        await context.SaveChangesAsync();
                        var message =
                            $"Route retired — {scheduleCount} schedule row(s) still reference it"
                            + (studentFkCount > 0 ? $", {studentFkCount} student assignment(s)" : string.Empty)
                            + (tripCount > 0 ? $", {tripCount} trip event(s)" : string.Empty)
                            + ".";
                        Logger.Information(
                            "Soft-retired route {RouteId} Schedules={Schedules} Students={Students} Trips={Trips}",
                            id,
                            scheduleCount,
                            studentFkCount,
                            tripCount);
                        return Result.SuccessResult(true, message);
                    }

                    var assignmentIds = await context.RouteAssignments
                        .Where(a => a.RouteId == id)
                        .Select(a => a.RouteAssignmentId)
                        .ToListAsync();

                    var routeNameLower = routeName.ToLowerInvariant();

                    // CA1311/CA1862: ToLowerInvariant and StringComparison overloads have no SQL translation;
                    // ToLower() is the form EF maps to the database LOWER() function, which is what runs here.
#pragma warning disable CA1311, CA1862
                    var assignedStudents = await context.Students
                        .Where(s => s.AmRouteId == id
                                 || s.PmRouteId == id
                                 || (s.AMRoute != null && s.AMRoute.ToLower() == routeNameLower)
                                 || (s.PMRoute != null && s.PMRoute.ToLower() == routeNameLower))
                        .ToListAsync();
#pragma warning restore CA1311, CA1862
                    if (assignmentIds.Count > 0)
                    {
                        var linked = await context.Students
                            .Where(s => s.RouteAssignmentId != null && assignmentIds.Contains(s.RouteAssignmentId.Value))
                            .ToListAsync();
                        assignedStudents = assignedStudents
                            .Concat(linked)
                            .DistinctBy(s => s.StudentId)
                            .ToList();
                    }

                    foreach (var student in assignedStudents)
                    {
                        if (StudentRouteAssignment.Matches(student, route, RouteTimeSlot.AM))
                        {
                            StudentRouteAssignment.SetSlot(student, RouteTimeSlot.AM, route: null);
                        }

                        if (StudentRouteAssignment.Matches(student, route, RouteTimeSlot.PM))
                        {
                            StudentRouteAssignment.SetSlot(student, RouteTimeSlot.PM, route: null);
                        }

                        if (student.RouteAssignmentId is > 0
                            && assignmentIds.Contains(student.RouteAssignmentId.Value))
                        {
                            student.RouteAssignmentId = null;
                        }
                    }

                    var assignments = await context.RouteAssignments
                        .Where(a => a.RouteId == id)
                        .ToListAsync();
                    if (assignments.Count > 0)
                    {
                        context.RouteAssignments.RemoveRange(assignments);
                    }

                    var stops = await context.RouteStops
                        .Where(s => s.RouteId == id)
                        .ToListAsync();
                    if (stops.Count > 0)
                    {
                        context.RouteStops.RemoveRange(stops);
                    }

                    var exceptions = await context.RouteRiderExceptions
                        .Where(e => e.RouteId == id)
                        .ToListAsync();
                    if (exceptions.Count > 0)
                    {
                        context.RouteRiderExceptions.RemoveRange(exceptions);
                    }

                    context.Routes.Remove(route);
                    await context.SaveChangesAsync();

                    Logger.Information(
                        "Hard-deleted route {RouteId} after unassigning {StudentCount} name-only riders, {AssignmentCount} vehicle assignments",
                        id,
                        assignedStudents.Count,
                        assignments.Count);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error deleting route {RouteId}", id);
                var detail = DatabaseUserMessage.ForOperation(ex, "delete this route");
                if (detail.Contains("related record", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
                    || detail.Contains("FK_Schedules_Route", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.FailureResult<bool>(
                        "Cannot delete this route. If daily schedules, student keys, or trip events still reference it, it should retire instead of hard-delete. Empty routes can be deleted.");
                }

                return Result.FailureResult<bool>(detail);
            }
        }

        #endregion

        #region Route Building Methods

        public async Task<Result<Route>> CreateNewRouteAsync(string routeName, DateTime routeDate, string? description = null)
        {
            try
            {
                Logger.Information("Creating new route: {RouteName} for date {RouteDate}", routeName, routeDate);

                // Validation
                if (string.IsNullOrWhiteSpace(routeName))
                {
                    return Result.FailureResult<Route>("Route name is required");
                }

                if (routeDate.Date < DateTime.UtcNow.Date)
                {
                    return Result.FailureResult<Route>("Route date cannot be in the past");
                }

                // Check for duplicate route name on the same date
                var (context, dispose) = GetWriteContext();
                try
                {
                    var existingRoute = await context.Routes
                        .FirstOrDefaultAsync(r => r.RouteName == routeName && r.Date.Date == routeDate.Date);

                    if (existingRoute != null)
                    {
                        return Result.FailureResult<Route>($"A route with name '{routeName}' already exists for {routeDate:yyyy-MM-dd}");
                    }

                    // Create new route
                    var newRoute = new Route
                    {
                        RouteName = routeName,
                        Date = routeDate,
                        Description = description,
                        IsActive = false, // Start inactive until fully configured
                        School = "Default School" // This should come from configuration
                    };
                    newRoute.Session = RouteSession.Infer(newRoute);

                    context.Routes.Add(newRoute);
                    await context.SaveChangesAsync();

                    Logger.Information("Successfully created route {RouteId}: {RouteName}", newRoute.RouteId, routeName);
                    return Result.SuccessResult(newRoute);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error creating new route {RouteName}", routeName);
                return Result.FailureResult<Route>($"Error creating route: {ex.Message}");
            }
        }

        public async Task<Result<RouteValidationResult>> ValidateRouteForActivationAsync(int routeId)
        {
            try
            {
                Logger.Information("Validating route {RouteId} for activation", routeId);

                var validationResult = new RouteValidationResult { IsValid = true };
                var (context, dispose) = GetReadContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        validationResult.IsValid = false;
                        validationResult.Issues.Add($"Route {routeId} not found");
                        return Result.SuccessResult(validationResult);
                    }

                    // Basic validation - route exists and has a name
                    if (string.IsNullOrWhiteSpace(route.RouteName))
                    {
                        validationResult.Issues.Add("Route name is required");
                    }

                    if (route.Date.Date < DateTime.UtcNow.Date)
                    {
                        validationResult.Issues.Add("Route date cannot be in the past");
                    }

                    // Name + date is the intended scope here. Bus/driver/stop/student checks live
                    // in the assign and stop services so this stays a cheap pre-save gate.

                    validationResult.IsValid = validationResult.Issues.Count == 0;

                    Logger.Information("Route validation completed. Valid: {IsValid}, Issues: {IssueCount}",
                        validationResult.IsValid, validationResult.Issues.Count);

                    return Result.SuccessResult(validationResult);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error validating route {RouteId}", routeId);
                return Result.FailureResult<RouteValidationResult>($"Error validating route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ActivateRouteAsync(int routeId)
        {
            try
            {
                Logger.Information("Activating route {RouteId}", routeId);
                // Skip validation here (already covered in separate tests)

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route == null)
                    {
                        Logger.Warning("ActivateRoute — route {RouteId} not found", routeId);
                        return Result.FailureResult<bool>($"Route {routeId} not found");
                    }
                    if (route.IsActive)
                    {
                        Logger.Information("ActivateRoute — route {RouteId} already active", routeId);
                        return Result.SuccessResult(true); // idempotent
                    }
                    route.IsActive = true;
                    context.Entry(route).Property(r => r.IsActive).IsModified = true; // force persistence
                    await context.SaveChangesAsync();
                    Logger.Information("Successfully activated route {RouteId}: {RouteName}", routeId, route.RouteName);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error activating route {RouteId}", routeId);
                return Result.FailureResult<bool>($"Error activating route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> DeactivateRouteAsync(int routeId)
        {
            try
            {
                var (opId, sw) = StartOp("DeactivateRoute", routeId);
                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route == null)
                    {
                        var existingIds = await context.Routes.Select(r => r.RouteId).ToListAsync();
                        Logger.Warning("DeactivateRoute — route {RouteId} not found OpId={OpId} ExistingRouteIds=[{Ids}]", routeId, opId, string.Join(',', existingIds));
                        return Result.FailureResult<bool>($"Route {routeId} not found");
                    }

                    if (!route.IsActive)
                    {
                        Logger.Information("DeactivateRoute — route {RouteId} already inactive OpId={OpId}", routeId, opId);
                        EndOpOk("DeactivateRoute", opId, sw, routeId);
                        return Result.SuccessResult(true); // idempotent
                    }

                    route.IsActive = false; // toggle flag
                    context.Entry(route).Property(r => r.IsActive).IsModified = true; // force persistence
                    await context.SaveChangesAsync();

                    Logger.Information("Successfully deactivated route {RouteId} OpId={OpId}", routeId, opId);
                    EndOpOk("DeactivateRoute", opId, sw, routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error deactivating route {RouteId}", routeId);
                return Result.FailureResult<bool>($"Error deactivating route: {ex.Message}");
            }
        }

        #endregion

        #region Route stops, assignments and lookups

        public async Task<Result<IEnumerable<RouteStop>>> GetRouteStopsAsync(int routeId)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<IEnumerable<RouteStop>>("Invalid routeId");
                }

                var (context, dispose) = GetReadContext();
                try
                {
                    var stops = await context.RouteStops
                        .Where(rs => rs.RouteId == routeId)
                        .OrderBy(rs => rs.StopOrder)
                        .AsNoTracking()
                        .ToListAsync();
                    return Result.SuccessResult(stops.AsEnumerable());
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving stops for route {RouteId}", routeId);
                return Result.FailureResult<IEnumerable<RouteStop>>($"Error retrieving route stops: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ReorderRouteStopsAsync(int routeId, List<int> orderedStopIds)
        {
            try
            {
                var (opId, sw) = StartOp("ReorderRouteStops", routeId);
                if (routeId <= 0 || orderedStopIds == null || orderedStopIds.Count == 0)
                {
                    return Result.FailureResult<bool>("Invalid input for reordering stops");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var stops = await context.RouteStops
                            .Where(rs => rs.RouteId == routeId)
                            .OrderBy(rs => rs.StopOrder)
                            .ToListAsync();

                        // Capture original ordering snapshot for diagnostics
                        var originalOrder = stops.Select(s => new { s.RouteStopId, s.StopOrder }).ToList();
                        Logger.Debug("ReorderRouteStops pre-state RouteId={RouteId} OpId={OpId} Original={Original}",
                            routeId,
                            opId,
                            string.Join(",", originalOrder.Select(o => $"{o.RouteStopId}:{o.StopOrder}")));

                        if (stops.Count != orderedStopIds.Count)
                        {
                            return Result.FailureResult<bool>("Ordered stop IDs count does not match existing stop count for route");
                        }

                        // Ensure all IDs exist
                        var stopIdSet = stops.Select(s => s.RouteStopId).ToHashSet();
                        if (orderedStopIds.Any(id => !stopIdSet.Contains(id)))
                        {
                            return Result.FailureResult<bool>("One or more stop IDs not found for route during reorder");
                        }

                        // Assign new order by position in orderedStopIds
                        int order = 1;
                        foreach (var id in orderedStopIds)
                        {
                            var s = stops.First(st => st.RouteStopId == id);
                            if (s.StopOrder != order)
                            {
                                s.StopOrder = order;
                                s.UpdatedDate = DateTime.UtcNow;
                            }
                            order++;
                        }

                        foreach (var s in stops)
                        {
                            context.Entry(s).Property(x => x.StopOrder).IsModified = true;
                        }

                        var routeEntity = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                        if (routeEntity is not null)
                        {
                            var orderedStops = orderedStopIds
                                .Select(id => stops.First(s => s.RouteStopId == id))
                                .ToList();
                            routeEntity.WaypointsJson = RouteWaypointSerializer.FromPairs(
                                orderedStops
                                    .Where(s => RouteStop.IsValidatedCoordinate(s.Latitude, s.Longitude))
                                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
                            context.Entry(routeEntity).Property(r => r.WaypointsJson).IsModified = true;
                        }

                        var affected = await context.SaveChangesAsync();

                        // Reload to verify persistence
                        var reloaded = await context.RouteStops
                            .Where(rs => rs.RouteId == routeId)
                            .OrderBy(rs => rs.StopOrder)
                            .Select(rs => new { rs.RouteStopId, rs.StopOrder })
                            .ToListAsync();

                        Logger.Debug("ReorderRouteStops post-state RouteId={RouteId} OpId={OpId} New={New}",
                            routeId,
                            opId,
                            string.Join(",", reloaded.Select(o => $"{o.RouteStopId}:{o.StopOrder}")));

                        var changed = !originalOrder.SequenceEqual(reloaded.Select(r => new { r.RouteStopId, r.StopOrder }));
                        if (!changed)
                        {
                            Logger.Warning("ReorderRouteStops detected no persisted change RouteId={RouteId} OpId={OpId} Affected={Affected}", routeId, opId, affected);
                        }
                        else
                        {
                            Logger.Information("Reordered {Count} stops for route {RouteId} OpId={OpId} Affected={Affected}", stops.Count, routeId, opId, affected);
                        }

                        EndOpOk("ReorderRouteStops", opId, sw, routeId, stops.Count);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        return Result.SuccessResult(changed);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error reordering stops for route {RouteId}", routeId); // basic existing log
                return Result.FailureResult<bool>($"Error reordering route stops: {ex.Message}");
            }
        }

        public async Task<Result<List<Bus>>> GetAvailableBusesAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var buses = await context.Buses
                        .Where(b => b.Status == "Active" || b.Status == "InService")
                        .OrderBy(b => b.BusNumber)
                        .ToListAsync();
                    return Result.SuccessResult(buses);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving available buses");
                return Result.FailureResult<List<Bus>>($"Error retrieving buses: {ex.Message}");
            }
        }

        public async Task<Result<List<Driver>>> GetAvailableDriversAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var drivers = await context.Drivers
                        .Where(d => d.Status == "Active")
                        .OrderBy(d => d.DriverName)
                        .ToListAsync();
                    return Result.SuccessResult(drivers);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving available drivers");
                return Result.FailureResult<List<Driver>>($"Error retrieving drivers: {ex.Message}");
            }
        }

        public async Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId)
        {
            var amResult = await AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.AM);
            if (amResult.IsSuccess)
            {
                return amResult;
            }

            return await AssignStudentToRouteAsync(studentId, routeId, RouteTimeSlot.PM);
        }

        public async Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId, RouteTimeSlot timeSlot)
        {
            return await AssignStudentToRouteAsync(studentId, routeId, timeSlot, overrideSeating: false)
                .ConfigureAwait(false);
        }

        public async Task<Result<bool>> AssignStudentToRouteAsync(
            int studentId,
            int routeId,
            RouteTimeSlot timeSlot,
            bool overrideSeating)
        {
            try
            {
                if (studentId <= 0 || routeId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid studentId or routeId");
                }

                if (timeSlot == RouteTimeSlot.Both)
                {
                    return Result.FailureResult<bool>("Specify AM or PM time slot for assignment");
                }

                if (_fitnessEvaluator is not null)
                {
                    var slotKind = timeSlot == RouteTimeSlot.AM
                        ? RouteTimeSlotKind.AM
                        : RouteTimeSlotKind.PM;
                    var fitness = await _fitnessEvaluator
                        .EvaluateAsync(studentId, routeId, slotKind, overrideSeating)
                        .ConfigureAwait(false);
                    if (!fitness.Allowed)
                    {
                        var detail = fitness.Reasons.Count > 0
                            ? string.Join("; ", fitness.Reasons)
                            : "Assignment blocked by fitness check";
                        return Result.FailureResult<bool>(detail);
                    }

                    if (fitness.Severity == AssignFitnessSeverity.Warn && fitness.Reasons.Count > 0)
                    {
                        Logger.Information(
                            "Assign proceeding with warnings Student={StudentId} Route={RouteId}: {Reasons}",
                            studentId, routeId, string.Join("; ", fitness.Reasons));
                    }
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
                    if (student is null)
                    {
                        return Result.FailureResult<bool>($"Student with ID {studentId} not found");
                    }

                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route is null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    if (StudentRouteAssignment.Matches(student, route, timeSlot))
                    {
                        return Result.SuccessResult(true);
                    }

                    if (!StudentRouteAssignment.IsUnassigned(student, timeSlot))
                    {
                        var current = timeSlot == RouteTimeSlot.AM ? student.AMRoute : student.PMRoute;
                        return Result.FailureResult<bool>($"Student already has a {timeSlot} route assigned: {current}");
                    }

                    // Fallback seating gate when evaluator not registered
                    if (_fitnessEvaluator is null)
                    {
                        var capacity = await GetCapacityForSlotAsync(context, route, timeSlot);
                        var assignedCount = await GetAssignedCountForSlotAsync(context, route, timeSlot);
                        if (capacity > 0 && assignedCount >= capacity && !overrideSeating)
                        {
                            return Result.FailureResult<bool>($"Route '{route.RouteName}' is at {timeSlot} capacity");
                        }
                    }

                    StudentRouteAssignment.SetSlot(student, timeSlot, route);

                    context.Entry(student).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    Logger.Information("Assigned student {StudentId} to route {RouteName} ({Slot})", studentId, route.RouteName, timeSlot);

                    if (_waypointRebuild is not null)
                    {
                        try
                        {
                            await _waypointRebuild.RebuildAndPersistAsync(routeId);
                        }
                        catch (Exception wpEx)
                        {
                            Logger.Warning(wpEx, "Waypoint rebuild after assign failed RouteId={RouteId}", routeId);
                        }
                    }

                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning student {StudentId} to route {RouteId} ({Slot})", studentId, routeId, timeSlot);
                return Result.FailureResult<bool>($"Error assigning student to route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId)
        {
            var amResult = await RemoveStudentFromRouteAsync(studentId, routeId, RouteTimeSlot.AM);
            if (amResult.IsSuccess)
            {
                return amResult;
            }

            return await RemoveStudentFromRouteAsync(studentId, routeId, RouteTimeSlot.PM);
        }

        public async Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId, RouteTimeSlot timeSlot)
        {
            try
            {
                if (studentId <= 0 || routeId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid studentId or routeId");
                }

                if (timeSlot == RouteTimeSlot.Both)
                {
                    return Result.FailureResult<bool>("Specify AM or PM time slot for removal");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
                    if (student is null)
                    {
                        return Result.FailureResult<bool>($"Student with ID {studentId} not found");
                    }

                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route is null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    if (!StudentRouteAssignment.Matches(student, route, timeSlot))
                    {
                        return Result.FailureResult<bool>($"Student is not assigned to the specified route for {timeSlot}");
                    }

                    StudentRouteAssignment.SetSlot(student, timeSlot, route: null);

                    context.Entry(student).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    Logger.Information("Removed student {StudentId} from route {RouteName} ({Slot})", studentId, route.RouteName, timeSlot);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error removing student {StudentId} from route {RouteId} ({Slot})", studentId, routeId, timeSlot);
                return Result.FailureResult<bool>($"Error removing student from route: {ex.Message}");
            }
        }

        public async Task<Result<RouteRiderException>> RecordRiderExceptionAsync(
            int routeId,
            int studentId,
            DateTime exceptionDate,
            string? reason = null)
        {
            try
            {
                if (routeId <= 0 || studentId <= 0)
                {
                    return Result.FailureResult<RouteRiderException>("Invalid routeId or studentId");
                }

                var day = DateTime.SpecifyKind(exceptionDate.Date, DateTimeKind.Utc);
                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                        if (route is null)
                        {
                            return Result.FailureResult<RouteRiderException>($"Route with ID {routeId} not found");
                        }

                        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
                        if (student is null)
                        {
                            return Result.FailureResult<RouteRiderException>($"Student with ID {studentId} not found");
                        }

                        var assigned = StudentRouteAssignment.Matches(student, route, RouteTimeSlot.AM)
                            || StudentRouteAssignment.Matches(student, route, RouteTimeSlot.PM);
                        if (!assigned)
                        {
                            return Result.FailureResult<RouteRiderException>(
                                "Student is not assigned to this route. Same-day not-riding does not unassign the year pairing.");
                        }

                        var existing = await context.RouteRiderExceptions.FirstOrDefaultAsync(e =>
                            e.RouteId == routeId
                            && e.StudentId == studentId
                            && e.ExceptionDate == day);
                        if (existing is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(reason))
                            {
                                existing.Reason = reason.Trim();
                                await context.SaveChangesAsync();
                            }

                            Logger.Information(
                                "Rider exception already recorded RouteId={RouteId} StudentId={StudentId} Date={Date}",
                                routeId,
                                studentId,
                                day);
                            return Result.SuccessResult(existing);
                        }

                        var row = new RouteRiderException
                        {
                            RouteId = routeId,
                            StudentId = studentId,
                            ExceptionDate = day,
                            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                            CreatedDate = DateTime.UtcNow
                        };
                        await context.RouteRiderExceptions.AddAsync(row);
                        await context.SaveChangesAsync();

                        Logger.Information(
                            "Recorded rider exception RouteId={RouteId} StudentId={StudentId} Date={Date} — published stops unchanged",
                            routeId,
                            studentId,
                            day);
                        return Result.SuccessResult(row);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(
                    Logger,
                    ex,
                    "Error recording rider exception RouteId={RouteId} StudentId={StudentId}",
                    routeId,
                    studentId);
                return Result.FailureResult<RouteRiderException>($"Error recording rider exception: {ex.Message}");
            }
        }

        public async Task<Result<List<Student>>> GetUnassignedStudentsAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var students = await context.Students
                        .Where(s => s.Active)
                        .Where(StudentRouteAssignment.UnassignedAm())
                        .Where(StudentRouteAssignment.UnassignedPm())
                        .OrderBy(s => s.StudentName)
                        .ToListAsync();
                    return Result.SuccessResult(students);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving unassigned students");
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}");
            }
        }

        public async Task<Result<List<Student>>> GetUnassignedStudentsAsync(RouteTimeSlot timeSlot)
        {
            try
            {
                if (timeSlot == RouteTimeSlot.Both)
                {
                    return await GetUnassignedStudentsAsync();
                }

                var (context, dispose) = GetReadContext();
                try
                {
                    var query = context.Students.Where(s => s.Active);
                    query = timeSlot == RouteTimeSlot.AM
                        ? query.Where(StudentRouteAssignment.UnassignedAm())
                        : query.Where(StudentRouteAssignment.UnassignedPm());
                    var students = await query
                            .OrderBy(s => s.StudentName)
                            .ThenBy(s => s.StudentId)
                            .ToListAsync();
                    return Result.SuccessResult(students);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving unassigned students for slot {Slot}", timeSlot);
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}");
            }
        }

        public async Task<Result<List<Student>>> GetStudentsForRouteAsync(int routeId, RouteTimeSlot timeSlot)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<List<Student>>("Invalid routeId");
                }

                if (timeSlot == RouteTimeSlot.Both)
                {
                    return Result.FailureResult<List<Student>>("Specify AM or PM time slot");
                }

                var (context, dispose) = GetReadContext();
                try
                {
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route is null || string.IsNullOrEmpty(route.RouteName))
                    {
                        return Result.SuccessResult(new List<Student>());
                    }

                    var students = await context.Students
                            .WhereOnSlot(routeId, route.RouteName, timeSlot)
                            .OrderBy(s => s.StudentName)
                            .ToListAsync();
                    return Result.SuccessResult(students);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students for route {RouteId} ({Slot})", routeId, timeSlot);
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}");
            }
        }

        public async Task<Result<List<Student>>> AutoAssignStudentsAsync(int routeId, RouteTimeSlot timeSlot)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<List<Student>>("Invalid routeId");
                }

                if (timeSlot == RouteTimeSlot.Both)
                {
                    return Result.FailureResult<List<Student>>("Specify AM or PM time slot for auto-assign");
                }

                var unassignedResult = await GetUnassignedStudentsAsync(timeSlot);
                if (!unassignedResult.IsSuccess || unassignedResult.Value is null)
                {
                    return Result.FailureResult<List<Student>>(unassignedResult.Error ?? "Failed to load unassigned students");
                }

                var assigned = new List<Student>();
                foreach (var student in unassignedResult.Value)
                {
                    var assignResult = await AssignStudentToRouteAsync(student.StudentId, routeId, timeSlot);
                    if (!assignResult.IsSuccess)
                    {
                        if (IsCapacityRejection(assignResult.Error))
                        {
                            break;
                        }

                        continue;
                    }

                    assigned.Add(student);
                }

                return Result.SuccessResult(assigned);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error auto-assigning students to route {RouteId} ({Slot})", routeId, timeSlot);
                return Result.FailureResult<List<Student>>($"Error auto-assigning students: {ex.Message}");
            }
        }

        public async Task<Result<List<Route>>> GetRoutesWithCapacityAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var routes = await context.Routes.Where(r => r.IsActive).ToListAsync();
                    var result = new List<Route>();
                    foreach (var route in routes)
                    {
                        var capacity = await GetRouteCapacityAsync(context, route);
                        if (capacity <= 0) capacity = 30; // default capacity
                        var assigned = await context.Students.WhereOnRoute(route).CountAsync();
                        if (assigned < capacity)
                        {
                            route.StudentCount = assigned;
                            result.Add(route);
                        }
                    }
                    return Result.SuccessResult(result);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving routes with capacity");
                return Result.FailureResult<List<Route>>($"Error retrieving routes: {ex.Message}");
            }
        }

        public async Task<Result<RouteUtilizationStats>> GetRouteUtilizationStatsAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var routes = await context.Routes.ToListAsync();
                    var totalRoutes = routes.Count;
                    var allStudents = await context.Students.ToListAsync();
                    var totalAssigned = allStudents.Count(StudentRouteAssignment.IsAssignedAny);
                    var totalUnassigned = allStudents.Count - totalAssigned;

                    int totalCapacity = 0;
                    double utilizationSum = 0;
                    int routesAtCapacity = 0;
                    int underutilized = 0;

                    foreach (var route in routes)
                    {
                        var capacity = await GetRouteCapacityAsync(context, route);
                        if (capacity <= 0) capacity = 30;
                        var assigned = allStudents.Count(s =>
                            StudentRouteAssignment.Matches(s, route, RouteTimeSlot.AM)
                            || StudentRouteAssignment.Matches(s, route, RouteTimeSlot.PM));
                        totalCapacity += capacity;
                        var utilization = capacity > 0 ? (double)assigned / capacity : 0.0;
                        utilizationSum += utilization;
                        if (assigned >= capacity) routesAtCapacity++;
                        if (utilization < 0.5) underutilized++;
                    }

                    var stats = new RouteUtilizationStats
                    {
                        TotalRoutes = totalRoutes,
                        TotalAssignedStudents = totalAssigned,
                        TotalUnassignedStudents = totalUnassigned,
                        TotalCapacity = totalCapacity,
                        AverageUtilizationRate = totalRoutes > 0 ? utilizationSum / totalRoutes : 0.0,
                        RoutesAtCapacity = routesAtCapacity,
                        UnderutilizedRoutes = underutilized,
                        TotalEstimatedDistance = routes.Sum(r => (double)(r.Distance ?? 0)),
                        TotalEstimatedTime = TimeSpan.FromMinutes(routes.Sum(r => (double)(r.EstimatedDuration ?? 0)))
                    };

                    return Result.SuccessResult(stats);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error calculating route utilization stats");
                return Result.FailureResult<RouteUtilizationStats>($"Error calculating route stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist updated arrival/departure timing for a set of route stops.
        /// </summary>
        public async Task<Result<bool>> UpdateRouteStopsTimingAsync(int routeId, IEnumerable<RouteStop> stops)
        {
            try
            {
                var (context, dispose) = GetWriteContext();
                try
                {
                    var stopIds = stops.Select(s => s.RouteStopId).ToList();
                    var dbStops = await context.RouteStops
                        .Where(rs => rs.RouteId == routeId && stopIds.Contains(rs.RouteStopId))
                        .ToListAsync();

                    foreach (var updated in stops)
                    {
                        var match = dbStops.FirstOrDefault(s => s.RouteStopId == updated.RouteStopId);
                        if (match != null)
                        {
                            match.ScheduledArrival = updated.ScheduledArrival;
                            match.ScheduledDeparture = updated.ScheduledDeparture;
                            match.EstimatedArrivalTime = updated.EstimatedArrivalTime;
                            match.EstimatedDepartureTime = updated.EstimatedDepartureTime;
                            match.UpdatedDate = DateTime.UtcNow;
                        }
                    }

                    await context.SaveChangesAsync();
                    Logger.Information("Updated timing for {Count} stops on RouteId={RouteId}", dbStops.Count, routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating route stop timing for RouteId={RouteId}", routeId);
                return Result.FailureResult<bool>($"Error updating stop timing: {ex.Message}");
            }
        }

        private static bool IsCapacityRejection(string? error) =>
            !string.IsNullOrEmpty(error)
            && error.Contains("capacity", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Fills <see cref="Route.StudentCount"/> and <see cref="Route.StopCount"/> from identity keys
        /// (name fallback only when the key is null). One query each — not an N+1 per route.
        /// </summary>
        private static async Task ApplyListMetricsAsync(BusBuddyDbContext context, List<Route> routes)
        {
            if (routes.Count == 0)
            {
                return;
            }

            var routeIds = routes.Select(r => r.RouteId).ToList();
            var stopCounts = await context.RouteStops
                .AsNoTracking()
                .Where(s => routeIds.Contains(s.RouteId))
                .GroupBy(s => s.RouteId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var roster = await context.Students
                .AsNoTracking()
                .Select(s => new { s.AmRouteId, s.PmRouteId, s.AMRoute, s.PMRoute })
                .ToListAsync();

            foreach (var route in routes)
            {
                route.StopCount = stopCounts.GetValueOrDefault(route.RouteId);
                var uniqueName = routes.Count(r =>
                    string.Equals(r.RouteName, route.RouteName, StringComparison.OrdinalIgnoreCase)) == 1;
                route.StudentCount = roster.Count(s =>
                    s.AmRouteId == route.RouteId
                    || s.PmRouteId == route.RouteId
                    || (uniqueName && s.AmRouteId == null && NamesEqual(s.AMRoute, route.RouteName))
                    || (uniqueName && s.PmRouteId == null && NamesEqual(s.PMRoute, route.RouteName)));
            }
        }

        private static bool NamesEqual(string? left, string? right) =>
            !string.IsNullOrWhiteSpace(left)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static async Task<int> GetAssignedCountForSlotAsync(BusBuddyDbContext context, Route route, RouteTimeSlot timeSlot)
        {
            return await context.Students
                .WhereOnSlot(route.RouteId, route.RouteName, timeSlot)
                .CountAsync();
        }

        private static async Task<int> GetCapacityForSlotAsync(BusBuddyDbContext context, Route route, RouteTimeSlot timeSlot)
        {
            if (timeSlot == RouteTimeSlot.AM && route.AMVehicleId.HasValue)
            {
                var am = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.AMVehicleId.Value);
                if (am != null && am.SeatingCapacity > 0)
                {
                    return am.SeatingCapacity;
                }
            }

            if (timeSlot == RouteTimeSlot.PM && route.PMVehicleId.HasValue)
            {
                var pm = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.PMVehicleId.Value);
                if (pm != null && pm.SeatingCapacity > 0)
                {
                    return pm.SeatingCapacity;
                }
            }

            return 30;
        }

        // Helper to compute route capacity from assigned buses
        private static async Task<int> GetRouteCapacityAsync(BusBuddyDbContext context, Route route)
        {
            var amCap = 0;
            var pmCap = 0;
            if (route.AMVehicleId.HasValue)
            {
                var am = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.AMVehicleId.Value);
                if (am != null) amCap = am.SeatingCapacity;
            }
            if (route.PMVehicleId.HasValue)
            {
                var pm = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.PMVehicleId.Value);
                if (pm != null) pmCap = pm.SeatingCapacity;
            }
            return Math.Max(amCap, pmCap);
        }

        public async Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, RouteTimeSlot timeSlot)
        {
            try
            {
                var (opId, sw) = StartOp("AssignVehicle", routeId);
                if (routeId <= 0 || vehicleId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or vehicleId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        Logger.Error("AssignVehicle failed — route {RouteId} not found", routeId);
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    var bus = await context.Buses.FindAsync(vehicleId);
                    if (bus == null)
                    {
                        Logger.Error("AssignVehicle failed — vehicle {VehicleId} not found", vehicleId);
                        return Result.FailureResult<bool>($"Vehicle with ID {vehicleId} not found");
                    }

                    // Availability check (Active / In Service)
                    if (!RouteVehicleLinker.IsAssignableStatus(bus.Status))
                    {
                        Logger.Error("AssignVehicle failed — vehicle {VehicleId} not available (Status: {Status})", vehicleId, bus.Status);
                        return Result.FailureResult<bool>($"Vehicle {bus.BusNumber} is not available (status: {bus.Status})");
                    }

                    RouteVehicleLinker.Apply(route, bus, timeSlot);

                    // Single SaveChanges is atomic; do not wrap in BeginTransactionAsync —
                    // NpgsqlRetryingExecutionStrategy rejects user-initiated transactions.
                    await context.SaveChangesAsync();

                    Logger.Information("Assigned vehicle {VehicleId} to route {RouteId} for {TimeSlot} OpId={OpId}", vehicleId, routeId, timeSlot, opId);
                    EndOpOk("AssignVehicle", opId, sw, routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning vehicle {VehicleId} to route {RouteId}", vehicleId, routeId);
                return Result.FailureResult<bool>($"Error assigning vehicle to route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> AssignDriverToRouteAsync(int routeId, int driverId, RouteTimeSlot timeSlot)
        {
            try
            {
                var (opId, sw) = StartOp("AssignDriver", routeId);
                if (routeId <= 0 || driverId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or driverId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    // Basic verification driver exists
                    var driver = await context.Drivers.FindAsync(driverId);
                    if (driver == null)
                    {
                        return Result.FailureResult<bool>($"Driver with ID {driverId} not found");
                    }

                    switch (timeSlot)
                    {
                        case RouteTimeSlot.AM:
                            route.AMDriverId = driverId;
                            break;
                        case RouteTimeSlot.PM:
                            route.PMDriverId = driverId;
                            break;
                        case RouteTimeSlot.Both:
                            route.AMDriverId = driverId;
                            route.PMDriverId = driverId;
                            break;
                        default:
                            return Result.FailureResult<bool>("Unsupported time slot");
                    }

                    await context.SaveChangesAsync();
                    Logger.Information("Assigned driver {DriverId} to route {RouteId} for {TimeSlot} OpId={OpId}", driverId, routeId, timeSlot, opId);
                    EndOpOk("AssignDriver", opId, sw, routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning driver {DriverId} to route {RouteId}", driverId, routeId);
                return Result.FailureResult<bool>($"Error assigning driver to route: {ex.Message}");
            }
        }

        public async Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop routeStop)
        {
            try
            {
                var (opId, sw) = StartOp("AddStop", routeId);
                if (routeStop is null)
                {
                    return Result.FailureResult<RouteStop>("RouteStop cannot be null");
                }

                if (routeId <= 0)
                {
                    return Result.FailureResult<RouteStop>("Invalid routeId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        // Ensure route exists
                        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                        if (route == null)
                        {
                            return Result.FailureResult<RouteStop>($"Route with ID {routeId} not found");
                        }

                        if (!routeStop.HasValidatedCoordinates)
                        {
                            return Result.FailureResult<RouteStop>(
                                "Stop requires a validated location (geocoded lat/lng). Unvalidated coordinates cannot be published waypoints.");
                        }

                        // Normalize and prepare the RouteStop entity
                        routeStop.RouteId = routeId; // enforce association
                        if (routeStop.StopOrder <= 0)
                        {
                            // Determine next StopOrder
                            var maxOrder = await context.RouteStops
                                .Where(rs => rs.RouteId == routeId)
                                .Select(rs => (int?)rs.StopOrder)
                                .MaxAsync() ?? 0;
                            routeStop.StopOrder = maxOrder + 1;
                        }

                        // CreatedDate is required by the model — set explicitly
                        if (routeStop.CreatedDate == default)
                        {
                            routeStop.CreatedDate = DateTime.UtcNow;
                        }

                        NormalizeStopEstimates(routeStop);

                        // Add and save changes asynchronously (EF Core best practice)
                        await context.RouteStops.AddAsync(routeStop);
                        await context.SaveChangesAsync();

                        Logger.Information("Added stop {StopName} (ID: {RouteStopId}) to route {RouteId} OpId={OpId}",
                            routeStop.StopName, routeStop.RouteStopId, routeId, opId);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        EndOpOk("AddStop", opId, sw, routeId);

                        return Result.SuccessResult(routeStop);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error adding stop to route {RouteId}", routeId);
                var detail = ex.GetBaseException().Message;
                return Result.FailureResult<RouteStop>($"Error adding stop to route: {detail}");
            }
        }

        public async Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId)
        {
            try
            {
                var (opId, sw) = StartOp("RemoveStop", routeId);
                if (routeId <= 0 || stopId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or stopId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        // Ensure route exists
                        var route = await context.Routes.FindAsync(routeId);
                        if (route == null)
                        {
                            Logger.Error("RemoveStop failed — route {RouteId} not found", routeId);
                            return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                        }

                        // Find the stop scoped to this route
                        var stop = await context.RouteStops
                            .FirstOrDefaultAsync(rs => rs.RouteStopId == stopId && rs.RouteId == routeId);

                        if (stop == null)
                        {
                            Logger.Error("RemoveStop failed — stop {StopId} not found for route {RouteId}", stopId, routeId);
                            return Result.FailureResult<bool>($"Stop with ID {stopId} not found for route {routeId}");
                        }

                        context.RouteStops.Remove(stop);

                        // Persist changes asynchronously
                        await context.SaveChangesAsync();

                        Logger.Information("Removed stop {StopId} from route {RouteId} OpId={OpId}", stopId, routeId, opId);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        EndOpOk("RemoveStop", opId, sw, routeId);
                        return Result.SuccessResult(true);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error removing stop {StopId} from route {RouteId}", stopId, routeId);
                return Result.FailureResult<bool>($"Error removing stop from route: {ex.Message}");
            }
        }

        // ReorderRouteStopsAsync implemented earlier (single implementation retained)

        public async Task<Result<Route>> CloneRouteAsync(int sourceRouteId, DateTime newDate, string? newRouteName = null)
        {
            try
            {
                var (opId, sw) = StartOp("CloneRoute", sourceRouteId);
                if (sourceRouteId <= 0)
                {
                    return Result.FailureResult<Route>("Invalid source route id");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var source = await context.Routes.AsNoTracking()
                            .FirstOrDefaultAsync(r => r.RouteId == sourceRouteId);
                        if (source is null)
                        {
                            return Result.FailureResult<Route>($"Route with ID {sourceRouteId} not found");
                        }

                        var stops = await context.RouteStops.AsNoTracking()
                            .Where(s => s.RouteId == sourceRouteId)
                            .OrderBy(s => s.StopOrder)
                            .ToListAsync();

                        var clone = new Route
                        {
                            Date = newDate == default
                                ? DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc)
                                : newDate.Date,
                            RouteName = string.IsNullOrWhiteSpace(newRouteName)
                                ? $"Copy of {source.RouteName}"
                                : newRouteName.Trim(),
                            Description = source.Description,
                            IsActive = false,
                            School = source.School,
                            Session = source.Session,
                            IsSpecialNeedsRoute = source.IsSpecialNeedsRoute,
                            RouteDescription = source.RouteDescription,
                            Boundaries = source.Boundaries,
                            Path = source.Path,
                            WaypointsJson = source.WaypointsJson,
                            Distance = source.Distance,
                            EstimatedDuration = source.EstimatedDuration,
                            StopCount = stops.Count,
                            StudentCount = 0
                        };

                        await context.Routes.AddAsync(clone);
                        await context.SaveChangesAsync();

                        foreach (var stop in stops)
                        {
                            await context.RouteStops.AddAsync(new RouteStop
                            {
                                RouteId = clone.RouteId,
                                StopName = stop.StopName,
                                StopAddress = stop.StopAddress,
                                Latitude = stop.Latitude,
                                Longitude = stop.Longitude,
                                StopOrder = stop.StopOrder,
                                ScheduledArrival = stop.ScheduledArrival,
                                ScheduledDeparture = stop.ScheduledDeparture,
                                StopDuration = stop.StopDuration,
                                Status = stop.Status,
                                Notes = stop.Notes,
                                CreatedDate = DateTime.UtcNow,
                                EstimatedArrivalTime = stop.EstimatedArrivalTime,
                                EstimatedDepartureTime = stop.EstimatedDepartureTime
                            });
                        }

                        if (stops.Count > 0)
                        {
                            await context.SaveChangesAsync();
                        }

                        Logger.Information(
                            "Cloned route {SourceId} to {CloneId} ({CloneName}) with {StopCount} stops OpId={OpId}",
                            sourceRouteId, clone.RouteId, clone.RouteName, stops.Count, opId);
                        EndOpOk("CloneRoute", opId, sw, clone.RouteId);
                        return Result.SuccessResult(clone);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error cloning route {RouteId}", sourceRouteId);
                return Result.FailureResult<Route>($"Error cloning route: {ex.Message}");
            }
        }

        #endregion

        #region Published path refresh

        /// <summary>
        /// Fills the non-nullable estimate columns so an unset stop does not persist <see cref="DateTime.MinValue"/>.
        /// These are published wall-clock face times at the stop (07:00 means 7am there), not UTC instants.
        /// </summary>
        private static void NormalizeStopEstimates(RouteStop routeStop)
        {
            var arrival = routeStop.ScheduledArrival == default
                ? DefaultStopArrival
                : routeStop.ScheduledArrival;
            var departure = routeStop.ScheduledDeparture == default
                ? arrival + PickupScheduleCalculator.DefaultDwell
                : routeStop.ScheduledDeparture;

            if (routeStop.EstimatedArrivalTime == default)
            {
                routeStop.EstimatedArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.Add(arrival), DateTimeKind.Utc);
            }

            if (routeStop.EstimatedDepartureTime == default)
            {
                routeStop.EstimatedDepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.Add(departure), DateTimeKind.Utc);
            }
        }

        private static readonly TimeSpan DefaultStopArrival = TimeSpan.FromHours(7);

        /// <summary>
        /// Rebuilds <see cref="Route.WaypointsJson"/> from ordered validated stops, then refreshes
        /// the Google drive path. Fail-open when routing is unavailable.
        /// </summary>
        private async Task RefreshPublishedPathAsync(BusBuddyDbContext context, int routeId)
        {
            var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId).ConfigureAwait(false);
            if (route is null)
            {
                return;
            }

            var coords = await context.RouteStops
                .Where(s => s.RouteId == routeId)
                .OrderBy(s => s.StopOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            var validated = coords
                .Where(s => RouteStop.IsValidatedCoordinate(s.Latitude, s.Longitude))
                .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
                .ToList();
            route.WaypointsJson = RouteWaypointSerializer.FromPairs(validated);
            if (validated.Count == 0)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
                return;
            }
            if (validated.Count >= 2 && _routingService is not null)
            {
                var refresh = await RouteDrivePathRefresher
                    .TryRefreshAsync(_routingService, route)
                    .ConfigureAwait(false);
                if (!refresh.Success && !refresh.Skipped)
                {
                    Logger.Warning(
                        "Drive path refresh after stop change skipped RouteId={RouteId}: {Message}",
                        routeId,
                        refresh.Message);
                }
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        #endregion

    }

}
