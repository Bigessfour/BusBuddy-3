using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.WPF.Models
{
    public class StudentViewModel
    {
        public int StudentId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Grade")]
        [Range(0, 12, ErrorMessage = "Grade must be between 0 and 12")]
        public int Grade { get; set; }

        [Display(Name = "Grade Display")]
        public string GradeDisplay { get; set; } = string.Empty;

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Date of Birth")]
        public string DateOfBirthString
        {
            get => DateOfBirth?.ToString("MM/dd/yyyy") ?? string.Empty;
            set
            {
                if (DateTime.TryParse(value, out DateTime result))
                {
                    DateOfBirth = result;
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    DateOfBirth = null;
                }
            }
        }

        [Display(Name = "Student ID")]
        public string StudentNumber { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "City")]
        public string City { get; set; } = string.Empty;

        [Display(Name = "State")]
        [StringLength(2, ErrorMessage = "State must be a 2-letter code")]
        public string State { get; set; } = string.Empty;

        [Display(Name = "Zip Code")]
        [StringLength(10, ErrorMessage = "Zip code cannot exceed 10 characters")]
        public string ZipCode { get; set; } = string.Empty;

        [Display(Name = "Formatted Address")]
        public string AddressFormatted { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Parent Phone")]
        public string ParentPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Parent Email")]
        public string ParentEmail { get; set; } = string.Empty;

        [Display(Name = "Parent/Guardian Name")]
        public string ParentName { get; set; } = string.Empty;

        public int? RouteId { get; set; }

        [Display(Name = "Route")]
        public string RouteName { get; set; } = string.Empty;

        [Display(Name = "Special Notes")]
        public string SpecialNotes { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
