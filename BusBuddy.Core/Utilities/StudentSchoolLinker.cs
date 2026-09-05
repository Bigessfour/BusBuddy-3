using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Keeps <see cref="Student.School"/> and <see cref="Student.DestinationId"/> aligned with the destinations catalog.
/// </summary>
public static class StudentSchoolLinker
{
    /// <summary>
    /// Sets <see cref="Student.DestinationId"/> from <see cref="Student.School"/> when a catalog match exists.
    /// When only <see cref="Student.DestinationId"/> is set, back-fills <see cref="Student.School"/> from the catalog.
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

        if (!string.IsNullOrWhiteSpace(student.School))
        {
            var match = catalog.FirstOrDefault(
                d => string.Equals(d.Name, student.School, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                student.DestinationId = match.DestinationId;
                student.School = match.Name;
                return;
            }
        }

        if (student.DestinationId is > 0)
        {
            var byId = catalog.FirstOrDefault(d => d.DestinationId == student.DestinationId);
            if (byId is not null)
            {
                student.School = byId.Name;
            }
        }
    }
}
