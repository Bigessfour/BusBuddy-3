using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents a destination for activities and field trips
    /// Enhanced destination management for sports and activities scheduling
    /// </summary>
    [Table("Destinations")]
    public class Destination
    {
        /// <summary>
        /// Primary key identifier
        /// </summary>
        [Key]
        public int DestinationId { get; set; }

        /// <summary>
        /// Name of the destination (e.g., "Natural History Museum", "Central High School")
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full street address
        /// </summary>
        [Required]
        [StringLength(300)]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// City name
        /// </summary>
        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// State abbreviation (e.g., "CA", "NY")
        /// </summary>
        [Required]
        [StringLength(2)]
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// ZIP/Postal code
        /// </summary>
        [Required]
        [StringLength(10)]
        public string ZipCode { get; set; } = string.Empty;

        /// <summary>
        /// Primary contact person at the destination
        /// </summary>
        [StringLength(100)]
        public string? ContactName { get; set; }

        /// <summary>
        /// Contact phone number
        /// </summary>
        [StringLength(20)]
        public string? ContactPhone { get; set; }

        /// <summary>
        /// Contact email address
        /// </summary>
        [StringLength(100)]
        [EmailAddress]
        public string? ContactEmail { get; set; }

        /// <summary>
        /// Type of destination for categorization
        /// </summary>
        [Required]
        [StringLength(50)]
        public string DestinationType { get; set; } = "Field Trip";

        /// <summary>
        /// Maximum number of students the venue can accommodate
        /// </summary>
        public int? MaxCapacity { get; set; }

        /// <summary>
        /// Special requirements or notes (accessibility, equipment, etc.)
        /// </summary>
        [StringLength(500)]
        public string? SpecialRequirements { get; set; }

        /// <summary>
        /// GPS Latitude coordinate for routing optimization
        /// </summary>
        [Column(TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        /// <summary>
        /// GPS Longitude coordinate for routing optimization
        /// </summary>
        [Column(TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        /// <summary>
        /// Whether this destination is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Soft delete flag
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// When this destination was created
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this destination was last updated
        /// </summary>
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who created this destination
        /// </summary>
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Who last updated this destination
        /// </summary>
        [StringLength(100)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Navigation property: Activities that use this destination
        /// </summary>
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

        /// <summary>
        /// Full formatted address for display
        /// </summary>
        [NotMapped]
        public string FullAddress => $"{Address}, {City}, {State} {ZipCode}";

        /// <summary>
        /// Display name with type for UI
        /// </summary>
        [NotMapped]
        public string DisplayName => $"{Name} ({DestinationType})";

        /// <summary>
        /// Whether GPS coordinates are available
        /// </summary>
        [NotMapped]
        public bool HasGpsCoordinates => Latitude.HasValue && Longitude.HasValue;
    }

    /// <summary>
    /// Common destination types for validation and categorization
    /// </summary>
    public static class DestinationTypes
    {
        public const string FieldTrip = "Field Trip";
        public const string SportsEvent = "Sports Event";
        public const string AcademicCompetition = "Academic Competition";
        public const string CommunityService = "Community Service";
        public const string BandCompetition = "Band Competition";
        public const string DramaPerformance = "Drama Performance";
        public const string CareerFair = "Career Fair";
        public const string CulturalExchange = "Cultural Exchange";
        public const string VolunteerWork = "Volunteer Work";
        public const string GraduationCeremony = "Graduation Ceremony";
        public const string Other = "Other";

        public static readonly string[] AllTypes = {
            FieldTrip, SportsEvent, AcademicCompetition, CommunityService,
            BandCompetition, DramaPerformance, CareerFair, CulturalExchange,
            VolunteerWork, GraduationCeremony, Other
        };
    }
}
