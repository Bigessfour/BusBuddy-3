using BusBuddy.Core.Domain;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; // Added for Stopwatch timing (basic instrumentation)

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Route Service implementation with comprehensive route management capabilities
    /// Implements Result pattern for robust error handling and logging
    /// Production-focused implementation prioritizing core route building functionality
    /// Updated: Uses IBusBuddyDbContextFactory for consistent dependency injection
    /// </summary>
    public partial class RouteService : IRouteService
    {
        private static readonly ILogger Logger = Log.ForContext<RouteService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;

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
        private static void EndOpFail(string name, Guid opId, Stopwatch sw, Exception ex, object? routeId = null)
        {
            sw.Stop();
            Logger.Error(ex, "FAIL  {Op} OpId={OpId} RouteId={RouteId} ElapsedMs={Ms}", name, opId, routeId, sw.ElapsedMilliseconds);
        }

        public RouteService(IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
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
                        .AsNoTracking() // Use AsNoTracking for better performance in read operations
                        .OrderBy(r => r.RouteName)
                        .ToListAsync();

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
                Logger.Error(ex, "Error retrieving active routes");
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
                        .AsNoTracking() // Use AsNoTracking for better performance in read operations
                        .OrderBy(r => r.RouteName)
                        .ToListAsync();

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
                Logger.Error(ex, "Error retrieving all routes");
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
                    var route = await context.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.RouteId == id);
                    if (route == null)
                    {
                        return Result.FailureResult<Route>($"Route with ID {id} not found");
                    }

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
                Logger.Error(ex, "Error retrieving route {RouteId}", id);
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
                Logger.Error(ex, "Error creating route {RouteName}", route.RouteName);
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
                    context.Entry(route).State = EntityState.Modified;
                    await context.SaveChangesAsync();

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
                Logger.Error(ex, "Error updating route {RouteId}", route.RouteId);
                return Result.FailureResult<Route>($"Error updating route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> DeleteRouteAsync(int id)
        {
            try
            {
                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FindAsync(id);
                    if (route == null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {id} not found");
                    }

                    context.Routes.Remove(route);
                    await context.SaveChangesAsync();

                    Logger.Information("Successfully deleted route {RouteId}", id);
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
                Logger.Error(ex, "Error deleting route {RouteId}", id);
                return Result.FailureResult<bool>($"Error deleting route: {ex.Message}");
            }
        }

        public async Task<Result<IEnumerable<Route>>> SearchRoutesAsync(string searchTerm)
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var routes = await context.Routes
                        .Where(r => r.RouteName.Contains(searchTerm) ||
                                   (r.Description != null && r.Description.Contains(searchTerm)))
                        .AsNoTracking() // Use AsNoTracking for better performance in read operations
                        .OrderBy(r => r.RouteName)
                        .ToListAsync();

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
                Logger.Error(ex, "Error searching routes with term: {SearchTerm}", searchTerm);
                return Result.FailureResult<IEnumerable<Route>>($"Error searching routes: {ex.Message}");
            }
        }

        public Task<Result<IEnumerable<Route>>> GetRoutesByBusIdAsync(int busId)
        {
            try
            {
                // Implementation would depend on how bus assignments are stored
                // For now, return empty result as placeholder
                var routes = new List<Route>();
                return Task.FromResult(Result.SuccessResult(routes.AsEnumerable()));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting routes for bus {BusId}", busId);
                return Task.FromResult(Result.FailureResult<IEnumerable<Route>>($"Error getting routes for bus: {ex.Message}"));
            }
        }

        public async Task<Result<bool>> IsRouteNumberUniqueAsync(string routeNumber, int? excludeId = null)
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var query = context.Routes.Where(r => r.RouteName == routeNumber);

                    if (excludeId.HasValue)
                    {
                        query = query.Where(r => r.RouteId != excludeId.Value);
                    }

                    var exists = await query.AnyAsync();
                    return Result.SuccessResult(!exists);
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
                Logger.Error(ex, "Error checking route number uniqueness: {RouteNumber}", routeNumber);
                return Result.FailureResult<bool>($"Error checking route uniqueness: {ex.Message}");
            }
        }

        #endregion

        #region Route Building Methods (Production Priority)

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

                if (routeDate < DateTime.Today)
                {
                    return Result.FailureResult<Route>("Route date cannot be in the past");
                }

                // Check for duplicate route name on the same date
                var (context, dispose) = GetReadContext();
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
                Logger.Error(ex, "Error creating new route {RouteName}", routeName);
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

                    if (route.Date < DateTime.Today)
                    {
                        validationResult.Issues.Add("Route date cannot be in the past");
                    }

                    // TODO: Add more comprehensive validation (bus, driver, stops, students)
                    // For production, comprehensive validation is recommended

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
                Logger.Error(ex, "Error validating route {RouteId}", routeId);
                return Result.FailureResult<RouteValidationResult>($"Error validating route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ActivateRouteAsync(int routeId)
        {
            try
            {
                Logger.Information("Activating route {RouteId}", routeId);
                // Production optimization: validation handled at higher level

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route == null)
                    {
                        Logger.Warning("ActivateRoute — route {RouteId} not found", routeId);
                        return Result.FailureResult<bool>($"Route {routeId} not found");
                    }
                    // Always set IsActive = true to ensure state persisted even in odd tracking scenarios (tests rely on FindAsync)
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
                Logger.Error(ex, "Error activating route {RouteId}", routeId);
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
                Logger.Error(ex, "Error deactivating route {RouteId}", routeId);
                return Result.FailureResult<bool>($"Error deactivating route: {ex.Message}");
            }
        }

        #endregion

        #region Placeholder Methods (To Be Implemented)

        // These methods return placeholder implementations to satisfy the interface
        // TODO: Implement these methods as needed for full functionality

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
                Logger.Error(ex, "Error retrieving stops for route {RouteId}", routeId);
                return Result.FailureResult<IEnumerable<RouteStop>>($"Error retrieving route stops: {ex.Message}");
            }
        }

        public Task<Result<RouteStop>> AddRouteStopAsync(RouteStop routeStop)
        {
            return Task.FromResult(Result.FailureResult<RouteStop>("Not implemented yet"));
        }

        public Task<Result<RouteStop>> UpdateRouteStopAsync(RouteStop routeStop)
        {
            return Task.FromResult(Result.FailureResult<RouteStop>("Not implemented yet"));
        }

        public Task<Result<bool>> DeleteRouteStopAsync(int routeStopId)
        {
            return Task.FromResult(Result.FailureResult<bool>("Not implemented yet"));
        }

        public Task<Result<decimal>> GetRouteTotalDistanceAsync(int routeId)
        {
            return Task.FromResult(Result.SuccessResult(0m));
        }

        public Task<Result<TimeSpan>> GetRouteEstimatedTimeAsync(int routeId)
        {
            return Task.FromResult(Result.SuccessResult(TimeSpan.Zero));
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
                            s.StopOrder = order; // apply only if changed
                            s.UpdatedDate = DateTime.UtcNow;
                        }
                        order++;
                    }

                    // Additional defensive: mark StopOrder property modified explicitly (helps some providers/tests)
                    foreach (var s in stops)
                    {
                        context.Entry(s).Property(x => x.StopOrder).IsModified = true;
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
                    return Result.SuccessResult(changed);
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
                Logger.Error(ex, "Error reordering stops for route {RouteId}", routeId); // basic existing log
                return Result.FailureResult<bool>($"Error reordering route stops: {ex.Message}");
            }
        }

        public async Task<Result<List<Bus>>> GetAvailableBusesAsync()
        {
            var op = StartOp("GetAvailableBuses");
            try
            {
                Logger.Information("Retrieving available buses for route assignment");

                var (context, dispose) = GetReadContext();
                try
                {
                    var buses = await context.Buses
                        .Where(b => b.Status == "Active" &&
                                   (b.NextMaintenanceDue == null || b.NextMaintenanceDue > DateTime.Now))
                        .OrderBy(b => b.BusNumber)
                        .ToListAsync();

                    Logger.Information("Found {Count} available buses (active status, no pending maintenance)",
                        buses.Count);

                    // Log bus details for pipeline tracing
                    foreach (var bus in buses.Take(5)) // Log first 5 for brevity
                    {
                        Logger.Debug("Available bus: {BusNumber} (ID: {BusId}) - Capacity: {Capacity}, Status: {Status}",
                            bus.BusNumber, bus.BusId, bus.SeatingCapacity, bus.Status);
                    }

                    if (buses.Count > 5)
                    {
                        Logger.Debug("... and {RemainingCount} more buses available", buses.Count - 5);
                    }

                    EndOpOk("GetAvailableBuses", op.OpId, op.Sw, count: buses.Count);
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
                EndOpFail("GetAvailableBuses", op.OpId, op.Sw, ex);
                Logger.Error(ex, "Error retrieving available buses");
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
                Logger.Error(ex, "Error retrieving available drivers");
                return Result.FailureResult<List<Driver>>($"Error retrieving drivers: {ex.Message}");
            }
        }

        public async Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId)
        {
            try
            {
                if (studentId <= 0 || routeId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid studentId or routeId");
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

                    // Capacity check
                    var capacity = await GetRouteCapacityAsync(context, route);
                    var assignedCount = await context.Students.CountAsync(s => s.AMRoute == route.RouteName || s.PMRoute == route.RouteName);
                    if (capacity > 0 && assignedCount >= capacity)
                    {
                        return Result.FailureResult<bool>($"Route '{route.RouteName}' is at capacity");
                    }

                    // Assign to AM if free; else PM if free; else fail
                    if (string.IsNullOrWhiteSpace(student.AMRoute))
                    {
                        student.AMRoute = route.RouteName;
                    }
                    else if (string.IsNullOrWhiteSpace(student.PMRoute))
                    {
                        student.PMRoute = route.RouteName;
                    }
                    else
                    {
                        return Result.FailureResult<bool>("Student already has both AM and PM routes assigned");
                    }

                    context.Entry(student).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    Logger.Information("Assigned student {StudentId} to route {RouteName}", studentId, route.RouteName);
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
                Logger.Error(ex, "Error assigning student {StudentId} to route {RouteId}", studentId, routeId);
                return Result.FailureResult<bool>($"Error assigning student to route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId)
        {
            try
            {
                if (studentId <= 0 || routeId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid studentId or routeId");
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

                    var changed = false;
                    if (string.Equals(student.AMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase))
                    {
                        student.AMRoute = null;
                        changed = true;
                    }
                    if (string.Equals(student.PMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase))
                    {
                        student.PMRoute = null;
                        changed = true;
                    }

                    if (!changed)
                    {
                        return Result.FailureResult<bool>("Student is not assigned to the specified route");
                    }

                    context.Entry(student).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    Logger.Information("Removed student {StudentId} from route {RouteName}", studentId, route.RouteName);
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
                Logger.Error(ex, "Error removing student {StudentId} from route {RouteId}", studentId, routeId);
                return Result.FailureResult<bool>($"Error removing student from route: {ex.Message}");
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
                        .Where(s => (s.AMRoute == null || s.AMRoute == "") && (s.PMRoute == null || s.PMRoute == "") && s.Active)
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
                Logger.Error(ex, "Error retrieving unassigned students");
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}");
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
                        if (capacity <= 0) capacity = 30; // default production capacity
                        var assigned = await context.Students.CountAsync(s => s.AMRoute == route.RouteName || s.PMRoute == route.RouteName);
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
                Logger.Error(ex, "Error retrieving routes with capacity");
                return Result.FailureResult<List<Route>>($"Error retrieving routes: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ValidateRouteCapacityAsync(int routeId)
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route is null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    var capacity = await GetRouteCapacityAsync(context, route);
                    if (capacity <= 0) capacity = 30;
                    var assigned = await context.Students.CountAsync(s => s.AMRoute == route.RouteName || s.PMRoute == route.RouteName);
                    return Result.SuccessResult(assigned <= capacity);
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
                Logger.Error(ex, "Error validating capacity for route {RouteId}", routeId);
                return Result.FailureResult<bool>($"Error validating route capacity: {ex.Message}");
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
                    var totalAssigned = allStudents.Count(s => !string.IsNullOrWhiteSpace(s.AMRoute) || !string.IsNullOrWhiteSpace(s.PMRoute));
                    var totalUnassigned = allStudents.Count - totalAssigned;

                    int totalCapacity = 0;
                    double utilizationSum = 0;
                    int routesAtCapacity = 0;
                    int underutilized = 0;

                    foreach (var route in routes)
                    {
                        var capacity = await GetRouteCapacityAsync(context, route);
                        if (capacity <= 0) capacity = 30;
                        var assigned = allStudents.Count(s => string.Equals(s.AMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase) ||
                                                              string.Equals(s.PMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase));
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
                Logger.Error(ex, "Error calculating route utilization stats");
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
                Logger.Error(ex, "Error updating route stop timing for RouteId={RouteId}", routeId);
                return Result.FailureResult<bool>($"Error updating stop timing: {ex.Message}");
            }
        }

        public async Task<Result<bool>> CanAssignStudentToRouteAsync(int studentId, int routeId)
        {
            try
            {
                var (context, dispose) = GetReadContext();
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

                    if (!string.IsNullOrWhiteSpace(student.AMRoute) && !string.IsNullOrWhiteSpace(student.PMRoute))
                    {
                        return Result.FailureResult<bool>("Student already has AM and PM routes");
                    }

                    var capacity = await GetRouteCapacityAsync(context, route);
                    if (capacity <= 0) capacity = 30;
                    var assigned = await context.Students.CountAsync(s => s.AMRoute == route.RouteName || s.PMRoute == route.RouteName);
                    if (assigned >= capacity)
                    {
                        return Result.FailureResult<bool>($"Route '{route.RouteName}' is at capacity");
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
                Logger.Error(ex, "Error validating assignment of student {StudentId} to route {RouteId}", studentId, routeId);
                return Result.FailureResult<bool>($"Error validating assignment: {ex.Message}");
            }
        }

        // Helper to compute route capacity from assigned buses
        private static async Task<int> GetRouteCapacityAsync(BusBuddyDbContext context, Route route)
        {
            var amCap = 0;
            var pmCap = 0;
            if (route.AMBusId.HasValue)
            {
                var am = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.AMBusId.Value);
                if (am != null) amCap = am.SeatingCapacity;
            }
            if (route.PMBusId.HasValue)
            {
                var pm = await context.Buses.FirstOrDefaultAsync(b => b.BusId == route.PMBusId.Value);
                if (pm != null) pmCap = pm.SeatingCapacity;
            }
            return Math.Max(amCap, pmCap);
        }

        public async Task<Result<bool>> AssignBusToRouteAsync(int routeId, int busId, RouteTimeSlot timeSlot)
        {
            try
            {
                var (opId, sw) = StartOp("AssignBus", routeId);
                if (routeId <= 0 || busId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or busId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    await using var transaction = await context.Database.BeginTransactionAsync();

                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        Logger.Error("AssignBus failed — route {RouteId} not found", routeId);
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    var bus = await context.Buses.FindAsync(busId);
                    if (bus == null)
                    {
                        Logger.Error("AssignBus failed — bus {BusId} not found", busId);
                        return Result.FailureResult<bool>($"Bus with ID {busId} not found");
                    }

                    // Availability check (Active status)
                    if (!string.Equals(bus.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Error("AssignBus failed — bus {BusId} not available (Status: {Status})", busId, bus.Status);
                        return Result.FailureResult<bool>($"Bus {busId} is not available");
                    }

                    // Apply assignment based on time slot
                    switch (timeSlot)
                    {
                        case RouteTimeSlot.AM:
                            route.AMBusId = busId;
                            break;
                        case RouteTimeSlot.PM:
                            route.PMBusId = busId;
                            break;
                        case RouteTimeSlot.Both:
                            route.AMBusId = busId;
                            route.PMBusId = busId;
                            break;
                        default:
                            Logger.Error("AssignBus failed — unsupported time slot {TimeSlot}", timeSlot);
                            return Result.FailureResult<bool>("Unsupported time slot");
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Logger.Information("Assigned bus {BusId} to route {RouteId} for {TimeSlot} OpId={OpId}", busId, routeId, timeSlot, opId);
                    EndOpOk("AssignBus", opId, sw, routeId);
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
                Logger.Error(ex, "Error assigning bus {BusId} to route {RouteId}", busId, routeId);
                return Result.FailureResult<bool>($"Error assigning bus to route: {ex.Message}");
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
                Logger.Error(ex, "Error assigning driver {DriverId} to route {RouteId}", driverId, routeId);
                return Result.FailureResult<bool>($"Error assigning driver to route: {ex.Message}");
            }
        }

        public Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop routeStop)
        {
            return Task.FromResult(Result.FailureResult<RouteStop>("Not implemented yet"));
        }

        public Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId)
        {
            return Task.FromResult(Result.FailureResult<bool>("Not implemented yet"));
        }

        // ReorderRouteStopsAsync implemented earlier (single implementation retained)

        public Task<Result<Route>> CloneRouteAsync(int sourceRouteId, DateTime newDate, string? newRouteName = null)
        {
            return Task.FromResult(Result.FailureResult<Route>("Not implemented yet"));
        }

        #endregion

        #region Wiley Schedule Generation

        /// <summary>
        /// Generates route schedules for Wiley routes, calculates times, and outputs to RouteSchedules/.
        /// Integrates with IStudentService for dynamic assignments. Error handling per Error-Handling.md.
        /// </summary>
        public async Task GenerateWileySchedulesAsync(BusBuddyDbContext context, IStudentService studentService)
        {
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "RouteSchedules");
            Directory.CreateDirectory(outputDir);

            var routes = await context.Routes.Where(r => r.IsActive).ToListAsync();
            foreach (var route in routes)
            {
                try
                {
                    var students = await studentService.GetStudentsForRouteAsync(context, route.RouteId);
                    var schedule = BuildRouteSchedule(route, students);
                    var fileName = $"Route-{route.RouteName.Replace(" ","")}-Schedule.txt";
                    var filePath = Path.Combine(outputDir, fileName);
                    await File.WriteAllTextAsync(filePath, schedule);
                    Logger.Information("Generated schedule for {RouteName} at {FilePath}", route.RouteName, filePath);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error generating schedule for route {RouteName}", route.RouteName);
                    // Per Error-Handling.md: log, continue, and optionally notify
                }
            }
        }

        private string BuildRouteSchedule(Route route, List<Student> students)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Route: {route.RouteName}");
            sb.AppendLine($"Description: {route.RouteDescription}");
            sb.AppendLine($"Path: {route.Path}");
            sb.AppendLine($"Date: {DateTime.Today:yyyy-MM-dd}");
            sb.AppendLine($"Speed: 40-50 mph (rural)");
            sb.AppendLine($"Stop Time: 5-10 min per stop");
            sb.AppendLine();
            sb.AppendLine("Student Assignments:");
            foreach (var student in students)
            {
                sb.AppendLine($"- {student.StudentName} (Grade {student.Grade}) - Stop: {student.BusStop}");
            }
            sb.AppendLine();
            sb.AppendLine("Estimated Times:");
            // Example times based on route
            int miles = route.RouteName.Contains("Truck Plaza") ? 35 : route.RouteName.Contains("Big Bend") ? 30 : 35;
            int baseMinutes = route.RouteName.Contains("Truck Plaza") ? 50 : 45;
            int stopMinutes = students.Count * 7; // avg 7 min per stop
            int totalMinutes = baseMinutes + stopMinutes;
            sb.AppendLine($"Total Distance: {miles} miles");
            sb.AppendLine($"Base Drive Time: {baseMinutes} min");
            sb.AppendLine($"Stop Time: {stopMinutes} min");
            sb.AppendLine($"Estimated Total Time: {totalMinutes} min");
            return sb.ToString();
        }

        /// <summary>
        /// Assigns a vehicle to a route for the specified time slot
        /// </summary>
        /// <param name="routeId">The ID of the route</param>
        /// <param name="vehicleId">The ID of the vehicle (bus) to assign</param>
        /// <param name="timeSlot">The time slot for the assignment</param>
        /// <returns>Result indicating success or failure</returns>
        public async Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, BusBuddy.Core.Domain.RouteTimeSlot timeSlot)
        {
            // Delegate to the existing AssignBusToRouteAsync method
            return await AssignBusToRouteAsync(routeId, vehicleId, timeSlot);
        }

        #endregion

    }

}








