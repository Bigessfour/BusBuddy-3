using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents individual stops along a bus route
/// Extended model for detailed route management with GPS coordinates and timing
/// </summary>
[Table("RouteStops")]
public class RouteStop
{
    [Key]
    public int RouteStopId { get; set; }

    [Required]
    [ForeignKey("Route")]
    [Display(Name = "Route")]
    public int RouteId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Stop Name")]
    public string StopName { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Stop Address")]
    public string StopAddress { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,8)")]
    [Display(Name = "Latitude")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(11,8)")]
    [Display(Name = "Longitude")]
    public decimal? Longitude { get; set; }

    [Required]
    [Display(Name = "Stop Order")]
    public int StopOrder { get; set; } // Order of stop in the route

    [Required]
    [Display(Name = "Scheduled Arrival")]
    public TimeSpan ScheduledArrival { get; set; }

    [Required]
    [Display(Name = "Scheduled Departure")]
    public TimeSpan ScheduledDeparture { get; set; }

    /// <summary>
    /// General scheduled time for the stop (typically the arrival time)
    /// Used by route duration calculations and stop reordering
    /// </summary>
    [Display(Name = "Scheduled Time")]
    public TimeSpan ScheduledTime => ScheduledArrival;

    [Range(0, 60)]
    [Display(Name = "Stop Duration (minutes)")]
    public int StopDuration { get; set; } // in minutes

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Active"; // Active, Inactive, Temporary

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } // Remove dynamic default

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// Estimated Arrival Time
    /// </summary>
    public DateTime EstimatedArrivalTime { get; set; }

    /// <summary>
    /// Estimated Departure Time
    /// </summary>
    public DateTime EstimatedDepartureTime { get; set; }

    // Navigation properties
    public virtual Route? Route { get; set; }
}
