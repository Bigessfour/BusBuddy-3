using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusBuddy.Core.Models.Trips;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents activity schedule information for Syncfusion scheduling
/// Based on Activity Schedule requirements from BusBuddy Tables schema
/// Built for Syncfusion scheduling methodology integration
/// Captures scheduled records of all Sports Trips and Activities
/// </summary>
[Table("ActivitySchedule")]
public class ActivitySchedule
{
    [Key]
    public int ActivityScheduleId { get; set; }

    [Required]
    [Display(Name = "Scheduled Date")]
    public DateTime ScheduledDate { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Trip Type")]
    public string TripType { get; set; } = string.Empty; // Sports Trip, Activity Trip

    [Required]
    [ForeignKey("ScheduledVehicle")]
    [Display(Name = "Scheduled Vehicle")]
    public int ScheduledVehicleId { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Scheduled Destination")]
    public string ScheduledDestination { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Scheduled Leave Time")]
    public TimeSpan ScheduledLeaveTime { get; set; }

    [Required]
    [Display(Name = "Scheduled Event Time")]
    public TimeSpan ScheduledEventTime { get; set; }

    [Display(Name = "Scheduled Riders")]
    public int? ScheduledRiders { get; set; }

    [Required]
    [ForeignKey("ScheduledDriver")]
    [Display(Name = "Scheduled Driver")]
    public int ScheduledDriverId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Requested By")]
    public string RequestedBy { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, In Progress, Completed, Cancelled

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } // Remove dynamic default

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy { get; set; }

    // Syncfusion Scheduler Properties
    [Display(Name = "Start Time")]
    [NotMapped]
    public DateTime StartDateTime
    {
        get => ScheduledDate.Date.Add(ScheduledLeaveTime);
        set
        {
            ScheduledDate = value.Date;
            ScheduledLeaveTime = value.TimeOfDay;
        }
    }

    [Display(Name = "End Time")]
    [NotMapped]
    public DateTime EndDateTime
    {
        get => ScheduledDate.Date.Add(ScheduledEventTime);
        set
        {
            ScheduledEventTime = value.TimeOfDay;
        }
    }

    [Display(Name = "Subject")]
    [NotMapped]
    public string Subject => $"{TripType} - {ScheduledDestination}";

    [Display(Name = "All Day")]
    [NotMapped]
    public bool IsAllDay => false;

    // Navigation properties
    public virtual Bus ScheduledVehicle { get; set; } = null!;
    public virtual Driver ScheduledDriver { get; set; } = null!;

    // Backwards-compatible alias expected by WPF views
    [NotMapped]
    public int ScheduledBusId
    {
        get => ScheduledVehicleId;
        set => ScheduledVehicleId = value;
    }

    // Student assignments for this activity schedule
    public virtual ICollection<StudentSchedule> StudentSchedules { get; set; } = new List<StudentSchedule>();

    // Link to detailed trip information
    [ForeignKey("TripEvent")]
    [Display(Name = "Trip Event")]
    public int? TripEventId { get; set; }
    public virtual TripEvent? TripEvent { get; set; }
}
