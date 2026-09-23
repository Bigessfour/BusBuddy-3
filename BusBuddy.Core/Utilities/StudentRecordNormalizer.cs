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
        NormalizeSchoolYear(student);
        NormalizePhones(student);
        EnforceSpecialNeedsHomePickup(student);
    }

    /// <summary>
    /// Rewrites a normalizable phone to <c>(NPA) NXX-XXXX</c>. A value that cannot be normalized
    /// is left unchanged so validation can reject it.
    /// </summary>
    public static void NormalizePhones(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        student.HomePhone = CanonicalPhone(student.HomePhone);
        student.CellPhone = CanonicalPhone(student.CellPhone);
        student.EmergencyPhone = CanonicalPhone(student.EmergencyPhone);
    }

    private static string? CanonicalPhone(string? phone) =>
        StudentPhone.TryNormalize(phone, out var normalized) ? normalized : phone;

    /// <summary>
    /// SchoolYear is required by the domain contract; default a blank one to the current year so an
    /// intake row is never orphaned from its year.
    /// </summary>
    public static void NormalizeSchoolYear(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        student.SchoolYear = string.IsNullOrWhiteSpace(student.SchoolYear)
            ? CurrentSchoolYear()
            : student.SchoolYear.Trim();
    }

    /// <summary>
    /// specs/students.md: special needs forces home pickup, so a catalog stop cannot stay attached.
    /// </summary>
    public static void EnforceSpecialNeedsHomePickup(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (student.RequiresSpecialNeedsBus)
        {
            student.PickupStopId = null;
            if (student.StudentId <= 0)
            {
                student.RequiresAide = true;
            }
        }
    }

    /// <summary>School year label in <c>2026-2027</c> form; rolls over on 1 July.</summary>
    public static string CurrentSchoolYear(DateTime? asOf = null)
    {
        var date = asOf ?? DateTime.UtcNow;
        var startYear = date.Month >= 7 ? date.Year : date.Year - 1;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{startYear}-{startYear + 1}");
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
