#if DEBUG
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>DEBUG-only student record metrics. Not intake CRUD.</summary>
public static class StudentDiagnostics
{
    private static readonly ILogger Logger = Log.ForContext(typeof(StudentDiagnostics));

    public static async Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(
        IBusBuddyDbContextFactory contextFactory,
        int studentId)
    {
        try
        {
            Logger.Debug("Retrieving diagnostic information for student {StudentId}", studentId);

            using var context = contextFactory.CreateDbContext();
            var student = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found for diagnostics", studentId);
                return new Dictionary<string, object> { { "Error", "Student not found" } };
            }

            var diagnostics = new Dictionary<string, object>
            {
                { "StudentId", student.StudentId },
                { "StudentName", student.StudentName },
                { "RecordCreationTime", student.CreatedDate },
                { "LastUpdateTime", student.UpdatedDate ?? DateTime.MinValue },
                { "RecordAgeInDays", (DateTime.UtcNow - student.CreatedDate).TotalDays },
                { "RecordCompleteness", CalculateRecordCompleteness(student) },
                { "HasRequiredFields", !string.IsNullOrEmpty(student.ParentGuardian) &&
                                      !string.IsNullOrEmpty(student.EmergencyPhone) &&
                                      !string.IsNullOrEmpty(student.HomeAddress) &&
                                      !string.IsNullOrEmpty(student.Grade) },
                { "HasRouteAssignment", !string.IsNullOrEmpty(student.AMRoute) || !string.IsNullOrEmpty(student.PMRoute) },
                { "HasBusStopAssignment", !string.IsNullOrEmpty(student.BusStop) },
                { "HasMedicalNotes", !string.IsNullOrEmpty(student.MedicalNotes) },
                { "HasSpecialNeeds", student.SpecialNeeds },
                { "HasTransportationNotes", !string.IsNullOrEmpty(student.TransportationNotes) },
                { "IsActive", student.Active },
                { "ModelState", SerializeStudentForDiagnostics(student) }
            };

            return diagnostics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating diagnostics for student {StudentId}", studentId);
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    public static async Task<Dictionary<string, object>> GetStudentOperationMetricsAsync(
        IBusBuddyDbContextFactory contextFactory)
    {
        try
        {
            Logger.Debug("Retrieving student operation metrics");

            var metrics = new Dictionary<string, object>();
            using var context = contextFactory.CreateDbContext();

            metrics["TotalStudentCount"] = await context.Students.CountAsync();
            metrics["ActiveStudentCount"] = await context.Students.CountAsync(s => s.Active);
            metrics["InactiveStudentCount"] = await context.Students.CountAsync(s => !s.Active);
            metrics["StudentsWithRoutes"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithoutRoutes"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithBusStops"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithoutBusStops"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithSpecialNeeds"] = await context.Students.CountAsync(s => s.RequiresSpecialNeedsBus || !string.IsNullOrEmpty(s.SpecialNeeds));

            var sw = new System.Diagnostics.Stopwatch();

            sw.Start();
            await context.Students.AsNoTracking().ToListAsync();
            sw.Stop();
            metrics["AllStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => s.Active).ToListAsync();
            sw.Stop();
            metrics["ActiveStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => !string.IsNullOrEmpty(s.AMRoute)).ToListAsync();
            sw.Stop();
            metrics["StudentsWithAMRouteQueryTimeMs"] = sw.ElapsedMilliseconds;

            var students = await context.Students.AsNoTracking().ToListAsync();
            var completenessScores = students.Select(CalculateRecordCompleteness).ToList();

            metrics["AverageRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Average() : 0;
            metrics["MaxRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Max() : 0;
            metrics["MinRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Min() : 0;

            metrics["CompletenessDistribution"] = new Dictionary<string, int>
            {
                { "0-25%", completenessScores.Count(s => s >= 0 && s < 0.25) },
                { "25-50%", completenessScores.Count(s => s >= 0.25 && s < 0.5) },
                { "50-75%", completenessScores.Count(s => s >= 0.5 && s < 0.75) },
                { "75-100%", completenessScores.Count(s => s >= 0.75 && s <= 1.0) }
            };

            return metrics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating student operation metrics");
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    private static double CalculateRecordCompleteness(Student student)
    {
        var requiredFields = new[]
        {
            student.StudentName,
            student.Grade,
            student.School,
            student.HomeAddress,
            student.City,
            student.State,
            student.Zip,
            student.HomePhone,
            student.ParentGuardian,
            student.EmergencyPhone
        };

        var optionalFields = new[]
        {
            student.StudentNumber,
            student.AMRoute,
            student.PMRoute,
            student.BusStop,
            student.MedicalNotes,
            student.TransportationNotes,
            student.DateOfBirth.HasValue ? "HasValue" : null,
            student.Gender,
            student.PickupAddress,
            student.DropoffAddress,
            student.SpecialAccommodations,
            student.Allergies,
            student.Medications,
            student.DoctorName,
            student.DoctorPhone,
            student.AlternativeContact,
            student.AlternativePhone
        };

        var requiredCompleteness = requiredFields.Count(f => !string.IsNullOrWhiteSpace(f)) / (double)requiredFields.Length;
        var optionalCompleteness = optionalFields.Count(f => !string.IsNullOrWhiteSpace(f)) / (double)optionalFields.Length;
        return (requiredCompleteness * 0.7) + (optionalCompleteness * 0.3);
    }

    private static object SerializeStudentForDiagnostics(Student student)
    {
        return new
        {
            student.StudentId,
            student.StudentName,
            student.StudentNumber,
            student.Grade,
            student.School,
            Address = new { student.HomeAddress, student.City, student.State, student.Zip },
            Contact = new { student.HomePhone, student.ParentGuardian, student.EmergencyPhone },
            EmergencyContacts = new
            {
                student.AlternativeContact,
                student.AlternativePhone,
                student.DoctorName,
                student.DoctorPhone
            },
            TransportationDetails = new
            {
                student.AMRoute,
                student.PMRoute,
                student.BusStop,
                student.PickupAddress,
                student.DropoffAddress,
                student.TransportationNotes
            },
            MedicalDetails = new
            {
                student.MedicalNotes,
                student.SpecialNeeds,
                student.SpecialAccommodations,
                student.Allergies,
                student.Medications
            },
            StatusInfo = new
            {
                student.Active,
                student.EnrollmentDate,
                student.CreatedDate,
                student.UpdatedDate,
                student.CreatedBy,
                student.UpdatedBy
            }
        };
    }
}
#endif
