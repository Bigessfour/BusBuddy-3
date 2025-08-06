using System;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents a data integrity issue found during validation
    /// </summary>
    public class DataIntegrityIssue
    {
        /// <summary>
        /// Type of entity where the issue was found (Route, Activity, Student, Driver, Vehicle, System)
        /// </summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// ID of the specific entity with the issue
        /// </summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>
        /// Category of the issue (Missing Required Data, Invalid Data Format, Business Logic Violation, etc.)
        /// </summary>
        public string IssueType { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of the issue
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Severity level: Critical, High, Medium, Low
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// When the issue was detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Suggested action to resolve the issue
        /// </summary>
        public string SuggestedAction { get; set; } = string.Empty;

        /// <summary>
        /// Whether this issue blocks critical operations
        /// </summary>
        public bool IsBlocking => Severity == "Critical";

        /// <summary>
        /// Get display text for the issue
        /// </summary>
        public string DisplayText => $"[{Severity}] {EntityType} {EntityId}: {Description}";
    }
}
