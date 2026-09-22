using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// What a student form starts with: the blank record a clerk sees on "Add student", and the database
/// context it reads catalogs through. Both need a working answer when dependency injection is absent
/// (design-time and unit tests), which is why they are resolved defensively rather than injected.
/// </summary>
internal static class StudentFormBootstrap
{
    /// <summary>
    /// A blank intake record for the current school year, active and eligible for both runs.
    /// <para>
    /// Pre-ticking AM and PM lives here and not on <c>Student</c> on purpose: a clerk sees the two
    /// checkboxes and can clear one before saving, whereas a CSV or JSON intake must never inherit an
    /// assumed eligibility it was not given.
    /// </para>
    /// </summary>
    public static StudentModel NewIntakeRecord() => new()
    {
        Active = true,
        EnrollmentDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
        CreatedDate = DateTime.UtcNow,
        School = string.Empty,
        State = "CO",
        RidesAm = true,
        RidesPm = true,
        SchoolYear = StudentRecordNormalizer.CurrentSchoolYear()
    };

    /// <summary>
    /// The app's configured context (BusBuddyDB) when DI is up. Callers fall back to a parameterless
    /// context, which is why a failure here returns null instead of throwing.
    /// </summary>
    public static BusBuddyDbContext? TryCreateDbContextViaDi()
    {
        try
        {
            var factory = App.ServiceProvider?.GetService(typeof(IBusBuddyDbContextFactory))
                as IBusBuddyDbContextFactory;
            return factory?.CreateDbContext();
        }
        catch
        {
            return null;
        }
    }
}
