using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a school bus vehicle in the fleet
/// Based on Vehicle Table from BusBuddy Tables schema
/// Matches requirements: Bus #, Year, Make, Model, Seating Capacity, VIN Number, License Number, Date Last Inspection
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support and comprehensive NULL handling
/// </summary>
[Table("Vehicles")]
[DebuggerDisplay("Bus {BusNumber} - {Year} {Make} {Model} - ID:{VehicleId}")]
public class Bus : INotifyPropertyChanged
{
    private string _busNumber = string.Empty;
    private int _year;
    private string _make = string.Empty;
    private string _model = string.Empty;
    private int _seatingCapacity;
    private string _vinNumber = string.Empty;
    private string _licenseNumber = string.Empty;
    private DateTime? _dateLastInspection;
    private int? _currentOdometer;
    private string _status = "Active";
    private DateTime? _purchaseDate;
    private decimal? _purchasePrice;
    private string? _insurancePolicyNumber;
    private DateTime? _insuranceExpiryDate;
    private decimal? _currentLatitude;
    private decimal? _currentLongitude;

    [Key]
    public int VehicleId { get; set; }

    /// <summary>
    /// Compatibility property for Id access (maps to VehicleId)
    /// </summary>
    [NotMapped]
    public int Id
    {
        get => VehicleId;
        set => VehicleId = value;
    }

    [Required]
    [StringLength(20)]
    [Display(Name = "Bus #")]
    public string BusNumber
    {
        get => _busNumber;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? $"BUS-{VehicleId:000}" : value.Trim();
            if (_busNumber != newValue)
            {
                _busNumber = newValue;
                OnPropertyChanged(nameof(BusNumber));
            }
        }
    }

    [Required]
    [Range(1990, 2030)]
    [Display(Name = "Year")]
    public int Year
    {
        get => _year;
        set
        {
            var newValue = value <= 0 ? DateTime.Now.Year : value;
            if (_year != newValue)
            {
                _year = newValue;
                OnPropertyChanged(nameof(Year));
            }
        }
    }

    [Required]
    [StringLength(50)]
    [Display(Name = "Make")]
    public string Make
    {
        get => _make;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? "Unknown Make" : value.Trim();
            if (_make != newValue)
            {
                _make = newValue;
                OnPropertyChanged(nameof(Make));
            }
        }
    }

    [Required]
    [StringLength(50)]
    [Display(Name = "Model")]
    public string Model
    {
        get => _model;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? "Unknown Model" : value.Trim();
            if (_model != newValue)
            {
                _model = newValue;
                OnPropertyChanged(nameof(Model));
            }
        }
    }

    [Required]
    [Range(1, 100)]
    [Display(Name = "Seating Capacity")]
    public int SeatingCapacity
    {
        get => _seatingCapacity;
        set
        {
            var newValue = value <= 0 ? 30 : value; // Default to 30 seats
            if (_seatingCapacity != newValue)
            {
                _seatingCapacity = newValue;
                OnPropertyChanged(nameof(SeatingCapacity));
            }
        }
    }

    [Required]
    [StringLength(17)]
    [Display(Name = "VIN Number")]
    public string VINNumber
    {
        get => _vinNumber;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? $"TEMP-VIN-{VehicleId:00000}" : value.Trim();
            if (_vinNumber != newValue)
            {
                _vinNumber = newValue;
                OnPropertyChanged(nameof(VINNumber));
            }
        }
    }

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "License Number")]
    public string LicenseNumber
    {
        get => _licenseNumber;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? $"TEMP-LIC-{VehicleId:000}" : value.Trim();
            if (_licenseNumber != newValue)
            {
                _licenseNumber = newValue;
                OnPropertyChanged(nameof(LicenseNumber));
                OnPropertyChanged(nameof(LicensePlate)); // Notify alias property too
            }
        }
    }

    /// <summary>
    /// Compatibility property for LicensePlate access (maps to LicenseNumber)
    /// </summary>
    [NotMapped]
    public string LicensePlate
    {
        get => LicenseNumber;
        set => LicenseNumber = value;
    }

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status
    {
        get => _status;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? "Active" : value.Trim();
            if (_status != newValue)
            {
                _status = newValue;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    [Display(Name = "Date Last Inspection")]
    public DateTime? DateLastInspection
    {
        get => _dateLastInspection;
        set
        {
            if (_dateLastInspection != value)
            {
                _dateLastInspection = value;
                OnPropertyChanged(nameof(DateLastInspection));
                OnPropertyChanged(nameof(InspectionStatus));
            }
        }
    }

    [Display(Name = "Current Odometer")]
    public int? CurrentOdometer
    {
        get => _currentOdometer;
        set
        {
            if (_currentOdometer != value)
            {
                _currentOdometer = value;
                OnPropertyChanged(nameof(CurrentOdometer));
                OnPropertyChanged(nameof(Mileage)); // Notify alias property too
            }
        }
    }

    /// <summary>
    /// Compatibility property for Mileage access (maps to CurrentOdometer)
    /// </summary>
    [NotMapped]
    public int Mileage
    {
        get => CurrentOdometer ?? 0;
        set => CurrentOdometer = value;
    }



    [Display(Name = "Purchase Date")]
    public DateTime? PurchaseDate
    {
        get => _purchaseDate;
        set
        {
            if (_purchaseDate != value)
            {
                _purchaseDate = value;
                OnPropertyChanged(nameof(PurchaseDate));
            }
        }
    }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Purchase Price")]
    public decimal? PurchasePrice
    {
        get => _purchasePrice;
        set
        {
            if (_purchasePrice != value)
            {
                _purchasePrice = value;
                OnPropertyChanged(nameof(PurchasePrice));
            }
        }
    }

    [StringLength(100)]
    [Display(Name = "Insurance Policy")]
    public string? InsurancePolicyNumber
    {
        get => _insurancePolicyNumber;
        set
        {
            if (_insurancePolicyNumber != value)
            {
                _insurancePolicyNumber = value;
                OnPropertyChanged(nameof(InsurancePolicyNumber));
            }
        }
    }

    [Display(Name = "Insurance Expiry")]
    public DateTime? InsuranceExpiryDate
    {
        get => _insuranceExpiryDate;
        set
        {
            if (_insuranceExpiryDate != value)
            {
                _insuranceExpiryDate = value;
                OnPropertyChanged(nameof(InsuranceExpiryDate));
                OnPropertyChanged(nameof(InsuranceStatus));
            }
        }
    }

    // Geo tracking for live map
    [Column(TypeName = "decimal(10,8)")]
    [Display(Name = "Current Latitude")]
    public decimal? CurrentLatitude
    {
        get => _currentLatitude;
        set
        {
            if (_currentLatitude != value)
            {
                _currentLatitude = value;
                OnPropertyChanged(nameof(CurrentLatitude));
            }
        }
    }

    [Column(TypeName = "decimal(11,8)")]
    [Display(Name = "Current Longitude")]
    public decimal? CurrentLongitude
    {
        get => _currentLongitude;
        set
        {
            if (_currentLongitude != value)
            {
                _currentLongitude = value;
                OnPropertyChanged(nameof(CurrentLongitude));
            }
        }
    }

    // Extended properties for maximum flexibility
    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } // Remove dynamic default, handle in application logic

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy { get; set; }

    [StringLength(50)]
    [Display(Name = "Department")]
    public string? Department { get; set; }

    [StringLength(20)]
    [Display(Name = "Fleet Type")]
    public string? FleetType { get; set; } // Regular, Special Needs, Activity, etc.

    [Display(Name = "Fuel Capacity")]
    [Column(TypeName = "decimal(8,2)")]
    public decimal? FuelCapacity { get; set; }

    [StringLength(20)]
    [Display(Name = "Fuel Type")]
    public string? FuelType { get; set; } // Gasoline, Diesel, Electric, Hybrid

    [Display(Name = "Miles Per Gallon")]
    [Column(TypeName = "decimal(6,2)")]
    public decimal? MilesPerGallon { get; set; }

    [Display(Name = "Next Maintenance Due")]
    public DateTime? NextMaintenanceDue { get; set; }

    [Display(Name = "Next Maintenance Mileage")]
    public int? NextMaintenanceMileage { get; set; }

    [Display(Name = "Last Service Date")]
    public DateTime? LastServiceDate { get; set; }

    [StringLength(1000)]
    [Display(Name = "Special Equipment")]
    public string? SpecialEquipment { get; set; } // Wheelchair lift, Air conditioning, etc.

    [Display(Name = "GPS Tracking")]
    public bool GPSTracking { get; set; } = false;

    [StringLength(100)]
    [Display(Name = "GPS Device ID")]
    public string? GPSDeviceId { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    // Computed properties for UI and reporting
    [NotMapped]
    [Display(Name = "Age")]
    public int Age => DateTime.Now.Year - Year;

    [NotMapped]
    [Display(Name = "Full Description")]
    public string FullDescription => $"{Year} {Make} {Model} (#{BusNumber})";

    /// <summary>
    /// Compatibility property for DisplayName access (maps to FullDescription format)
    /// </summary>
    [NotMapped]
    public string DisplayName => $"{BusNumber} - {Make} {Model}";

    [NotMapped]
    [Display(Name = "Inspection Status")]
    public string InspectionStatus
    {
        get
        {
            if (!DateLastInspection.HasValue)
            {
                return "Overdue";
            }

            var daysSinceInspection = (DateTime.Now - DateLastInspection.Value).TotalDays;
            var monthsSinceInspection = daysSinceInspection / 30.0; // Use double for precision
            return monthsSinceInspection switch
            {
                > 12.0 => "Overdue",
                > 11.0 => "Due Soon",
                _ => "Current"
            };
        }
    }

    [NotMapped]
    [Display(Name = "Insurance Status")]
    public string InsuranceStatus
    {
        get
        {
            if (!InsuranceExpiryDate.HasValue)
            {
                return "Unknown";
            }

            var daysUntilExpiry = (InsuranceExpiryDate.Value - DateTime.Now).Days;
            return daysUntilExpiry switch
            {
                < 0 => "Expired",
                < 30 => "Expiring Soon",
                _ => "Current"
            };
        }
    }

    [NotMapped]
    [Display(Name = "Is Available")]
    public bool IsAvailable => Status == "Active";

    [NotMapped]
    [Display(Name = "Needs Attention")]
    public bool NeedsAttention => InspectionStatus != "Current" || InsuranceStatus == "Expired" || InsuranceStatus == "Expiring Soon";

    // Navigation properties
    public virtual ICollection<Route> AMRoutes { get; set; } = new List<Route>();
    public virtual ICollection<Route> PMRoutes { get; set; } = new List<Route>();
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<ActivitySchedule> ScheduledActivities { get; set; } = new List<ActivitySchedule>();
    public virtual ICollection<Fuel> FuelRecords { get; set; } = new List<Fuel>();
    public virtual ICollection<Maintenance> MaintenanceRecords { get; set; } = new List<Maintenance>();

    // Legacy navigation for backward compatibility
    [NotMapped]
    public virtual ICollection<Route> Routes => AMRoutes.Concat(PMRoutes).ToList();

    /// <summary>
    /// Compatibility property for Capacity access (maps to SeatingCapacity for backward compatibility)
    /// </summary>
    [NotMapped]
    public int Capacity
    {
        get => SeatingCapacity;
        set => SeatingCapacity = value;
    }

    // INotifyPropertyChanged implementation for Syncfusion data binding

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
