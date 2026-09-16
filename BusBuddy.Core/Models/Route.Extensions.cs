using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Capacity from the year-default pairing buses. Roster membership lives on
/// <c>Student.AmRouteId</c>/<c>PmRouteId</c> — do not keep an in-memory student list here.
/// </summary>
public partial class Route
{
    [NotMapped]
    public int MaxCapacity
    {
        get
        {
            var amCapacity = AMVehicle?.Capacity ?? 0;
            var pmCapacity = PMVehicle?.Capacity ?? 0;
            return Math.Max(amCapacity, pmCapacity);
        }
    }
}

/// <summary>
/// Route time slot for student assignments.
/// </summary>
public enum RouteTimeSlot
{
    AM,
    PM,
    Both
}

/// <summary>
/// Route validation result
/// </summary>
public class RouteValidationResult
{
    public bool IsValid { get; set; }
    public bool CanActivate { get; set; }
    public List<string> Issues { get; set; } = new();
    public string Summary => IsValid ? "Route is valid" : $"{Issues.Count} issue(s) found";
}
