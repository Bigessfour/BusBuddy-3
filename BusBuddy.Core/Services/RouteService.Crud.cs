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
    /// <summary>Route CRUD, create-new, and activate/deactivate.</summary>
    public partial class RouteService
    {
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
                        var message = RouteDeleteClerkMessages.BuildRetiredMessage(
                            routeName,
                            scheduleCount,
                            studentFkCount,
                            tripCount);
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
    }
}
