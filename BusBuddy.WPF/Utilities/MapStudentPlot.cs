using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Utilities;

/// <summary>Plot a pin: coordinates, optional student names, caption, optional roster ids.</summary>
internal delegate void MapPinPlot(
    double latitude,
    double longitude,
    IEnumerable<string>? studentNames,
    string? label,
    IEnumerable<int>? studentIds);

/// <summary>
/// Draws student PK / HOME pins. Policy lives in <see cref="StudentPlotLocation"/>.
/// </summary>
internal static class MapStudentPlot
{
    public static void Draw(
        MapPinPlot plot,
        string studentName,
        IReadOnlyList<StudentPlotPoint> points,
        int? studentId = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        var name = string.IsNullOrWhiteSpace(studentName) ? "Student" : studentName;
        var ids = studentId is > 0 ? new[] { studentId.Value } : null;
        foreach (var point in points)
        {
            var label = point.AtPickup
                ? MapMarkerLabels.ForPickup(point.PickupName)
                : MapMarkerLabels.ForHome(name);
            plot(point.Latitude, point.Longitude, new[] { name }, label, ids);
        }
    }

    public static void Draw(
        MapPinPlot plot,
        Student student,
        IReadOnlyList<StudentPlotPoint> points) =>
        Draw(plot, student.StudentName ?? student.StudentNumber ?? "Student", points, student.StudentId);
}
