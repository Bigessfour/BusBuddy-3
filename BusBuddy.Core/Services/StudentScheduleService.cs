using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service implementation for managing student-schedule assignments
/// Handles the relationship between students and scheduled trips/activities
/// </summary>
public class StudentScheduleService : IStudentScheduleService
{
    private static readonly ILogger Logger = Log.ForContext<StudentScheduleService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public StudentScheduleService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<StudentSchedule> AssignStudentToScheduleAsync(int studentId, int scheduleId, string? pickupLocation = null, string? dropoffLocation = null, string? notes = null)
    {
        using var context = _contextFactory.CreateWriteDbContext();

        using (Serilog.Context.LogContext.PushProperty("StudentId", studentId))
        using (Serilog.Context.LogContext.PushProperty("ScheduleId", scheduleId))
        using (Serilog.Context.LogContext.PushProperty("PickupLocation", pickupLocation))
        using (Serilog.Context.LogContext.PushProperty("DropoffLocation", dropoffLocation))
        using (Serilog.Context.LogContext.PushProperty("Operation", "AssignStudentToSchedule"))
        {
            try
            {
                Logger.Information("Starting student assignment to schedule");

                // Check if assignment already exists
                var existingAssignment = await context.StudentSchedules
                    .FirstOrDefaultAsync(ss => ss.StudentId == studentId && ss.ScheduleId == scheduleId);

                if (existingAssignment != null)
                {
                    Logger.Warning("Student is already assigned to schedule, returning existing assignment");
                    return existingAssignment;
                }

                // Verify student and schedule exist
                var student = await context.Students.FindAsync(studentId);
                var schedule = await context.Schedules.FindAsync(scheduleId);

                if (student == null)
                {
                    Logger.Error("Student not found in database");
                    throw new ArgumentException($"Student with ID {studentId} not found");
                }
                if (schedule == null)
                {
                    Logger.Error("Schedule not found in database");
                    throw new ArgumentException($"Schedule with ID {scheduleId} not found");
                }

                Logger.Information("Student and schedule validated successfully");

                var studentSchedule = new StudentSchedule
                {
                    StudentId = studentId,
                    ScheduleId = scheduleId,
                    AssignmentType = "Regular",
                    PickupLocation = pickupLocation,
                    DropoffLocation = dropoffLocation,
                    Notes = notes,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = Environment.UserName
                };

                context.StudentSchedules.Add(studentSchedule);
                await context.SaveChangesAsync();

                Logger.Information("Successfully assigned student to schedule with ID {StudentScheduleId}",
                    studentSchedule.StudentScheduleId);
                return studentSchedule;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while assigning student to schedule");
                throw;
            }
        }
    }
    public async Task<StudentSchedule> AssignStudentToActivityScheduleAsync(int studentId, int activityScheduleId, string? pickupLocation = null, string? dropoffLocation = null, string? notes = null)
    {
        try
        {
            Logger.Information("Assigning student {StudentId} to activity schedule {ActivityScheduleId}", studentId, activityScheduleId);

            using var context = _contextFactory.CreateWriteDbContext();

            // Check if assignment already exists
            var existingAssignment = await context.StudentSchedules
                .FirstOrDefaultAsync(ss => ss.StudentId == studentId && ss.ActivityScheduleId == activityScheduleId);

            if (existingAssignment != null)
            {
                Logger.Warning("Student {StudentId} is already assigned to activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
                return existingAssignment;
            }

            // Verify student and activity schedule exist
            var student = await context.Students.FindAsync(studentId);
            var activitySchedule = await context.ActivitySchedule.FindAsync(activityScheduleId);

            if (student == null)
            {

                throw new ArgumentException($"Student with ID {studentId} not found");
            }


            if (activitySchedule == null)
            {

                throw new ArgumentException($"Activity schedule with ID {activityScheduleId} not found");
            }


            var studentSchedule = new StudentSchedule
            {
                StudentId = studentId,
                ActivityScheduleId = activityScheduleId,
                AssignmentType = activitySchedule.TripType,
                PickupLocation = pickupLocation,
                DropoffLocation = dropoffLocation,
                Notes = notes,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = Environment.UserName
            };

            context.StudentSchedules.Add(studentSchedule);
            await context.SaveChangesAsync();

            Logger.Information("Successfully assigned student {StudentId} to activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
            return studentSchedule;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning student {StudentId} to activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
            throw;
        }
    }

    public async Task<List<StudentSchedule>> AssignStudentsToScheduleAsync(List<int> studentIds, int scheduleId, string assignmentType = "Regular")
    {
        try
        {
            Logger.Information("Assigning {Count} students to schedule {ScheduleId}", studentIds.Count, scheduleId);

            using var context = _contextFactory.CreateWriteDbContext();

            // Verify schedule exists
            var schedule = await context.Schedules.FindAsync(scheduleId);
            if (schedule == null)
            {

                throw new ArgumentException($"Schedule with ID {scheduleId} not found");
            }


            var studentSchedules = new List<StudentSchedule>();

            foreach (var studentId in studentIds)
            {
                // Check if assignment already exists
                var existingAssignment = await context.StudentSchedules
                    .FirstOrDefaultAsync(ss => ss.StudentId == studentId && ss.ScheduleId == scheduleId);

                if (existingAssignment != null)
                {
                    Logger.Warning("Student {StudentId} is already assigned to schedule {ScheduleId}", studentId, scheduleId);
                    continue;
                }

                // Verify student exists
                var student = await context.Students.FindAsync(studentId);
                if (student == null)
                {
                    Logger.Warning("Student with ID {StudentId} not found, skipping", studentId);
                    continue;
                }

                var studentSchedule = new StudentSchedule
                {
                    StudentId = studentId,
                    ScheduleId = scheduleId,
                    AssignmentType = assignmentType,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = Environment.UserName
                };

                context.StudentSchedules.Add(studentSchedule);
                studentSchedules.Add(studentSchedule);
            }

            await context.SaveChangesAsync();

            Logger.Information("Successfully assigned {Count} students to schedule {ScheduleId}", studentSchedules.Count, scheduleId);
            return studentSchedules;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning students to schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<bool> RemoveStudentFromScheduleAsync(int studentId, int scheduleId)
    {
        try
        {
            Logger.Information("Removing student {StudentId} from schedule {ScheduleId}", studentId, scheduleId);

            using var context = _contextFactory.CreateWriteDbContext();

            var studentSchedule = await context.StudentSchedules
                .FirstOrDefaultAsync(ss => ss.StudentId == studentId && ss.ScheduleId == scheduleId);

            if (studentSchedule == null)
            {
                Logger.Warning("Student {StudentId} is not assigned to schedule {ScheduleId}", studentId, scheduleId);
                return false;
            }

            context.StudentSchedules.Remove(studentSchedule);
            await context.SaveChangesAsync();

            Logger.Information("Successfully removed student {StudentId} from schedule {ScheduleId}", studentId, scheduleId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing student {StudentId} from schedule {ScheduleId}", studentId, scheduleId);
            throw;
        }
    }

    public async Task<bool> RemoveStudentFromActivityScheduleAsync(int studentId, int activityScheduleId)
    {
        try
        {
            Logger.Information("Removing student {StudentId} from activity schedule {ActivityScheduleId}", studentId, activityScheduleId);

            using var context = _contextFactory.CreateWriteDbContext();

            var studentSchedule = await context.StudentSchedules
                .FirstOrDefaultAsync(ss => ss.StudentId == studentId && ss.ActivityScheduleId == activityScheduleId);

            if (studentSchedule == null)
            {
                Logger.Warning("Student {StudentId} is not assigned to activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
                return false;
            }

            context.StudentSchedules.Remove(studentSchedule);
            await context.SaveChangesAsync();

            Logger.Information("Successfully removed student {StudentId} from activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing student {StudentId} from activity schedule {ActivityScheduleId}", studentId, activityScheduleId);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsForScheduleAsync(int scheduleId)
    {
        using var context = _contextFactory.CreateDbContext();

        using (Serilog.Context.LogContext.PushProperty("ScheduleId", scheduleId))
        using (Serilog.Context.LogContext.PushProperty("Operation", "GetStudentsForSchedule"))
        {
            try
            {
                Logger.Information("Starting to retrieve students for schedule");

                var students = await context.StudentSchedules
                    .Where(ss => ss.ScheduleId == scheduleId)
                    .Include(ss => ss.Student)
                    .Select(ss => ss.Student)
                    .ToListAsync();

                Logger.Information("Successfully retrieved {StudentCount} students for schedule", students.Count);

                // Log detailed student information for diagnostics
                foreach (var student in students)
                {
                    Logger.Debug("Retrieved student: {StudentId} - {StudentName}",
                        student.StudentId, student.StudentName);
                }

                return students;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while retrieving students for schedule");
                throw;
            }
        }
    }
    public async Task<List<Student>> GetStudentsForActivityScheduleAsync(int activityScheduleId)
    {
        try
        {
            Logger.Information("Getting students for activity schedule {ActivityScheduleId}", activityScheduleId);

            using var context = _contextFactory.CreateDbContext();

            var students = await context.StudentSchedules
                .Where(ss => ss.ActivityScheduleId == activityScheduleId)
                .Include(ss => ss.Student)
                .Select(ss => ss.Student)
                .ToListAsync();

            Logger.Information("Found {Count} students for activity schedule {ActivityScheduleId}", students.Count, activityScheduleId);
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting students for activity schedule {ActivityScheduleId}", activityScheduleId);
            throw;
        }
    }

    public async Task<List<StudentSchedule>> GetSchedulesForStudentAsync(int studentId)
    {
        using var context = _contextFactory.CreateDbContext();

        using (Serilog.Context.LogContext.PushProperty("StudentId", studentId))
        using (Serilog.Context.LogContext.PushProperty("Operation", "GetSchedulesForStudent"))
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Logger.Information("Starting to retrieve schedules for student");

                var schedules = await context.StudentSchedules
                    .Where(ss => ss.StudentId == studentId && ss.ScheduleId != null)
                    .Include(ss => ss.Schedule)
                    .ThenInclude(s => s!.Bus)
                    .Include(ss => ss.Schedule)
                    .ThenInclude(s => s!.Route)
                    .Include(ss => ss.Schedule)
                    .ThenInclude(s => s!.Driver)
                    .ToListAsync();

                stopwatch.Stop();
                Logger.Information("Successfully retrieved {ScheduleCount} schedules for student in {ElapsedMs}ms",
                    schedules.Count, stopwatch.ElapsedMilliseconds);

                // Log detailed schedule information for diagnostics
                foreach (var schedule in schedules)
                {
                    Logger.Debug("Retrieved schedule: {ScheduleId} - {BusNumber} - {RouteName} - {DriverName}",
                        schedule.ScheduleId,
                        schedule.Schedule?.Bus?.BusNumber ?? "Unknown Bus",
                        schedule.Schedule?.Route?.RouteName ?? "Unknown Route",
                        schedule.Schedule?.Driver?.DriverName ?? "Unknown Driver");
                }

                return schedules;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error occurred while retrieving schedules for student after {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
    public async Task<List<StudentSchedule>> GetActivitySchedulesForStudentAsync(int studentId)
    {
        try
        {
            Logger.Information("Getting activity schedules for student {StudentId}", studentId);

            using var context = _contextFactory.CreateDbContext();

            var schedules = await context.StudentSchedules
                .Where(ss => ss.StudentId == studentId && ss.ActivityScheduleId != null)
                .Include(ss => ss.ActivitySchedule)
                .ThenInclude(a => a!.ScheduledVehicle)
                .Include(ss => ss.ActivitySchedule)
                .ThenInclude(a => a!.ScheduledDriver)
                .ToListAsync();

            Logger.Information("Found {Count} activity schedules for student {StudentId}", schedules.Count, studentId);
            return schedules;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting activity schedules for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> ConfirmStudentAttendanceAsync(int studentScheduleId, bool attended)
    {
        try
        {
            Logger.Information("Confirming attendance for student schedule {StudentScheduleId}: {Attended}", studentScheduleId, attended);

            using var context = _contextFactory.CreateWriteDbContext();

            var studentSchedule = await context.StudentSchedules.FindAsync(studentScheduleId);
            if (studentSchedule == null)
            {
                Logger.Warning("Student schedule {StudentScheduleId} not found", studentScheduleId);
                return false;
            }

            studentSchedule.Attended = attended;
            studentSchedule.Confirmed = true;
            studentSchedule.UpdatedDate = DateTime.UtcNow;
            studentSchedule.UpdatedBy = Environment.UserName;

            await context.SaveChangesAsync();

            Logger.Information("Successfully confirmed attendance for student schedule {StudentScheduleId}", studentScheduleId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error confirming attendance for student schedule {StudentScheduleId}", studentScheduleId);
            throw;
        }
    }

    public async Task<bool> UpdateStudentAssignmentAsync(StudentSchedule studentSchedule)
    {
        try
        {
            Logger.Information("Updating student assignment {StudentScheduleId}", studentSchedule.StudentScheduleId);

            using var context = _contextFactory.CreateWriteDbContext();

            var existingAssignment = await context.StudentSchedules.FindAsync(studentSchedule.StudentScheduleId);
            if (existingAssignment == null)
            {
                Logger.Warning("Student schedule {StudentScheduleId} not found", studentSchedule.StudentScheduleId);
                return false;
            }

            existingAssignment.PickupLocation = studentSchedule.PickupLocation;
            existingAssignment.DropoffLocation = studentSchedule.DropoffLocation;
            existingAssignment.Notes = studentSchedule.Notes;
            existingAssignment.Confirmed = studentSchedule.Confirmed;
            existingAssignment.Attended = studentSchedule.Attended;
            existingAssignment.UpdatedDate = DateTime.UtcNow;
            existingAssignment.UpdatedBy = Environment.UserName;

            await context.SaveChangesAsync();

            Logger.Information("Successfully updated student assignment {StudentScheduleId}", studentSchedule.StudentScheduleId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating student assignment {StudentScheduleId}", studentSchedule.StudentScheduleId);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetStudentAssignmentStatisticsAsync()
    {
        using var context = _contextFactory.CreateDbContext();

        using (Serilog.Context.LogContext.PushProperty("Operation", "GetStudentAssignmentStatistics"))
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Logger.Information("Starting to calculate student assignment statistics");

                var stats = new Dictionary<string, int>
                {
                    ["TotalAssignments"] = await context.StudentSchedules.CountAsync(),
                    ["RegularAssignments"] = await context.StudentSchedules.CountAsync(ss => ss.AssignmentType == "Regular"),
                    ["ActivityAssignments"] = await context.StudentSchedules.CountAsync(ss => ss.AssignmentType == "Activity"),
                    ["SportTripAssignments"] = await context.StudentSchedules.CountAsync(ss => ss.AssignmentType == "SportTrip"),
                    ["ConfirmedAssignments"] = await context.StudentSchedules.CountAsync(ss => ss.Confirmed),
                    ["AttendedAssignments"] = await context.StudentSchedules.CountAsync(ss => ss.Attended),
                    ["StudentsWithAssignments"] = await context.StudentSchedules.Select(ss => ss.StudentId).Distinct().CountAsync(),
                    ["SchedulesWithAssignments"] = await context.StudentSchedules.Where(ss => ss.ScheduleId != null).Select(ss => ss.ScheduleId).Distinct().CountAsync(),
                    ["ActivitySchedulesWithAssignments"] = await context.StudentSchedules.Where(ss => ss.ActivityScheduleId != null).Select(ss => ss.ActivityScheduleId).Distinct().CountAsync()
                };

                stopwatch.Stop();
                Logger.Information("Successfully calculated student assignment statistics in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                // Log detailed statistics for diagnostics
                foreach (var stat in stats)
                {
                    Logger.Debug("Statistic: {StatisticName} = {Value}", stat.Key, stat.Value);
                }

                return stats;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error occurred while calculating student assignment statistics after {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
