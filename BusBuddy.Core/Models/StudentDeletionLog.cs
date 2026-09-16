using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Operational record of a clerk-confirmed student deletion. specs/students.md: log student id,
/// student number, reason, optional note, and related-assignment counts — not name/address/guardian.
/// There is no FK to Students; the roster row is already gone.
/// </summary>
[Table("StudentDeletionLogs")]
public class StudentDeletionLog
{
    [Key]
    public int StudentDeletionLogId { get; set; }

    public int StudentId { get; set; }

    [StringLength(20)]
    public string? StudentNumber { get; set; }

    [StringLength(9)]
    public string? SchoolYear { get; set; }

    [Required]
    [StringLength(32)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Notes { get; set; }

    public bool WasActive { get; set; }

    public int ScheduleCount { get; set; }

    public int TransferCount { get; set; }

    public int RiderExceptionCount { get; set; }

    public DateTime DeletedUtc { get; set; } = DateTime.UtcNow;
}
