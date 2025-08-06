using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

// Extended Route model for route building functionality
// Adds collection properties and computed values for route construction
public partial class Route
{
    #region Route Building Extensions

    /// <summary>
    /// Collection of students assigned to this route. Computed from Student.AMRoute and Student.PMRoute.
    /// </summary>
    [NotMapped]
    public ObservableCollection<Student> AssignedStudents { get; set; } = new();

    /// <summary>
    /// Collection of route stops in order.
    /// </summary>
    [NotMapped]
    public ObservableCollection<RouteStop> RouteStops { get; set; } = new();

    /// <summary>
    /// Current capacity utilization (0.0 to 1.0).
    /// </summary>
    [NotMapped]
    public double UtilizationRate => MaxCapacity > 0 ? (double)AssignedStudents.Count / MaxCapacity : 0.0;

    /// <summary>
    /// Maximum capacity based on assigned vehicle.
    /// </summary>
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

    /// <summary>
    /// Available capacity for new student assignments.
    /// </summary>
    [NotMapped]
    public int AvailableCapacity => Math.Max(0, MaxCapacity - AssignedStudents.Count);

    /// <summary>
    /// Whether the route is at or over capacity
    /// </summary>
    [NotMapped]
    public bool IsAtCapacity => AssignedStudents.Count >= MaxCapacity;

    /// <summary>
    /// Route building status
    /// </summary>
    [NotMapped]
    public RouteStatus BuildingStatus { get; set; } = RouteStatus.Draft;

    /// <summary>
    /// Total estimated time for the route including all stops
    /// </summary>
    [NotMapped]
    public TimeSpan EstimatedTotalTime
    {
        get
        {
            if (!RouteStops.Any())
            {
                return TimeSpan.Zero;
            }

            var firstStop = RouteStops.OrderBy(s => s.StopOrder).First().ScheduledTime;
            var lastStop = RouteStops.OrderBy(s => s.StopOrder).Last().ScheduledTime;

            return lastStop.Subtract(firstStop);
        }
    }

    /// <summary>
    /// Route efficiency score (0.0 to 1.0)
    /// Based on utilization, distance per student, and time efficiency
    /// </summary>
    [NotMapped]
    public double EfficiencyScore
    {
        get
        {
            if (AssignedStudents.Count == 0)
            {
                return 0.0;
            }

            var utilizationScore = UtilizationRate * 0.4; // 40% weight
            var distanceEfficiency = Distance > 0 ? Math.Min(1.0, 10.0 / (double)Distance * AssignedStudents.Count) * 0.3 : 0.0; // 30% weight
            var timeEfficiency = EstimatedDuration > 0 ? Math.Min(1.0, 60.0 / EstimatedDuration.Value) * 0.3 : 0.0; // 30% weight

            return Math.Min(1.0, utilizationScore + distanceEfficiency + timeEfficiency);
        }
    }

    /// <summary>
    /// Display name for UI binding
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{SafeRouteName} ({AssignedStudents.Count}/{MaxCapacity} students)";

    /// <summary>
    /// Status indicator for UI
    /// </summary>
    [NotMapped]
    public string StatusIndicator
    {
        get
        {
            return BuildingStatus switch
            {
                RouteStatus.Draft => "📝 Draft",
                RouteStatus.InProgress => "🔄 Building",
                RouteStatus.Review => "👀 Review",
                RouteStatus.Active => "✅ Active",
                RouteStatus.Inactive => "⏸️ Inactive",
                RouteStatus.Archived => "📦 Archived",
                _ => "❓ Unknown"
            };
        }
    }

    #endregion

    #region Route Building Methods

    /// <summary>
    /// Add a student to this route with validation
    /// </summary>
    public bool TryAddStudent(Student student, RouteTimeSlot timeSlot = RouteTimeSlot.AM)
    {
        if (student == null || IsAtCapacity)
        {
            return false;
        }

        // Check if student is already assigned
        if (AssignedStudents.Any(s => s.StudentId == student.StudentId))
        {
            return false;
        }

        // Add to collection and update student properties
        AssignedStudents.Add(student);

        if (timeSlot == RouteTimeSlot.AM)
        {
            student.AMRoute = this.RouteName;
        }
        else
        {
            student.PMRoute = this.RouteName;
        }

        OnPropertyChanged(nameof(AssignedStudents));
        OnPropertyChanged(nameof(UtilizationRate));
        OnPropertyChanged(nameof(AvailableCapacity));
        OnPropertyChanged(nameof(IsAtCapacity));
        OnPropertyChanged(nameof(DisplayName));

        return true;
    }

    /// <summary>
    /// Remove a student from this route
    /// </summary>
    public bool TryRemoveStudent(Student student)
    {
        if (student == null)
        {
            return false;
        }

        var removed = AssignedStudents.Remove(student);
        if (removed)
        {
            // Clear student route assignments
            if (student.AMRoute == this.RouteName)
            {
                student.AMRoute = null;
            }

            if (student.PMRoute == this.RouteName)
            {
                student.PMRoute = null;
            }

            OnPropertyChanged(nameof(AssignedStudents));
            OnPropertyChanged(nameof(UtilizationRate));
            OnPropertyChanged(nameof(AvailableCapacity));
            OnPropertyChanged(nameof(IsAtCapacity));
            OnPropertyChanged(nameof(DisplayName));
        }

        return removed;
    }

    /// <summary>
    /// Add a route stop in the correct order
    /// </summary>
    public void AddRouteStop(RouteStop stop)
    {
        if (stop == null)
        {
            return;
        }

        stop.RouteId = this.RouteId;
        stop.StopOrder = RouteStops.Count + 1;

        RouteStops.Add(stop);
        OnPropertyChanged(nameof(RouteStops));
        OnPropertyChanged(nameof(EstimatedTotalTime));
    }

    /// <summary>
    /// Reorder route stops
    /// </summary>
    public void ReorderStops()
    {
        var orderedStops = RouteStops.OrderBy(s => s.ScheduledTime).ToList();
        RouteStops.Clear();

        for (int i = 0; i < orderedStops.Count; i++)
        {
            orderedStops[i].StopOrder = i + 1;
            RouteStops.Add(orderedStops[i]);
        }

        OnPropertyChanged(nameof(RouteStops));
        OnPropertyChanged(nameof(EstimatedTotalTime));
    }

    /// <summary>
    /// Validate route is ready for activation
    /// </summary>
    public RouteValidationResult ValidateForActivation()
    {
        var issues = new List<string>();

        if (!HasAMAssignment && !HasPMAssignment)
        {
            issues.Add("Route must have at least one vehicle and driver assignment");
        }

        if (AssignedStudents.Count == 0)
        {
            issues.Add("Route must have at least one assigned student");
        }

        if (AssignedStudents.Count > MaxCapacity)
        {
            issues.Add($"Route exceeds capacity: {AssignedStudents.Count}/{MaxCapacity} students");
        }

        if (!RouteStops.Any())
        {
            issues.Add("Route must have at least one stop");
        }

        return new RouteValidationResult
        {
            IsValid = !issues.Any(),
            Issues = issues,
            CanActivate = !issues.Any() || issues.All(i => i.Contains("stop")) // Can activate without stops for now
        };
    }

    #endregion
}

/// <summary>
/// Route building status enumeration
/// </summary>
public enum RouteStatus
{
    Draft,
    InProgress,
    Review,
    Active,
    Inactive,
    Archived
}

/// <summary>
/// Route time slot for student assignments
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
    public string Summary => IsValid ? "✅ Route is valid" : $"❌ {Issues.Count} issue(s) found";
}

// Route Stop model

