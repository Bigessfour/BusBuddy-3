using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Inter-district (or school-to-school) transfer: timed pickup/dropoff between two school destinations.
/// Used to plan home→school routes when a student attends a school other than their neighborhood campus.
/// </summary>
[Table("StudentSchoolTransfers")]
public class StudentSchoolTransfer
{
    [Key]
    public int TransferId { get; set; }

    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public Student? Student { get; set; }

    /// <summary>Sending / morning-origin school (often neighborhood school or home district).</summary>
    public int FromDestinationId { get; set; }

    [ForeignKey(nameof(FromDestinationId))]
    public Destination? FromDestination { get; set; }

    /// <summary>Receiving school the student attends.</summary>
    public int ToDestinationId { get; set; }

    [ForeignKey(nameof(ToDestinationId))]
    public Destination? ToDestination { get; set; }

    /// <summary>Scheduled pickup time (local).</summary>
    public TimeSpan? PickupTime { get; set; }

    /// <summary>Scheduled dropoff time (local).</summary>
    public TimeSpan? DropoffTime { get; set; }

    [StringLength(300)]
    public string? PickupAddress { get; set; }

    [StringLength(300)]
    public string? DropoffAddress { get; set; }

    [Column(TypeName = "decimal(10,8)")]
    public decimal? PickupLatitude { get; set; }

    [Column(TypeName = "decimal(11,8)")]
    public decimal? PickupLongitude { get; set; }

    [Column(TypeName = "decimal(10,8)")]
    public decimal? DropoffLatitude { get; set; }

    [Column(TypeName = "decimal(11,8)")]
    public decimal? DropoffLongitude { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}
