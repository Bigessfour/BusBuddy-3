using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Entity for storing AI analysis results and insights from Grok API
/// Supports maintenance predictions, route optimizations, and operational insights
/// </summary>
[Table("AIInsights")]
public class AIInsight
{
    [Key]
    public int InsightId { get; set; }

    /// <summary>
    /// Type of insight: Maintenance, Route, Performance, Security, UI, etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string InsightType { get; set; } = string.Empty;

    /// <summary>
    /// Priority level: Critical, High, Medium, Low
    /// </summary>
    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Vehicle, Route, Driver, or System identifier this insight relates to
    /// </summary>
    [MaxLength(100)]
    public string? EntityReference { get; set; }

    /// <summary>
    /// JSON-formatted insight details from Grok analysis
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string InsightDetails { get; set; } = string.Empty;

    /// <summary>
    /// Brief summary of the insight for dashboard display
    /// </summary>
    [MaxLength(500)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Recommended actions based on AI analysis
    /// </summary>
    [MaxLength(1000)]
    public string? RecommendedActions { get; set; }

    /// <summary>
    /// Confidence score from AI analysis (0.0 to 1.0)
    /// </summary>
    [Column(TypeName = "decimal(4,3)")]
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Source system that generated the insight
    /// </summary>
    [MaxLength(50)]
    public string Source { get; set; } = "Grok-4";

    /// <summary>
    /// Status: New, Reviewed, InProgress, Resolved, Dismissed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "New";

    /// <summary>
    /// When the insight was generated
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the insight was last updated
    /// </summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// User who created or imported the insight
    /// </summary>
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the insight
    /// </summary>
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// When this insight expires or should be re-evaluated
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Cost savings estimate if recommendation is implemented
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedSavings { get; set; }

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Navigation property - related vehicle if insight is vehicle-specific
    /// </summary>
    public virtual Bus? Vehicle { get; set; }
    public int? VehicleId { get; set; }

    /// <summary>
    /// Navigation property - related route if insight is route-specific
    /// </summary>
    public virtual Route? Route { get; set; }
    public int? RouteId { get; set; }

    /// <summary>
    /// Navigation property - related driver if insight is driver-specific
    /// </summary>
    public virtual Driver? Driver { get; set; }
    public int? DriverId { get; set; }
}
