using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.WPF.Models
{
    public class RouteViewModel
    {
        public int RouteId { get; set; }

        [Required(ErrorMessage = "Route number is required")]
        [Display(Name = "Route #")]
        public string RouteNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Route name is required")]
        [Display(Name = "Name")]
        [StringLength(100, ErrorMessage = "Route name cannot exceed 100 characters")]
        public string RouteName { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Start Time")]
        public string StartTimeFormatted { get; set; } = string.Empty;

        [Display(Name = "End Time")]
        public string EndTimeFormatted { get; set; } = string.Empty;

        [Display(Name = "Duration (min)")]
        public int? DurationMinutes { get; set; }

        [Display(Name = "Distance (mi)")]
        [Range(0, 500, ErrorMessage = "Distance must be between 0 and 500 miles")]
        public decimal? DistanceMiles { get; set; }

        [Display(Name = "Status")]
        public string StatusDisplay { get; set; } = "Unknown";

        public int? BusId { get; set; }

        [Display(Name = "Bus")]
        public string BusNumber { get; set; } = string.Empty;

        public int? DriverId { get; set; }

        [Display(Name = "Driver")]
        public string DriverName { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        // Derived property for display
        [Display(Name = "Route Info")]
        public string DisplayName => $"{RouteNumber} - {RouteName}";
    }
}
