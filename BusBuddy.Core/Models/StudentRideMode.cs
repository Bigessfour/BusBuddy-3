namespace BusBuddy.Core.Models;

/// <summary>
/// AM / PM / Both ride participation taken from the student's explicit eligibility flags.
/// Occasional-rider stops are retained on the unused slot's mirror route.
/// </summary>
public enum StudentRideMode
{
    Neither = 0,
    AM = 1,
    PM = 2,
    Both = 3
}

/// <summary>Helpers to read <see cref="StudentRideMode"/> from student ride eligibility.</summary>
public static class StudentRideModeHelper
{
    /// <summary>
    /// Ride mode from explicit AM/PM eligibility. This is the authoritative source: eligibility is
    /// recorded on the student and is independent of whether a route has been assigned yet.
    /// </summary>
    public static StudentRideMode FromFlags(bool ridesAm, bool ridesPm)
    {
        if (ridesAm && ridesPm)
        {
            return StudentRideMode.Both;
        }

        if (ridesAm)
        {
            return StudentRideMode.AM;
        }

        return ridesPm ? StudentRideMode.PM : StudentRideMode.Neither;
    }

    public static StudentRideMode FromStudent(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        return FromFlags(student.RidesAm, student.RidesPm);
    }

    /// <summary>
    /// Fills in eligibility from route assignment for intake paths that carry AMRoute/PMRoute but no
    /// explicit flags — JSON seed, legacy CSV export, prep seeds. Uses the same rule as the database
    /// backfill in <c>20260907150000_StudentRideEligibilityAndSchoolYear</c>: a non-blank route means
    /// the child rides that run.
    /// </summary>
    /// <remarks>
    /// A record that already states any eligibility is left untouched, so an explicit flag from a
    /// file always wins over inference. A record stating neither is either genuinely a non-rider
    /// (blank routes, so inference agrees) or simply silent (inference is the best available answer).
    /// </remarks>
    /// <returns>True when eligibility was inferred, so callers can log the backfill.</returns>
    public static bool ApplyRouteDerivedEligibility(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (student.RidesAm || student.RidesPm)
        {
            return false;
        }

        student.RidesAm = student.AmRouteId is > 0
            || (student.AmRouteId is null && !string.IsNullOrWhiteSpace(student.AMRoute));
        student.RidesPm = student.PmRouteId is > 0
            || (student.PmRouteId is null && !string.IsNullOrWhiteSpace(student.PMRoute));
        return student.RidesAm || student.RidesPm;
    }

    /// <summary>True when the student should keep a stop on the PM mirror even if AM-only.</summary>
    public static bool RetainStopOnPmMirror(StudentRideMode mode) =>
        mode is StudentRideMode.AM or StudentRideMode.Both or StudentRideMode.Neither;

    /// <summary>True when the student should keep a stop on the AM mirror even if PM-only.</summary>
    public static bool RetainStopOnAmMirror(StudentRideMode mode) =>
        mode is StudentRideMode.PM or StudentRideMode.Both or StudentRideMode.Neither;

    /// <summary>
    /// Year-start PM mirror: assign PMRoute unless the student was already AM-only
    /// (occasional-rider stop stays on the PM proposal OrderedStudentIds only).
    /// </summary>
    public static bool ShouldAssignPmMirror(StudentRideMode priorMode) =>
        priorMode is not StudentRideMode.AM;
}
