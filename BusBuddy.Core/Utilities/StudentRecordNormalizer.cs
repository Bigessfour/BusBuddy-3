using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Normalizes student fields before EF save (legacy DB defaults, Postgres timestamptz UTC).
/// </summary>
public static class StudentRecordNormalizer
{
    public static void NormalizeForPersistence(Student student)
    {
        NormalizeOptionalForeignKeys(student);
        NormalizeDateTimes(student);
    }

    public static void NormalizeOptionalForeignKeys(Student student)
    {
        if (student.FamilyId is <= 0)
        {
            student.FamilyId = null;
        }

        if (student.DestinationId is <= 0)
        {
            student.DestinationId = null;
        }

        if (student.PickupStopId is <= 0)
        {
            student.PickupStopId = null;
        }

        if (student.RouteAssignmentId is <= 0)
        {
            student.RouteAssignmentId = null;
        }
    }

    public static void NormalizeDateTimes(Student student)
    {
        student.CreatedDate = ToUtc(student.CreatedDate);
        if (student.UpdatedDate.HasValue)
        {
            student.UpdatedDate = ToUtc(student.UpdatedDate.Value);
        }

        if (student.EnrollmentDate.HasValue)
        {
            student.EnrollmentDate = ToUtcDate(student.EnrollmentDate.Value);
        }

        if (student.DateOfBirth.HasValue)
        {
            student.DateOfBirth = ToUtcDate(student.DateOfBirth.Value);
        }
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static DateTime ToUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
