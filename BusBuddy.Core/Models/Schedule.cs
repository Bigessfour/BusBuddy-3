using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a scheduled bus operation for daily routes and sports/activity trips
/// Unified model for all scheduling needs including regular routes and sports trips
/// Extended model for enhanced scheduling functionality with Syncfusion components
/// </summary>
[Table("Schedules")]
public class Schedule
{
    [Key]
    public int ScheduleId { get; set; }

    [Required]
    [ForeignKey("Bus")]
    [Display(Name = "Bus")]
    [Column("VehicleId")] // Map to the actual VehicleId column in database
    public int BusId { get; set; }

    [Required]
    [ForeignKey("Route")]
    [Display(Name = "Route")]
    public int RouteId { get; set; }

    [Required]
    [ForeignKey("Driver")]
    [Display(Name = "Driver")]
    public int DriverId { get; set; }

    [Required]
    [Display(Name = "Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Required]
    [Display(Name = "Arrival Time")]
    public DateTime ArrivalTime { get; set; }

    [Required]
    [Display(Name = "Schedule Date")]
    public DateTime ScheduleDate { get; set; }

    [StringLength(50)]
    [Display(Name = "Sports Category")]
    public string? SportsCategory { get; set; } // For sports trips: "Volleyball", "Junior High Volleyball", "Football", "Junior High Football", "Softball", "Cheer", "Activity" (for non-sports)

    [StringLength(200)]
    [Display(Name = "Opponent")]
    public string? Opponent { get; set; } // For sports trips: opponent team name; for activities: description

    [StringLength(200)]
    [Display(Name = "Location")]
    public string? Location { get; set; } // Full location description (can be parsed for home/away)

    [StringLength(100)]
    [Display(Name = "Destination Town")]
    public string? DestinationTown { get; set; } // Extracted town/city for away games

    [Display(Name = "Depart Time")]
    public TimeSpan? DepartTime { get; set; } // Time to depart from school

    [Display(Name = "Scheduled Time")]
    public TimeSpan? ScheduledTime { get; set; } // Game/event start time

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled, Delayed

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    // Navigation properties
    public virtual Bus Bus { get; set; } = null!;
    public virtual Route Route { get; set; } = null!;
    public virtual Driver Driver { get; set; } = null!;

    // Student assignments for this schedule
    public virtual ICollection<StudentSchedule> StudentSchedules { get; set; } = new List<StudentSchedule>();

    // Computed properties for sports trip functionality
    [NotMapped]
    public bool IsSportsTrip => !string.IsNullOrEmpty(SportsCategory) && SportsCategory != "Activity";

    [NotMapped]
    public bool IsHomeGame => !string.IsNullOrEmpty(Location) && Location.Contains("home", StringComparison.OrdinalIgnoreCase);

    [NotMapped]
    public bool IsAwayGame => !string.IsNullOrEmpty(Location) && !IsHomeGame;

    [NotMapped]
    public string DisplayTitle => IsSportsTrip ?
        $"{SportsCategory} vs {Opponent ?? "TBD"}" :
        $"Route {RouteId} - {Bus?.BusNumber ?? "TBD"}";

    [NotMapped]
    public string EventType => IsSportsTrip ? "Sports" : "Route";
}
