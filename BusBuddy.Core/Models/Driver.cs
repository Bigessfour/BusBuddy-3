using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a school bus driver
/// Based on Drivers Table from BusBuddy Tables schema
/// Matches requirements: Driver Name, Driver Phone, Driver Email, Address, City, State, Zip, Drivers Licence Type, Training Complete
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support
/// </summary>
[Table("Drivers")]
public class Driver : INotifyPropertyChanged
{
    private string _driverName = string.Empty;
    private string? _driverPhone;
    private string? _driverEmail;
    private string? _address;
    private string? _city;
    private string? _state;
    private string? _zip;
    private string _driversLicenceType = string.Empty;
    private bool _trainingComplete;
    private string _status = "Active";
    private decimal? _homeLatitude;
    private decimal? _homeLongitude;

    [Key]
    public int DriverId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Driver Name")]
    public string DriverName
    {
        get => _driverName;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? $"Driver-{DriverId}" : value.Trim();
            if (_driverName != newValue)
            {
                _driverName = newValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    [StringLength(20)]
    [Display(Name = "Driver Phone")]
    public string? DriverPhone
    {
        get => _driverPhone;
        set
        {
            if (_driverPhone != value)
            {
                _driverPhone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Phone));
            }
        }
    }

    [StringLength(100)]
    [EmailAddress]
    [Display(Name = "Driver Email")]
    public string? DriverEmail
    {
        get => _driverEmail;
        set
        {
            if (_driverEmail != value)
            {
                _driverEmail = value;
                OnPropertyChanged();
            }
        }
    }

    [StringLength(200)]
    [Display(Name = "Address")]
    public string? Address
    {
        get => _address;
        set
        {
            if (_address != value)
            {
                _address = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    [StringLength(50)]
    [Display(Name = "City")]
    public string? City
    {
        get => _city;
        set
        {
            if (_city != value)
            {
                _city = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    [StringLength(2)]
    [Display(Name = "State")]
    public string? State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    [StringLength(10)]
    [Display(Name = "Zip")]
    public string? Zip
    {
        get => _zip;
        set
        {
            if (_zip != value)
            {
                _zip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullAddress));
            }
        }
    }

    [Required]
    [StringLength(20)]
    [Display(Name = "Drivers Licence Type")]
    public string DriversLicenceType
    {
        get => _driversLicenceType;
        set
        {
            if (_driversLicenceType != value)
            {
                _driversLicenceType = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Training Complete?")]
    public bool TrainingComplete
    {
        get => _trainingComplete;
        set
        {
            if (_trainingComplete != value)
            {
                _trainingComplete = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QualificationStatus));
            }
        }
    }

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value ?? "Active";
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAvailable));
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    // Geo home location for route planning
    [Column(TypeName = "decimal(10,8)")]
    [Display(Name = "Home Latitude")]
    public decimal? HomeLatitude
    {
        get => _homeLatitude;
        set
        {
            if (_homeLatitude != value)
            {
                _homeLatitude = value;
                OnPropertyChanged();
            }
        }
    }

    [Column(TypeName = "decimal(11,8)")]
    [Display(Name = "Home Longitude")]
    public decimal? HomeLongitude
    {
        get => _homeLongitude;
        set
        {
            if (_homeLongitude != value)
            {
                _homeLongitude = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Compatibility property for Id access
    /// </summary>
    [NotMapped]
    public int Id => DriverId;

    /// <summary>
    /// Compatibility property for Name access
    /// </summary>
    [NotMapped]
    public string Name => DriverName;

    /// <summary>
    /// Indicates if the driver is active
    /// </summary>
    [NotMapped]
    public bool IsActive => Status == "Active";

    // Additional properties for enhanced functionality
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string? FirstName { get; set; }
    // NOTE: For MVP Save button enablement we need PropertyChanged when First/Last names change.
    // Rather than refactor auto-property widely, provide explicit backing fields in future.

    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [StringLength(20)]
    [Display(Name = "License Number")]
    public string? LicenseNumber { get; set; }

    [StringLength(10)]
    [Display(Name = "License Class")]
    public string? LicenseClass { get; set; }

    [Display(Name = "License Issue Date")]
    public DateTime? LicenseIssueDate { get; set; }

    [Display(Name = "License Expiry Date")]
    public DateTime? LicenseExpiryDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Endorsements")]
    public string? Endorsements { get; set; }

    [Display(Name = "Hire Date")]
    public DateTime? HireDate { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } // Remove dynamic default, handle in application logic

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    // Extended properties for maximum flexibility
    [StringLength(100)]
    [Display(Name = "Emergency Contact Name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    [Display(Name = "Emergency Contact Phone")]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(100)]
    [Display(Name = "Medical Restrictions")]
    public string? MedicalRestrictions { get; set; }

    [Display(Name = "Background Check Date")]
    public DateTime? BackgroundCheckDate { get; set; }

    [Display(Name = "Background Check Expiry")]
    public DateTime? BackgroundCheckExpiry { get; set; }

    [Display(Name = "Drug Test Date")]
    public DateTime? DrugTestDate { get; set; }

    [Display(Name = "Drug Test Expiry")]
    public DateTime? DrugTestExpiry { get; set; }

    [Display(Name = "Physical Exam Date")]
    public DateTime? PhysicalExamDate { get; set; }

    [Display(Name = "Physical Exam Expiry")]
    public DateTime? PhysicalExamExpiry { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy { get; set; }

    // Computed properties for UI and reporting
    [NotMapped]
    [Display(Name = "Full Name")]
    public string FullName
    {
        get
        {
            if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
            {

                return $"{FirstName} {LastName}";
            }


            return DriverName;
        }
    }

    [NotMapped]
    [Display(Name = "Full Address")]
    public string FullAddress
    {
        get
        {
            var parts = new[] { Address, City, State, Zip }.Where(p => !string.IsNullOrEmpty(p));
            return string.Join(", ", parts);
        }
    }

    [NotMapped]
    [Display(Name = "Qualification Status")]
    public string QualificationStatus
    {
        get
        {
            if (!TrainingComplete)
            {
                return "Training Required";
            }


            if (LicenseExpiryDate.HasValue && LicenseExpiryDate.Value < DateTime.Now)
            {
                return "License Expired";
            }


            if (LicenseExpiryDate.HasValue && LicenseExpiryDate.Value < DateTime.Now.AddDays(30))
            {
                return "License Expiring";
            }


            return "Qualified";
        }
    }

    [NotMapped]
    [Display(Name = "License Status")]
    public string LicenseStatus
    {
        get
        {
            if (!LicenseExpiryDate.HasValue)
            {
                return "Unknown";
            }


            var daysUntilExpiry = (LicenseExpiryDate.Value - DateTime.Now).Days;
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
    public bool IsAvailable => Status == "Active" && TrainingComplete && LicenseStatus != "Expired";

    [NotMapped]
    [Display(Name = "Needs Attention")]
    public bool NeedsAttention => !TrainingComplete || LicenseStatus == "Expired" || LicenseStatus == "Expiring Soon";

    [NotMapped]
    [Display(Name = "Contact Phone")]
    public string? ContactPhone
    {
        get => DriverPhone;
        set
        {
            if (DriverPhone != value)
            {
                DriverPhone = value;
                OnPropertyChanged();
            }
        }
    }

    [NotMapped]
    [Display(Name = "Contact Email")]
    public string? ContactEmail
    {
        get => DriverEmail;
        set
        {
            if (DriverEmail != value)
            {
                DriverEmail = value;
                OnPropertyChanged();
            }
        }
    }

    // Compatibility properties
    [NotMapped]
    public string? Phone => DriverPhone;

    // Navigation properties
    public virtual ICollection<Route> AMRoutes { get; set; } = new List<Route>();
    public virtual ICollection<Route> PMRoutes { get; set; } = new List<Route>();
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<ActivitySchedule> ScheduledActivities { get; set; } = new List<ActivitySchedule>();

    // INotifyPropertyChanged implementation for Syncfusion data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
