using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models
{
    [Table("Students")]
    public class Student
    {
        [Key]
        public int StudentId { get; set; }

        [Required]
        [StringLength(20, ErrorMessage = "Student number cannot exceed 20 characters")]
        public string StudentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "Student name cannot exceed 100 characters")]
        public string StudentName { get; set; } = string.Empty;

        // Add other properties as needed
    }
}
