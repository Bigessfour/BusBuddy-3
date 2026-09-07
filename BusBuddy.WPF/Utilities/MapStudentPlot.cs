using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Draws student PK / HOME pins. Policy lives in <see cref="StudentPlotLocation"/>.
/// </summary>
internal static class MapStudentPlot
{
    public static void Draw(
        Action<double, double, IEnumerable<string>?, string?> plot,
        string studentName,
        IReadOnlyList<StudentPlotPoint> points)
    {
        ArgumentNullException.ThrowIfNull(plot);
        var name = string.IsNullOrWhiteSpace(studentName) ? "Student" : studentName;
        foreach (var point in points)
        {
            var label = point.AtPickup
                ? MapMarkerLabels.ForPickup(point.PickupName)
                : MapMarkerLabels.ForHome(name);
            plot(point.Latitude, point.Longitude, new[] { name }, label);
        }
    }

    public static void Draw(
        Action<double, double, IEnumerable<string>?, string?> plot,
        Student student,
        IReadOnlyList<StudentPlotPoint> points) =>
        Draw(plot, student.StudentName ?? student.StudentNumber ?? "Student", points);
}
