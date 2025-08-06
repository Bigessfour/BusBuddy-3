using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents assignment of a vehicle to a route
/// </summary>
[Table("RouteAssignments")]
public class RouteAssignment
{
    [Key]
    public int RouteAssignmentId { get; set; }

    [Required]
    public int RouteId { get; set; }
    [ForeignKey("RouteId")]
    public Route? Route { get; set; }

    [Required]
    public int VehicleId { get; set; }
    [ForeignKey("VehicleId")]
    public Bus? Vehicle { get; set; }

    [Required]
    public DateTime AssignmentDate { get; set; } = DateTime.Today;
    [Flags]
    public enum DaysOfWeek
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64
    }

    public int? GuardianId { get; set; }
    public Guardian? Guardian { get; set; }
}
