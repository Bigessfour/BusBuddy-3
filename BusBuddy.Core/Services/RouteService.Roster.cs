using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services;
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
    /// <summary>Session roster, unassigned students, and route capacity listings.</summary>
    public partial class RouteService
    {
        public async Task<Result<RouteSessionLoad>> GetSessionLoadAsync(int routeId, DateTime serviceDate)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<RouteSessionLoad>("Invalid routeId");
                }

                var day = DateTime.SpecifyKind(serviceDate.Date, DateTimeKind.Utc);
                var (context, dispose) = GetReadContext();
                try
                {
                    var route = await context.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.RouteId == routeId);
                    if (route is null)
                    {
                        return Result.FailureResult<RouteSessionLoad>($"Route with ID {routeId} not found");
                    }

                    var slot = RouteSession.ToAssignmentSlot(route);
                    var assignedIds = await context.Students.AsNoTracking()
                        .WhereOnSlot(route.RouteId, route.RouteName, slot)
                        .Select(s => s.StudentId)
                        .ToListAsync();
                    var exceptionIds = await context.RouteRiderExceptions.AsNoTracking()
                        .Where(e => e.RouteId == routeId && e.ExceptionDate == day)
                        .Select(e => e.StudentId)
                        .ToListAsync();
                    var notRiding = assignedIds.Intersect(exceptionIds).Count();
                    var capacity = await GetCapacityForSlotAsync(context, route, slot);
                    string? warning = capacity <= 0
                        ? $"No default bus is assigned for this {slot} run, so seating capacity is unknown."
                        : null;
                    var load = new RouteSessionLoad
                    {
                        RouteId = routeId,
                        Session = RouteSession.IsKnown(route.Session) ? route.Session! : RouteSession.Infer(route),
                        Slot = slot,
                        AssignedCount = assignedIds.Count,
                        NotRidingCount = notRiding,
                        LoadCount = Math.Max(0, assignedIds.Count - notRiding),
                        Capacity = capacity,
                        Warning = warning
                    };
                    return warning is null
                        ? Result.SuccessResult(load)
                        : Result.SuccessResult(load, warning);
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error loading session load RouteId={RouteId}", routeId);
                return Result.FailureResult<RouteSessionLoad>($"Error loading session load: {ex.Message}");
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
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}", ex);
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
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}", ex);
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
                return Result.FailureResult<List<Student>>($"Error retrieving students: {ex.Message}", ex);
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

                var routeResult = await GetRouteByIdAsync(routeId).ConfigureAwait(false);
                if (!routeResult.IsSuccess || routeResult.Value is null)
                {
                    return Result.FailureResult<List<Student>>(routeResult.Error ?? $"Route with ID {routeId} not found");
                }

                var slotCheck = RejectMismatchedSessionSlot<List<Student>>(routeResult.Value, timeSlot);
                if (slotCheck is not null)
                {
                    return slotCheck;
                }

                var school = routeResult.Value.School?.Trim();
                var unassignedResult = await GetUnassignedStudentsAsync(timeSlot);
                if (!unassignedResult.IsSuccess || unassignedResult.Value is null)
                {
                    return Result.FailureResult<List<Student>>(unassignedResult.Error ?? "Failed to load unassigned students");
                }

                var candidates = unassignedResult.Value;
                if (!string.IsNullOrWhiteSpace(school))
                {
                    candidates = candidates
                        .Where(s => string.Equals(s.School?.Trim(), school, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var assigned = new List<Student>();
                foreach (var student in candidates)
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
                    var routes = await context.Routes.AsNoTracking().Where(r => r.IsActive).ToListAsync();
                    var loads = await LoadSessionSeatCountsAsync(context, routes);
                    var result = new List<Route>();
                    foreach (var route in routes)
                    {
                        var (assigned, capacity) = loads[route.RouteId];
                        if (capacity > 0 && assigned < capacity)
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
                    var routes = await context.Routes.AsNoTracking().ToListAsync();
                    var totalRoutes = routes.Count;
                    var assignmentFlags = await context.Students.AsNoTracking()
                        .Select(s => new { s.AmRouteId, s.PmRouteId, s.AMRoute, s.PMRoute })
                        .ToListAsync();
                    var totalAssigned = assignmentFlags.Count(s =>
                        s.AmRouteId is > 0 || s.PmRouteId is > 0
                        || !string.IsNullOrWhiteSpace(s.AMRoute)
                        || !string.IsNullOrWhiteSpace(s.PMRoute));
                    var totalUnassigned = assignmentFlags.Count - totalAssigned;
                    var loads = await LoadSessionSeatCountsAsync(context, routes);

                    int totalCapacity = 0;
                    double utilizationSum = 0;
                    int routesAtCapacity = 0;
                    int underutilized = 0;
                    int ratedRoutes = 0;

                    foreach (var route in routes)
                    {
                        var (assigned, capacity) = loads[route.RouteId];
                        if (capacity <= 0)
                        {
                            continue;
                        }

                        ratedRoutes++;
                        totalCapacity += capacity;
                        var utilization = (double)assigned / capacity;
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
                        AverageUtilizationRate = ratedRoutes > 0 ? utilizationSum / ratedRoutes : 0.0,
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
        private static bool IsCapacityRejection(string? error) =>
            !string.IsNullOrEmpty(error)
            && error.Contains("would be exceeded", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Seating capacity of the bus on this slot. Zero when that slot has no bus — do not invent seats.
        /// </summary>
        private static async Task<int> GetCapacityForSlotAsync(BusBuddyDbContext context, Route route, RouteTimeSlot timeSlot)
        {
            var vehicleId = SessionVehicleId(route, timeSlot);
            if (vehicleId is not int id)
            {
                return 0;
            }

            var bus = await context.Buses.AsNoTracking().FirstOrDefaultAsync(b => b.BusId == id);
            return bus is not null && bus.SeatingCapacity > 0 ? bus.SeatingCapacity : 0;
        }

        private static int? SessionVehicleId(Route route, RouteTimeSlot timeSlot) =>
            timeSlot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;

        /// <summary>
        /// One roster read and one bus read for the whole list. Counts the row's session slot only.
        /// </summary>
        private static async Task<Dictionary<int, (int Assigned, int Capacity)>> LoadSessionSeatCountsAsync(
            BusBuddyDbContext context,
            IReadOnlyList<Route> routes)
        {
            var result = new Dictionary<int, (int Assigned, int Capacity)>();
            if (routes.Count == 0)
            {
                return result;
            }

            var names = await context.Routes.AsNoTracking()
                .Select(r => r.RouteName)
                .ToListAsync();
            var vehicleIds = routes
                .Select(r => SessionVehicleId(r, RouteSession.ToAssignmentSlot(r)))
                .Where(id => id is > 0)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var capacities = vehicleIds.Count == 0
                ? new Dictionary<int, int>()
                : await context.Buses.AsNoTracking()
                    .Where(b => vehicleIds.Contains(b.BusId))
                    .Select(b => new { b.BusId, b.SeatingCapacity })
                    .ToDictionaryAsync(b => b.BusId, b => b.SeatingCapacity);

            var roster = await context.Students.AsNoTracking()
                .Select(s => new { s.AmRouteId, s.PmRouteId, s.AMRoute, s.PMRoute })
                .ToListAsync();

            foreach (var route in routes)
            {
                var slot = RouteSession.ToAssignmentSlot(route);
                var uniqueName = names.Count(n => NamesEqual(n, route.RouteName)) == 1;
                var assigned = roster.Count(s => slot == RouteTimeSlot.PM
                    ? s.PmRouteId == route.RouteId
                        || (uniqueName && s.PmRouteId == null && NamesEqual(s.PMRoute, route.RouteName))
                    : s.AmRouteId == route.RouteId
                        || (uniqueName && s.AmRouteId == null && NamesEqual(s.AMRoute, route.RouteName)));
                var capacity = 0;
                var vehicleId = SessionVehicleId(route, slot);
                if (vehicleId is int id && capacities.TryGetValue(id, out var seats) && seats > 0)
                {
                    capacity = seats;
                }

                result[route.RouteId] = (assigned, capacity);
            }

            return result;
        }

        /// <summary>
        /// Null when the requested slot matches this already-loaded row. Otherwise a failure the caller can return.
        /// </summary>
        private static Result<T>? RejectMismatchedSessionSlot<T>(Route route, RouteTimeSlot timeSlot)
        {
            var expected = RouteSession.ToAssignmentSlot(route);
            if (timeSlot == expected)
            {
                return null;
            }

            return Result.FailureResult<T>(
                $"Route '{route.RouteName}' is the {expected} run. Assign this student on the {expected} slot.");
        }
    }
}
