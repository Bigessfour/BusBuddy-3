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

    /// <summary>
    /// True when the stop has a validated GPS point. Default 0,0 is unset (non-nullable columns).
    /// US centroid is not a catalog stop.
    /// </summary>
    [NotMapped]
    public bool HasGpsCoordinates => HasValidatedCoordinates;

    [NotMapped]
    public bool HasValidatedCoordinates => LocationCoordinate.IsValidated(Latitude, Longitude);

    [NotMapped]
    public string LocationType => LocationTypes.PickupStop;

    [NotMapped]
    public bool IsDistrictFacility => true;

    [NotMapped]
    public bool SchoolYearStable => true;

    [NotMapped]
    public string CoordinateStatus => LocationTypes.ValidationStatus(HasValidatedCoordinates);

    /// <summary>Corner or Intersection. A student home is not a catalog stop unless a clerk publishes it as PickupStop.</summary>
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

    /// <summary>Leftover label. Do not use for new stops — home pickup stays on the student, not the catalog.</summary>
    public const string RuralHome = "RuralHome";

    public static IReadOnlyList<string> All { get; } = [Corner, Intersection];
}
