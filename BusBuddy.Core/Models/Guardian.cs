using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents a guardian for a student/family in BusBuddy
    /// </summary>
    public class Guardian
    {
        [Key]
        [NotMapped]
        public int Id
        {
            get => GuardianId;
            set => GuardianId = value;
        }

        public int GuardianId { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string? Notes { get; set; }

        [Required]
        public int FamilyId { get; set; }
        public Family? Family { get; set; }
    }
}
