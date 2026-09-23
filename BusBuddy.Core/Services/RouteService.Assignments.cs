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
    /// <summary>Student assignment and same-day not-riding exceptions.</summary>
    public partial class RouteService
    {
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

                    var slotCheck = RejectMismatchedSessionSlot<bool>(route, timeSlot);
                    if (slotCheck is not null)
                    {
                        return slotCheck;
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

                    RouteBusCapacity.Check busCapacity;
                    var pairedBusId = timeSlot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
                    if (pairedBusId is int sessionBusId)
                    {
                        var session = await _busService
                            .GetSessionLoadAsync(sessionBusId, route.RouteId, timeSlot)
                            .ConfigureAwait(false);
                        if (!session.IsSuccess || session.Value is null)
                        {
                            return Result.FailureResult<bool>(session.Error);
                        }

                        busCapacity = RouteBusCapacity.ForCandidate(
                            session.Value,
                            alreadyRiding: false,
                            student.RequiresWheelchair,
                            overrideSeating);
                    }
                    else
                    {
                        busCapacity = new RouteBusCapacity.Check(
                            false,
                            null,
                            $"No default bus is assigned for this {timeSlot} run, so seating capacity is unknown.");
                    }
                    if (busCapacity.Blocked)
                    {
                        return Result.FailureResult<bool>(
                            busCapacity.Message ?? $"Route '{route.RouteName}' is at {timeSlot} capacity");
                    }

                    if (busCapacity.Warning is not null)
                    {
                        Logger.Warning(
                            "Assign capacity warning Student={StudentId} Route={RouteId}: {Warning}",
                            studentId,
                            routeId,
                            busCapacity.Warning);
                    }
                    else if (busCapacity.Message is not null)
                    {
                        Logger.Information(
                            "Assign capacity override Student={StudentId} Route={RouteId}: {Message}",
                            studentId,
                            routeId,
                            busCapacity.Message);
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
    }
}
