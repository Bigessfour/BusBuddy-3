namespace BusBuddy.Core.Models;

/// <summary>
/// AM / PM / Both ride participation derived from student route assignments.
/// Occasional-rider stops are retained on the unused slot's mirror route.
/// </summary>
public enum StudentRideMode
{
    Neither = 0,
    AM = 1,
    PM = 2,
    Both = 3
}

/// <summary>Helpers to derive <see cref="StudentRideMode"/> from AM/PM route name fields.</summary>
public static class StudentRideModeHelper
{
    public static StudentRideMode FromRouteNames(string? amRoute, string? pmRoute)
    {
        var hasAm = !string.IsNullOrWhiteSpace(amRoute);
        var hasPm = !string.IsNullOrWhiteSpace(pmRoute);
        if (hasAm && hasPm)
        {
            return StudentRideMode.Both;
        }

        if (hasAm)
        {
            return StudentRideMode.AM;
        }

        if (hasPm)
        {
            return StudentRideMode.PM;
        }

        return StudentRideMode.Neither;
    }

    public static StudentRideMode FromStudent(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        return FromRouteNames(student.AMRoute, student.PMRoute);
    }

    /// <summary>True when the student should keep a stop on the PM mirror even if AM-only.</summary>
    public static bool RetainStopOnPmMirror(StudentRideMode mode) =>
        mode is StudentRideMode.AM or StudentRideMode.Both or StudentRideMode.Neither;

    /// <summary>True when the student should keep a stop on the AM mirror even if PM-only.</summary>
    public static bool RetainStopOnAmMirror(StudentRideMode mode) =>
        mode is StudentRideMode.PM or StudentRideMode.Both or StudentRideMode.Neither;
}
