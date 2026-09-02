using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// District pickup stop catalog — shared boarding location (corner, intersection, rural home).
/// Students reference <see cref="PickupStopId"/>; route generation dedupes stops by this id.
/// </summary>
[Table("PickupStops")]
public class PickupStop
{
    [Key]
    public int PickupStopId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Stop Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Column(TypeName = "decimal(10,8)")]
    public decimal Latitude { get; set; }

    [Column(TypeName = "decimal(11,8)")]
    public decimal Longitude { get; set; }

    /// <summary>Corner, Intersection, RuralHome, or other clerk label.</summary>
    [Required]
    [StringLength(20)]
    public string StopType { get; set; } = PickupStopTypes.Corner;

    public bool Active { get; set; } = true;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}

public static class PickupStopTypes
{
    public const string Corner = "Corner";
    public const string Intersection = "Intersection";
    public const string RuralHome = "RuralHome";

    public static IReadOnlyList<string> All { get; } = [Corner, Intersection, RuralHome];
}
