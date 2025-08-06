using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Models
{
    public class BusViewModel : INotifyPropertyChanged
    {
        public int BusId { get; set; }

        [Required(ErrorMessage = "Bus number is required")]
        [Display(Name = "Bus #")]
        public string BusNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Capacity is required")]
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100")]
        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Make")]
        public string Make { get; set; } = string.Empty;

        [Display(Name = "Model")]
        public string Model { get; set; } = string.Empty;

        [Display(Name = "Year")]
        [Range(1980, 2050, ErrorMessage = "Year must be between 1980 and 2050")]
        public int? Year { get; set; }

        [Display(Name = "License Plate")]
        public string LicensePlate { get; set; } = string.Empty;

        [Display(Name = "Last Maintenance")]
        public string LastMaintenanceDateFormatted { get; set; } = string.Empty;

        [Display(Name = "Miles Since Maintenance")]
        public int? MilesSinceLastMaintenance { get; set; }

        [Display(Name = "Status")]
        public string MaintenanceStatus { get; set; } = "Unknown";

        [Display(Name = "Notes")]
        public string Notes { get; set; } = string.Empty;

        // Derived property for display
        [Display(Name = "Bus Info")]
        public string DisplayName => $"{BusNumber} - {Make} {Model} ({Year ?? 0})";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
