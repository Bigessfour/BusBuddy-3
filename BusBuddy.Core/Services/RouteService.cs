using BusBuddy.Core.Models;
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

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Route Service implementation with comprehensive route management capabilities
    /// Implements Result pattern for robust error handling and logging
    /// MVP-focused implementation prioritizing core route building functionality
    /// Updated: Uses IBusBuddyDbContextFactory for consistent dependency injection
    /// </summary>
    public partial class RouteService : IRouteService
    {
        private static readonly ILogger Logger = Log.ForContext<RouteService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;

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
                    var route = await context.Routes.FindAsync(id);
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

        #region Route Building Methods (MVP Priority)

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
                        validationResult.Errors.Add($"Route {routeId} not found");
                        return Result.SuccessResult(validationResult);
                    }

                    // Basic validation - route exists and has a name
                    if (string.IsNullOrWhiteSpace(route.RouteName))
                    {
                        validationResult.Errors.Add("Route name is required");
                    }

                    if (route.Date < DateTime.Today)
                    {
                        validationResult.Errors.Add("Route date cannot be in the past");
                    }

                    // TODO: Add more comprehensive validation (bus, driver, stops, students)
                    // For MVP, basic validation is sufficient

                    validationResult.IsValid = validationResult.Errors.Count == 0;

                    Logger.Information("Route validation completed. Valid: {IsValid}, Errors: {ErrorCount}",
                        validationResult.IsValid, validationResult.Errors.Count);

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

                // First validate the route
                var validationResult = await ValidateRouteForActivationAsync(routeId);
                if (!validationResult.IsSuccess)
                {
                    return Result.FailureResult<bool>(validationResult.Error!);
                }

                if (!validationResult.Value!.IsValid)
                {
                    var errors = string.Join("; ", validationResult.Value.Errors);
                    return Result.FailureResult<bool>($"Route validation failed: {errors}");
                }

                // Activate the route
                var context = _contextFactory.CreateDbContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    route!.IsActive = true;

                    await context.SaveChangesAsync();

                    Logger.Information("Successfully activated route {RouteId}: {RouteName}", routeId, route.RouteName);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    // Properly dispose the context when done
                    await context.DisposeAsync();
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
                var context = _contextFactory.CreateDbContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        return Result.FailureResult<bool>($"Route {routeId} not found");
                    }

                    route.IsActive = false;
                    await context.SaveChangesAsync();

                    Logger.Information("Successfully deactivated route {RouteId}", routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    // Properly dispose the context when done
                    await context.DisposeAsync();
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

        public Task<Result<IEnumerable<RouteStop>>> GetRouteStopsAsync(int routeId)
        {
            return Task.FromResult(Result.SuccessResult((IEnumerable<RouteStop>)new List<RouteStop>()));
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

        public async Task<Result<List<Bus>>> GetAvailableBusesAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var buses = await context.Buses
                        .Where(b => b.Status == "Active")
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
                        if (capacity <= 0) capacity = 30; // default MVP capacity
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
            if (route.AMVehicleId.HasValue)
            {
                var am = await context.Buses.FirstOrDefaultAsync(b => b.VehicleId == route.AMVehicleId.Value);
                if (am != null) amCap = am.SeatingCapacity;
            }
            if (route.PMVehicleId.HasValue)
            {
                var pm = await context.Buses.FirstOrDefaultAsync(b => b.VehicleId == route.PMVehicleId.Value);
                if (pm != null) pmCap = pm.SeatingCapacity;
            }
            return Math.Max(amCap, pmCap);
        }

        public async Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, RouteTimeSlot timeSlot)
        {
            try
            {
                if (routeId <= 0 || vehicleId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or vehicleId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    await using var transaction = await context.Database.BeginTransactionAsync();

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

                    // Availability check (Active status)
                    if (!string.Equals(bus.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Error("AssignVehicle failed — vehicle {VehicleId} not available (Status: {Status})", vehicleId, bus.Status);
                        return Result.FailureResult<bool>($"Vehicle {vehicleId} is not available");
                    }

                    // Apply assignment based on time slot
                    switch (timeSlot)
                    {
                        case RouteTimeSlot.AM:
                            route.AMVehicleId = vehicleId;
                            break;
                        case RouteTimeSlot.PM:
                            route.PMVehicleId = vehicleId;
                            break;
                        case RouteTimeSlot.Both:
                            route.AMVehicleId = vehicleId;
                            route.PMVehicleId = vehicleId;
                            break;
                        default:
                            Logger.Error("AssignVehicle failed — unsupported time slot {TimeSlot}", timeSlot);
                            return Result.FailureResult<bool>("Unsupported time slot");
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Logger.Information("Assigned vehicle {VehicleId} to route {RouteId} for {TimeSlot}", vehicleId, routeId, timeSlot);
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
                Logger.Error(ex, "Error assigning vehicle {VehicleId} to route {RouteId}", vehicleId, routeId);
                return Result.FailureResult<bool>($"Error assigning vehicle to route: {ex.Message}");
            }
        }

        public Task<Result<bool>> AssignDriverToRouteAsync(int routeId, int driverId, RouteTimeSlot timeSlot)
        {
            return Task.FromResult(Result.FailureResult<bool>("Not implemented yet"));
        }

    public async Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop routeStop)
        {
            try
            {
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
                    // Ensure route exists
                    var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route == null)
                    {
                        return Result.FailureResult<RouteStop>($"Route with ID {routeId} not found");
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

                    // Add and save changes asynchronously (EF Core best practice)
                    await context.RouteStops.AddAsync(routeStop);
                    await context.SaveChangesAsync();

                    Logger.Information("Added stop {StopName} (ID: {RouteStopId}) to route {RouteId}",
                        routeStop.StopName, routeStop.RouteStopId, routeId);

                    return Result.SuccessResult(routeStop);
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
                Logger.Error(ex, "Error adding stop to route {RouteId}", routeId);
                return Result.FailureResult<RouteStop>($"Error adding stop to route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId)
        {
            try
            {
                if (routeId <= 0 || stopId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or stopId");
                }

                var (context, dispose) = GetWriteContext();
                try
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

                    Logger.Information("Removed stop {StopId} from route {RouteId}", stopId, routeId);
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
                Logger.Error(ex, "Error removing stop {StopId} from route {RouteId}", stopId, routeId);
                return Result.FailureResult<bool>($"Error removing stop from route: {ex.Message}");
            }
        }

        public Task<Result<bool>> ReorderRouteStopsAsync(int routeId, List<int> orderedStopIds)
        {
            return Task.FromResult(Result.FailureResult<bool>("Not implemented yet"));
        }

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

        #endregion

    }

}








