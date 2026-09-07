using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Same-day “not riding” for one student on one published route.
/// Does not delete the stop, the year assignment, or the student.
/// Named as an exception in the domain spec (not a .NET Exception type).
/// </summary>
[Table("RouteRiderExceptions")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Domain term from specs/routes.md: rider exception.")]
public class RouteRiderException
{
    [Key]
    public int RouteRiderExceptionId { get; set; }

    [Required]
    public int RouteId { get; set; }

    [ForeignKey(nameof(RouteId))]
    public Route? Route { get; set; }

    [Required]
    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public Student? Student { get; set; }

    [Required]
    public DateTime ExceptionDate { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
