using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents school calendar information
/// Based on School Calendar requirements from BusBuddy Tables schema
/// Captures school dates, holidays, events like Thanksgiving Break, Christmas Break, Spring Break
/// School Year begins July 1 and ends June 30th
/// </summary>
[Table("SchoolCalendar")]
public class SchoolCalendar
{
    [Key]
    public int CalendarId { get; set; }

    [Required]
    [Display(Name = "Date")]
    public DateTime Date { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Event Type")]
    public string EventType { get; set; } = string.Empty; // School Day, Holiday, Break, Event

    [Required]
    [StringLength(100)]
    [Display(Name = "Event Name")]
    public string EventName { get; set; } = string.Empty; // Thanksgiving Break, Christmas Break, Spring Break, etc.

    [Required]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = string.Empty; // 2024-2025

    [Display(Name = "Start Date")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    [Required]
    [Display(Name = "Routes Required")]
    public bool RoutesRequired { get; set; } = true; // Whether bus routes are needed on this date

    [StringLength(200)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }
}
