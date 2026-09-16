namespace BusBuddy.Core.Models;

/// <summary>
/// Outcome of <c>IDestinationService.DeleteSchoolAsync</c>. Unused campuses are removed; campuses
/// still referenced by students, transfers, activities, or trips are retired (<c>IsActive=false</c>).
/// </summary>
public enum SchoolDeleteResult
{
    NotFound = 0,
    Deleted = 1,
    Retired = 2
}
