using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents the relationship between students and scheduled trips
/// Enables assignment of students to specific scheduled trips/activities
/// Bridge table for many-to-many relationship between Students and Schedules
/// </summary>
[Table("StudentSchedules")]
public class StudentSchedule
{
    [Key]
    public int StudentScheduleId { get; set; }

    [Required]
    [ForeignKey("Student")]
    [Display(Name = "Student")]
    public int StudentId { get; set; }

    [ForeignKey("Schedule")]
    [Display(Name = "Schedule")]
    public int? ScheduleId { get; set; }

    [ForeignKey("ActivitySchedule")]
    [Display(Name = "Activity Schedule")]
    public int? ActivityScheduleId { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Assignment Type")]
    public string AssignmentType { get; set; } = "Regular"; // Regular, Activity, SportTrip

    [StringLength(100)]
    [Display(Name = "Pickup Location")]
    public string? PickupLocation { get; set; }

    [StringLength(100)]
    [Display(Name = "Dropoff Location")]
    public string? DropoffLocation { get; set; }

    [Display(Name = "Confirmed")]
    public bool Confirmed { get; set; } = false;

    [Display(Name = "Attended")]
    public bool Attended { get; set; } = false;

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual Schedule? Schedule { get; set; }
    public virtual ActivitySchedule? ActivitySchedule { get; set; }
}
