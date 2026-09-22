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
                return Result.FailureResult<List<Bus>>($"Error retrieving buses: {ex.Message}", ex);
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
                return Result.FailureResult<List<Driver>>($"Error retrieving drivers: {ex.Message}", ex);
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

                var slotCheck = await RejectMismatchedSessionSlotAsync<bool>(routeId, timeSlot).ConfigureAwait(false);
                if (slotCheck is not null)
                {
                    return slotCheck;
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
                        if (fitness.SuggestedRouteIds.Count > 0)
                        {
                            detail += ". Suggested route ids: " + string.Join(", ", fitness.SuggestedRouteIds);
                        }

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

                    if (timeSlot == RouteTimeSlot.AM && !student.RidesAm)
                    {
                        return Result.FailureResult<bool>(
                            $"Student {studentId} is not eligible for the AM run.");
                    }

                    if (timeSlot == RouteTimeSlot.PM && !student.RidesPm)
                    {
                        return Result.FailureResult<bool>(
                            $"Student {studentId} is not eligible for the PM run.");
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

        public async Task<Result<bool>> ClearRiderExceptionAsync(int routeId, int studentId, DateTime exceptionDate)
        {
            try
            {
                if (routeId <= 0 || studentId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or studentId");
                }

                var day = DateTime.SpecifyKind(exceptionDate.Date, DateTimeKind.Utc);
                var (context, dispose) = GetWriteContext();
                try
                {
                    var existing = await context.RouteRiderExceptions.FirstOrDefaultAsync(e =>
                        e.RouteId == routeId
                        && e.StudentId == studentId
                        && e.ExceptionDate == day);
                    if (existing is null)
                    {
                        return Result.SuccessResult(true);
                    }

                    context.RouteRiderExceptions.Remove(existing);
                    await context.SaveChangesAsync();
                    Logger.Information(
                        "Cleared rider exception RouteId={RouteId} StudentId={StudentId} Date={Date} — year assignment unchanged",
                        routeId,
                        studentId,
                        day);
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
                DatabaseUserMessage.LogFailure(
                    Logger,
                    ex,
                    "Error clearing rider exception RouteId={RouteId} StudentId={StudentId}",
                    routeId,
                    studentId);
                return Result.FailureResult<bool>($"Error clearing rider exception: {ex.Message}");
            }
        }

        public async Task<Result<IReadOnlyList<int>>> GetRiderExceptionStudentIdsAsync(int routeId, DateTime exceptionDate)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<IReadOnlyList<int>>("Invalid routeId");
                }

                var day = DateTime.SpecifyKind(exceptionDate.Date, DateTimeKind.Utc);
                var (context, dispose) = GetReadContext();
                try
                {
                    var ids = await context.RouteRiderExceptions
                        .AsNoTracking()
                        .Where(e => e.RouteId == routeId && e.ExceptionDate == day)
                        .Select(e => e.StudentId)
                        .Distinct()
                        .ToListAsync();
                    return Result.SuccessResult<IReadOnlyList<int>>(ids);
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
                    "Error loading rider exceptions RouteId={RouteId}",
                    routeId);
                return Result.FailureResult<IReadOnlyList<int>>($"Error loading rider exceptions: {ex.Message}");
            }
        }

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

                var slotCheck = await RejectMismatchedSessionSlotAsync<List<Student>>(routeId, timeSlot)
                    .ConfigureAwait(false);
                if (slotCheck is not null)
                {
                    return slotCheck;
                }

                var routeResult = await GetRouteByIdAsync(routeId).ConfigureAwait(false);
                if (!routeResult.IsSuccess || routeResult.Value is null)
                {
                    return Result.FailureResult<List<Student>>(routeResult.Error ?? $"Route with ID {routeId} not found");
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
            && error.Contains("capacity", StringComparison.OrdinalIgnoreCase);
        private static async Task<int> GetAssignedCountForSlotAsync(BusBuddyDbContext context, Route route, RouteTimeSlot timeSlot)
        {
            return await context.Students
                .WhereOnSlot(route.RouteId, route.RouteName, timeSlot)
                .CountAsync();
        }

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
        /// Null when the requested slot matches this row. Otherwise a failure the caller can return.
        /// </summary>
        private async Task<Result<T>?> RejectMismatchedSessionSlotAsync<T>(int routeId, RouteTimeSlot timeSlot)
        {
            var (context, dispose) = GetReadContext();
            try
            {
                var route = await context.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.RouteId == routeId);
                if (route is null)
                {
                    return Result.FailureResult<T>($"Route with ID {routeId} not found");
                }

                var expected = RouteSession.ToAssignmentSlot(route);
                if (timeSlot == expected)
                {
                    return null;
                }

                return Result.FailureResult<T>(
                    $"Route '{route.RouteName}' is the {expected} run. Assign this student on the {expected} slot.");
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
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

                    // BusBuddyDbContext defaults to NoTracking. Find can return an untracked
                    // route, and SaveChanges would then skip the pairing columns.
                    context.Entry(route).State = EntityState.Modified;
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
