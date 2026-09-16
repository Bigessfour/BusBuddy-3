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
    /// <summary>Student, vehicle, and driver assignment plus roster/capacity lookups.</summary>
    public partial class RouteService
    {
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
        private static bool IsCapacityRejection(string? error) =>
            !string.IsNullOrEmpty(error)
            && error.Contains("capacity", StringComparison.OrdinalIgnoreCase);
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

                    // BusBuddyDbContext defaults to NoTracking; test factories often forget TrackAll
                    // on write. Mark modified so leftover DriverService wrap persists either way.
                    context.Entry(route).State = EntityState.Modified;
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
    }
}
