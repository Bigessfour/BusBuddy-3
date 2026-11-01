using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Shared driver+bus duty log for a session (#112). One child collection — not duplicated on Driver and Bus.
/// Defect notes also feed Fleet Maintenance; Fuel type does not store gallons.
/// </summary>
[Table("DutyEvents")]
public class DutyEvent
{
    public const int MaxNotesLength = 1000;

    [Key]
    public int DutyEventId { get; set; }

    [Required]
    [ForeignKey(nameof(Driver))]
    [Display(Name = "Driver")]
    public int DriverId { get; set; }

    [Required]
    [ForeignKey(nameof(Bus))]
    [Display(Name = "Bus")]
    public int BusId { get; set; }

    [Required]
    [Display(Name = "Session")]
    public DutySession Session { get; set; } = DutySession.AM;

    [Required]
    [Display(Name = "Event Type")]
    public DutyEventType EventType { get; set; } = DutyEventType.PreTrip;

    [Required]
    [Display(Name = "Evented At")]
    public DateTime EventedAt { get; set; } = DateTime.UtcNow;

    [StringLength(MaxNotesLength)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    /// <summary>Optional link to an existing Fuel row. Never stores gallons here.</summary>
    [ForeignKey(nameof(Fuel))]
    [Display(Name = "Fuel Record")]
    public int? FuelId { get; set; }

    /// <summary>Optional odometer for defect → maintenance handoff.</summary>
    [Display(Name = "Odometer")]
    public int? OdometerReading { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public virtual Driver Driver { get; set; } = null!;
    public virtual Bus Bus { get; set; } = null!;
    public virtual Fuel? Fuel { get; set; }
}
