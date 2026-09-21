using System.Text;

namespace BusBuddy.Core.Services;

/// <summary>Clerk-facing copy when <see cref="RouteService.DeleteRouteAsync"/> soft-retires instead of hard-deleting.</summary>
public static class RouteDeleteClerkMessages
{
    public static string BuildRetiredMessage(
        string routeName,
        int scheduleCount,
        int studentAssignmentCount,
        int tripEventCount)
    {
        var name = string.IsNullOrWhiteSpace(routeName) ? "This route" : $"\"{routeName.Trim()}\"";
        var sb = new StringBuilder();
        sb.AppendLine($"{name} was retired and removed from the route list.");
        sb.AppendLine();
        sb.AppendLine("It cannot be permanently deleted yet because:");
        if (studentAssignmentCount > 0)
        {
            sb.AppendLine($"• {studentAssignmentCount} student(s) still have this route on AM or PM.");
        }

        if (scheduleCount > 0)
        {
            sb.AppendLine($"• {scheduleCount} schedule row(s) still reference it.");
        }

        if (tripEventCount > 0)
        {
            sb.AppendLine($"• {tripEventCount} trip event(s) still reference it.");
        }

        sb.AppendLine();
        sb.AppendLine("What to do:");
        if (studentAssignmentCount > 0)
        {
            sb.AppendLine("• Open Manage Route and remove riders or assign them to another route.");
        }

        sb.AppendLine("• Assigned bus and driver do not block delete — only students, schedules, and trips do.");
        if (scheduleCount > 0 || tripEventCount > 0)
        {
            sb.AppendLine("• Clear schedule and trip links to this route before permanent delete.");
        }

        sb.AppendLine();
        sb.AppendLine("Turn on \"Show retired routes\" on the Routes screen to see inactive rows again.");
        return sb.ToString().TrimEnd();
    }
}
