using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Per-driver training / compliance record (sub-module of Drivers).
/// Tracks Colorado CDE matrix items (background, CDL medical, annual tests, ELDT, etc.).
/// </summary>
[Table("DriverTrainingRecords")]
public class DriverTrainingRecord
{
    [Key]
    public int TrainingRecordId { get; set; }

    public int DriverId { get; set; }

    [ForeignKey(nameof(DriverId))]
    public Driver? Driver { get; set; }

    /// <summary>Code from <see cref="CdeDriverTrainingCodes"/>.</summary>
    [Required]
    [StringLength(80)]
    public string RequirementCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string RequirementName { get; set; } = string.Empty;

    public DateTime? CompletedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool IsApplicable { get; set; } = true;

    [StringLength(100)]
    public string? CertificateOrReference { get; set; }

    [StringLength(100)]
    public string? ProviderOrInstructor { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [NotMapped]
    public bool IsComplete => CompletedDate.HasValue;

    [NotMapped]
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

    [NotMapped]
    public bool IsExpiringSoon =>
        ExpiryDate.HasValue &&
        !IsExpired &&
        ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);

    [NotMapped]
    public string StatusLabel =>
        !IsApplicable ? "N/A" :
        !IsComplete ? "Missing" :
        IsExpired ? "Expired" :
        IsExpiringSoon ? "Expiring Soon" :
        "Current";
}
