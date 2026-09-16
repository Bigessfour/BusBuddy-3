using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Keeps <see cref="Student.School"/> and <see cref="Student.DestinationId"/> aligned with the destinations catalog.
/// </summary>
/// <remarks>
/// specs/students.md treats the school as a destination reference, so <see cref="Student.DestinationId"/>
/// is the source of truth and <see cref="Student.School"/> is a display mirror. The string is still
/// honoured as an *input* when no FK is set, which is what keeps pre-<c>DestinationId</c> rows and CSV
/// imports working.
/// </remarks>
public static class StudentSchoolLinker
{
    /// <summary>
    /// Reconciles the school FK and its display mirror. When <see cref="Student.DestinationId"/> is set
    /// it wins and <see cref="Student.School"/> is rewritten from the catalog; when it is not, a matching
    /// <see cref="Student.School"/> string resolves the FK. An unmatched string is left alone so legacy
    /// free-text rows survive a round-trip.
    /// </summary>
    public static void SyncDestinationFromSchoolName(Student student, IReadOnlyList<Destination> schools)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (schools is null || schools.Count == 0)
        {
            return;
        }

        var catalog = schools.Where(s => s.DestinationId > 0).ToList();
        if (catalog.Count == 0)
        {
            return;
        }

        if (student.DestinationId is > 0)
        {
            var byId = catalog.FirstOrDefault(d => d.DestinationId == student.DestinationId);
            if (byId is not null)
            {
                student.School = byId.Name;
                return;
            }

            // Catalog is often active-schools-only. A retired campus is still a valid assignment
            // (specs/locations.md: soft-retire because students still reference it). Leave the FK.
            return;
        }

        if (!string.IsNullOrWhiteSpace(student.School))
        {
            var match = catalog.FirstOrDefault(
                d => string.Equals(d.Name, student.School, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                student.DestinationId = match.DestinationId;
                student.School = match.Name;
            }
        }
    }

    /// <summary>
    /// Refreshes the <see cref="Student.School"/> display mirror from a loaded
    /// <see cref="Student.Destination"/> navigation. No-op when the navigation was not included or the
    /// row predates <c>DestinationId</c>, so the legacy string stays visible.
    /// </summary>
    public static void HydrateSchoolName(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (student.Destination is { } destination && !string.IsNullOrWhiteSpace(destination.Name))
        {
            student.School = destination.Name;
        }
    }

    /// <summary>Bulk form of <see cref="HydrateSchoolName"/> for roster reads.</summary>
    public static void HydrateSchoolNames(IEnumerable<Student> students)
    {
        ArgumentNullException.ThrowIfNull(students);
        foreach (var student in students)
        {
            HydrateSchoolName(student);
        }
    }
}
