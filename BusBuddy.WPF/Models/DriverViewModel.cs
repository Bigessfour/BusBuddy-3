using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.WPF.Models
{
    public class DriverViewModel
    {
        public int DriverId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Display(Name = "Full Name with ID")]
        public string FullNameWithId { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Phone")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "License Number")]
        public string LicenseNumber { get; set; } = string.Empty;

        [Display(Name = "License Expiry")]
        public string LicenseExpiryDateFormatted { get; set; } = string.Empty;

        [Display(Name = "License Status")]
        public string LicenseStatusDisplay { get; set; } = "Unknown";

        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Years Experience")]
        [Range(0, 50, ErrorMessage = "Experience must be between 0 and 50 years")]
        public int? YearsOfExperience { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;
    }
}
