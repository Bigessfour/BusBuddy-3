namespace BusBuddy.Core.Models;

/// <summary>
/// Shared rules for special-needs student transport and route matching.
/// </summary>
public static class StudentSpecialNeedsHelper
{
    public const string LegacyTransportFlag = "Special needs bus required";

    public static bool RequiresSpecialNeedsTransport(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        return student.RequiresSpecialNeedsBus
            || !string.IsNullOrWhiteSpace(student.SpecialNeeds);
    }

    public static bool IsSpecialNeedsRoute(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return route.IsSpecialNeedsRoute
            || route.RouteName.Contains("special needs", StringComparison.OrdinalIgnoreCase);
    }

    public static void SyncLegacySpecialNeedsText(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (student.RequiresSpecialNeedsBus)
        {
            if (string.IsNullOrWhiteSpace(student.SpecialNeeds))
            {
                student.SpecialNeeds = LegacyTransportFlag;
            }
        }
        else if (string.Equals(student.SpecialNeeds, LegacyTransportFlag, StringComparison.OrdinalIgnoreCase))
        {
            student.SpecialNeeds = string.Empty;
        }
    }
}
