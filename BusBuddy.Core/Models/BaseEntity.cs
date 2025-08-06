using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Base entity class providing common properties for all entities
    /// Includes audit fields, concurrency control, and soft delete functionality
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// Primary key identifier
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Timestamp when the entity was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the entity was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Row version for optimistic concurrency control
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Soft delete flag — entity is marked as deleted but not physically removed
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Timestamp when the entity was soft deleted (if applicable)
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// User who created this entity
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated this entity
        /// </summary>
        public string? UpdatedBy { get; set; }
    }
}
