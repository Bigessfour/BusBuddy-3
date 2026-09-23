using System.Text;

namespace BusBuddy.Core.Services;

/// <summary>Clerk-facing copy when <see cref="RouteService.DeleteRouteAsync"/> retires a route.</summary>
public static class RouteDeleteClerkMessages
{
    public static string BuildRetiredMessage(
        string routeName,
        int scheduleCount,
        int studentAssignmentCount,
        int tripEventCount,
        int stopCount)
    {
        var name = string.IsNullOrWhiteSpace(routeName) ? "This route" : $"\"{routeName.Trim()}\"";
        var sb = new StringBuilder();
        sb.AppendLine($"{name} was retired and hidden from the route list.");

        var kept = new List<string>();
        if (studentAssignmentCount > 0)
        {
            kept.Add($"• {studentAssignmentCount} student(s) still have this route on AM or PM.");
        }

        if (stopCount > 0)
        {
            kept.Add($"• {stopCount} published stop(s) stay on the route.");
        }

        if (scheduleCount > 0)
        {
            kept.Add($"• {scheduleCount} schedule row(s) still reference it.");
        }

        if (tripEventCount > 0)
        {
            kept.Add($"• {tripEventCount} trip event(s) still reference it.");
        }

        if (kept.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("These stay with the route:");
            foreach (var line in kept)
            {
                sb.AppendLine(line);
            }
        }

        sb.AppendLine();
        sb.AppendLine("The assigned bus and driver stay on this route.");
        sb.AppendLine("Turn on \"Show retired routes\" on the Routes screen to see inactive rows again.");
        return sb.ToString().TrimEnd();
    }
}
