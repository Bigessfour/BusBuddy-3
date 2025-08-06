using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces;

/// <summary>
/// Service interface for managing student-schedule assignments
/// Handles the relationship between students and scheduled trips/activities
/// </summary>
public interface IStudentScheduleService
{
    /// <summary>
    /// Assigns a student to a regular schedule
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="pickupLocation">Optional pickup location</param>
    /// <param name="dropoffLocation">Optional dropoff location</param>
    /// <param name="notes">Optional notes</param>
    /// <returns>Created StudentSchedule</returns>
    Task<StudentSchedule> AssignStudentToScheduleAsync(int studentId, int scheduleId, string? pickupLocation = null, string? dropoffLocation = null, string? notes = null);

    /// <summary>
    /// Assigns a student to an activity schedule
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="activityScheduleId">Activity Schedule ID</param>
    /// <param name="pickupLocation">Optional pickup location</param>
    /// <param name="dropoffLocation">Optional dropoff location</param>
    /// <param name="notes">Optional notes</param>
    /// <returns>Created StudentSchedule</returns>
    Task<StudentSchedule> AssignStudentToActivityScheduleAsync(int studentId, int activityScheduleId, string? pickupLocation = null, string? dropoffLocation = null, string? notes = null);

    /// <summary>
    /// Assigns multiple students to a schedule
    /// </summary>
    /// <param name="studentIds">List of student IDs</param>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="assignmentType">Type of assignment (Regular, Activity, SportTrip)</param>
    /// <returns>List of created StudentSchedules</returns>
    Task<List<StudentSchedule>> AssignStudentsToScheduleAsync(List<int> studentIds, int scheduleId, string assignmentType = "Regular");

    /// <summary>
    /// Removes a student from a schedule
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>True if successful</returns>
    Task<bool> RemoveStudentFromScheduleAsync(int studentId, int scheduleId);

    /// <summary>
    /// Removes a student from an activity schedule
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="activityScheduleId">Activity Schedule ID</param>
    /// <returns>True if successful</returns>
    Task<bool> RemoveStudentFromActivityScheduleAsync(int studentId, int activityScheduleId);

    /// <summary>
    /// Gets all students assigned to a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>List of students</returns>
    Task<List<Student>> GetStudentsForScheduleAsync(int scheduleId);

    /// <summary>
    /// Gets all students assigned to an activity schedule
    /// </summary>
    /// <param name="activityScheduleId">Activity Schedule ID</param>
    /// <returns>List of students</returns>
    Task<List<Student>> GetStudentsForActivityScheduleAsync(int activityScheduleId);

    /// <summary>
    /// Gets all schedules for a student
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <returns>List of schedules with assignment details</returns>
    Task<List<StudentSchedule>> GetSchedulesForStudentAsync(int studentId);

    /// <summary>
    /// Gets all activity schedules for a student
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <returns>List of activity schedules with assignment details</returns>
    Task<List<StudentSchedule>> GetActivitySchedulesForStudentAsync(int studentId);

    /// <summary>
    /// Confirms a student's attendance for a scheduled trip
    /// </summary>
    /// <param name="studentScheduleId">StudentSchedule ID</param>
    /// <param name="attended">Whether the student attended</param>
    /// <returns>True if successful</returns>
    Task<bool> ConfirmStudentAttendanceAsync(int studentScheduleId, bool attended);

    /// <summary>
    /// Updates student assignment details
    /// </summary>
    /// <param name="studentSchedule">StudentSchedule to update</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateStudentAssignmentAsync(StudentSchedule studentSchedule);

    /// <summary>
    /// Gets student assignment statistics
    /// </summary>
    /// <returns>Dictionary of statistics</returns>
    Task<Dictionary<string, int>> GetStudentAssignmentStatisticsAsync();
}
