using System.Text;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>Activity roster CSV. Not activity CRUD or PDF generation.</summary>
public static class ActivityCsvExporter
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ActivityCsvExporter));

    public static string ToCsv(IReadOnlyList<Activity> activities)
    {
        Logger.Information("Exporting {Count} activities to CSV", activities.Count);

        var sb = new StringBuilder();
        sb.AppendLine("ActivityId,Date,Type,Description,Destination,LeaveTime,ReturnTime,Driver,Vehicle,Route,RequestedBy,Status,Passengers,Notes");

        foreach (var activity in activities)
        {
            sb.AppendLine(CsvLine.Join(
                activity.ActivityId,
                activity.Date,
                activity.ActivityType,
                activity.Description,
                activity.Destination,
                activity.LeaveTime,
                activity.ReturnTime,
                activity.Driver?.FullName,
                activity.AssignedVehicle?.BusNumber,
                activity.Route?.RouteName,
                activity.RequestedBy,
                activity.Status,
                activity.ExpectedPassengers,
                activity.Notes));
        }

        return sb.ToString();
    }
}
