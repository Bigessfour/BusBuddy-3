using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        [Required]
        [StringLength(200)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string User { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Details { get; set; }
    }
}
