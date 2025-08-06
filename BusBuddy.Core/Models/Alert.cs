using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents an alert or notification in the BusBuddy system
    /// </summary>
    public class Alert
    {
        /// <summary>
        /// Unique identifier for the alert
        /// </summary>
        [Key]
        public int AlertId { get; set; }

        /// <summary>
        /// Type of alert (e.g., Warning, Info, Error, Emergency)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Alert message content
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level (e.g., Critical, High, Medium, Low)
        /// </summary>
        [StringLength(20)]
        public string Severity { get; set; } = "Low";

        /// <summary>
        /// When the alert was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether the alert has been acknowledged
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// Who acknowledged the alert
        /// </summary>
        [StringLength(100)]
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// When the alert was acknowledged
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// Related entity ID (e.g., VehicleId, RouteId, StudentId)
        /// </summary>
        public int? RelatedEntityId { get; set; }

        /// <summary>
        /// Type of related entity (e.g., Vehicle, Route, Student, Driver)
        /// </summary>
        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        /// <summary>
        /// Additional data or context for the alert
        /// </summary>
        [StringLength(1000)]
        public string? AdditionalData { get; set; }

        /// <summary>
        /// Whether the alert should be automatically dismissed after a certain time
        /// </summary>
        public bool AutoDismiss { get; set; }

        /// <summary>
        /// When the alert should be automatically dismissed (if AutoDismiss is true)
        /// </summary>
        public DateTime? DismissAt { get; set; }
    }
}
