using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents an activity record for bus operations
/// Based on Activities Table from BusBuddy Tables schema
/// Matches requirements: Date, Activity, Destination, Leave Time, Event Time, Requested By, Assigned Vehicle, Driver
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support
/// </summary>
[Table("Activities")]
public class Activity : INotifyPropertyChanged
{
    private DateTime _date;
    private string _activityType = string.Empty;
    private string _destination = string.Empty;
    private TimeSpan _leaveTime;
    private TimeSpan _eventTime;
    private string _requestedBy = string.Empty;
    private int _assignedVehicleId;
    private int _driverId;
    private int? _studentsCount;
    private string? _notes;
    private string _status = "Scheduled";
    private int? _routeId;
    private int? _startOdometer;
    private int? _endOdometer;
    private string? _description;
    private TimeSpan _returnTime;
    private int? _expectedPassengers;
    private int? _recurringSeriesId;

    [Key]
    public int ActivityId { get; set; }

    [Required]
    [Display(Name = "Date")]
    public DateTime Date
    {
        get => _date;
        set
        {
            if (_date != value)
            {
                _date = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActivityDate));
            }
        }
    }

    [Required]
    [StringLength(50)]
    [Display(Name = "Activity")]
    public string ActivityType
    {
        get => _activityType;
        set
        {
            if (_activityType != value)
            {
                _activityType = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    [Required]
    [StringLength(200)]
    [Display(Name = "Destination")]
    public string Destination
    {
        get => _destination;
        set
        {
            if (_destination != value)
            {
                _destination = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Foreign key to the Destinations table (optional for backward compatibility)
    /// </summary>
    [Display(Name = "Destination")]
    public int? DestinationId { get; set; }

    /// <summary>
    /// Navigation property to the Destination entity
    /// </summary>
    [ForeignKey("DestinationId")]
    public virtual Destination? DestinationEntity { get; set; }

    /// <summary>
    /// Override destination text for custom locations not in the Destinations table
    /// </summary>
    [StringLength(200)]
    [Display(Name = "Custom Destination")]
    public string? DestinationOverride { get; set; }

    [Required]
    [Display(Name = "Leave Time")]
    public TimeSpan LeaveTime
    {
        get => _leaveTime;
        set
        {
            if (_leaveTime != value)
            {
                _leaveTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTime));
            }
        }
    }

    [Required]
    [Display(Name = "Event Time")]
    public TimeSpan EventTime
    {
        get => _eventTime;
        set
        {
            if (_eventTime != value)
            {
                _eventTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTime));
            }
        }
    }

    [Required]
    [StringLength(100)]
    [Display(Name = "Requested By")]
    public string RequestedBy
    {
        get => _requestedBy;
        set
        {
            if (_requestedBy != value)
            {
                _requestedBy = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    [Required]
    [ForeignKey("AssignedVehicle")]
    [Display(Name = "Assigned Vehicle")]
    public int AssignedVehicleId
    {
        get => _assignedVehicleId;
        set
        {
            if (_assignedVehicleId != value)
            {
                _assignedVehicleId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VehicleId));
            }
        }
    }

    [ForeignKey("Driver")]
    [Display(Name = "Driver")]
    public int? DriverId
    {
        get => _driverId == 0 ? null : _driverId;
        set
        {
            var newValue = value ?? 0;
            if (_driverId != newValue)
            {
                _driverId = newValue;
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Students Count")]
    public int? StudentsCount
    {
        get => _studentsCount;
        set
        {
            if (_studentsCount != value)
            {
                _studentsCount = value;
                OnPropertyChanged();
            }
        }
    }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged();
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
                _status = value ?? "Scheduled";
                OnPropertyChanged();
            }
        }
    }

    // Additional properties for enhanced functionality with change notifications
    [Display(Name = "Activity Date")]
    [NotMapped]
    public DateTime ActivityDate
    {
        get => Date;
        set => Date = value;
    }

    [Display(Name = "Start Time")]
    [NotMapped]
    public TimeSpan? StartTime
    {
        get => LeaveTime;
        set => LeaveTime = value ?? TimeSpan.Zero;
    }

    [Display(Name = "End Time")]
    [NotMapped]
    public TimeSpan? EndTime
    {
        get => EventTime;
        set => EventTime = value ?? TimeSpan.Zero;
    }

    // Compatibility properties for existing code with change notifications
    [NotMapped]
    [Display(Name = "Vehicle ID")]
    public int VehicleId
    {
        get => AssignedVehicleId;
        set => AssignedVehicleId = value;
    }

    [Display(Name = "Route ID")]
    [ForeignKey("Route")]
    public int? RouteId
    {
        get => _routeId;
        set
        {
            if (_routeId != value)
            {
                _routeId = value;
                OnPropertyChanged();
            }
        }
    }

    [NotMapped]
    [Display(Name = "Start Odometer")]
    public int? StartOdometer
    {
        get => _startOdometer;
        set
        {
            if (_startOdometer != value)
            {
                _startOdometer = value;
                OnPropertyChanged();
            }
        }
    }

    [NotMapped]
    [Display(Name = "End Odometer")]
    public int? EndOdometer
    {
        get => _endOdometer;
        set
        {
            if (_endOdometer != value)
            {
                _endOdometer = value;
                OnPropertyChanged();
            }
        }
    }

    // Properties needed for ActivityService implementation
    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Return Time")]
    public TimeSpan ReturnTime
    {
        get => _returnTime;
        set
        {
            if (_returnTime != value)
            {
                _returnTime = value;
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Expected Passengers")]
    public int? ExpectedPassengers
    {
        get => _expectedPassengers;
        set
        {
            if (_expectedPassengers != value)
            {
                _expectedPassengers = value;
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Recurring Series ID")]
    public int? RecurringSeriesId
    {
        get => _recurringSeriesId;
        set
        {
            if (_recurringSeriesId != value)
            {
                _recurringSeriesId = value;
                OnPropertyChanged();
            }
        }
    }

    // Extended properties for maximum flexibility
    [StringLength(100)]
    [Display(Name = "Activity Category")]
    public string? ActivityCategory { get; set; } // Sports, Field Trip, Special Event, etc.

    [Display(Name = "Estimated Cost")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedCost { get; set; }

    [Display(Name = "Actual Cost")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualCost { get; set; }

    [Display(Name = "Approval Required")]
    public bool ApprovalRequired { get; set; } = true;

    [Display(Name = "Approved")]
    public bool Approved { get; set; } = false;

    [StringLength(100)]
    [Display(Name = "Approved By")]
    public string? ApprovedBy { get; set; }

    [Display(Name = "Approval Date")]
    public DateTime? ApprovalDate { get; set; }

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

    // Geographic properties for destination coordinates (Activities need this, Routes don't)
    [Display(Name = "Destination Latitude")]
    [Column(TypeName = "decimal(10,8)")]
    public decimal? DestinationLatitude { get; set; }

    [Display(Name = "Destination Longitude")]
    [Column(TypeName = "decimal(11,8)")]
    public decimal? DestinationLongitude { get; set; }

    [Display(Name = "Distance (Miles)")]
    [Column(TypeName = "decimal(8,2)")]
    public decimal? DistanceMiles { get; set; }

    [Display(Name = "Estimated Travel Time")]
    public TimeSpan? EstimatedTravelTime { get; set; }

    // Navigation/routing properties for activities with variable destinations
    [StringLength(500)]
    [Display(Name = "Directions")]
    public string? Directions { get; set; }

    [StringLength(200)]
    [Display(Name = "Pickup Location")]
    public string? PickupLocation { get; set; } = "School"; // Default to school

    [Display(Name = "Pickup Latitude")]
    [Column(TypeName = "decimal(10,8)")]
    public decimal? PickupLatitude { get; set; }

    [Display(Name = "Pickup Longitude")]
    [Column(TypeName = "decimal(11,8)")]
    public decimal? PickupLongitude { get; set; }

    // Computed properties for Syncfusion scheduling
    [NotMapped]
    [Display(Name = "Duration")]
    public TimeSpan Duration => EventTime - LeaveTime;

    [NotMapped]
    [Display(Name = "Full Start DateTime")]
    public DateTime FullStartDateTime => Date.Date.Add(LeaveTime);

    [NotMapped]
    [Display(Name = "Full End DateTime")]
    public DateTime FullEndDateTime => Date.Date.Add(EventTime);

    [NotMapped]
    [Display(Name = "Is All Day")]
    public bool IsAllDay => Duration.TotalHours >= 23;

    // Navigation properties
    public virtual Bus AssignedVehicle { get; set; } = null!;

    // Legacy navigation for backward compatibility
    [NotMapped]
    public virtual Bus Vehicle => AssignedVehicle;

    public virtual Driver? Driver { get; set; }

    // Route navigation property for activities
    public virtual Route? Route { get; set; }

    // Activity schedules for this activity
    public virtual ICollection<ActivitySchedule> ActivitySchedules { get; set; } = new List<ActivitySchedule>();

    // INotifyPropertyChanged implementation for Syncfusion data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string ActivityName { get; set; } = string.Empty;
    public TimeSpan DepartureTime { get; set; }
    public TimeSpan EstimatedArrival { get; set; }
    public int AssignedDriverId { get; set; }
}
