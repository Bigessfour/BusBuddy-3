using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a daily route record
/// Based on Routes Table from BusBuddy Tables schema
/// Enhanced with proper DateTime handling and NULL safety
/// </summary>
[Table("Routes")]
public partial class Route : INotifyPropertyChanged
{
    private DateTime _date = DateTime.Today;
    private string _routeName = string.Empty;
    private string? _description;
    private bool _isActive = true;

    [Key]
    public int RouteId { get; set; }

    [Required]
    [Display(Name = "Date")]
    public DateTime Date
    {
        get => _date;
        set
        {
            // Ensure date is always valid and not in the far future
            var newValue = value == default(DateTime) ? DateTime.Today :
                          value > DateTime.Today.AddYears(1) ? DateTime.Today : value.Date;
            if (_date != newValue)
            {
                _date = newValue;
                OnPropertyChanged();
            }
        }
    }

    [Required]
    [StringLength(50)]
    [Display(Name = "Route Name")]
    public string RouteName
    {
        get => _routeName;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? $"Route-{RouteId}" : value.Trim();
            if (_routeName != newValue)
            {
                _routeName = newValue;
                OnPropertyChanged();
            }
        }
    }

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                OnPropertyChanged();
            }
        }
    }

    [Display(Name = "Is Active")]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    // AM Route Information
    [ForeignKey("AMVehicle")]
    [Display(Name = "AM Vehicle")]
    public int? AMVehicleId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "AM Begin Miles")]
    public decimal? AMBeginMiles { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "AM End Miles")]
    public decimal? AMEndMiles { get; set; }

    [Range(0, 100)]
    [Display(Name = "AM Riders")]
    public int? AMRiders { get; set; }

    [ForeignKey("AMDriver")]
    [Display(Name = "AM Driver")]
    public int? AMDriverId { get; set; }

    // PM Route Information
    [ForeignKey("PMVehicle")]
    [Display(Name = "PM Vehicle")]
    public int? PMVehicleId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "PM Begin Miles")]
    public decimal? PMBeginMiles { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "PM End Miles")]
    public decimal? PMEndMiles { get; set; }

    [Range(0, 100)]
    [Display(Name = "PM Riders")]
    public int? PMRiders { get; set; }

    [ForeignKey("PMDriver")]
    [Display(Name = "PM Driver")]
    public int? PMDriverId { get; set; }

    // Additional properties for route analysis and optimization
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Distance (Miles)")]
    public decimal? Distance { get; set; }

    [Display(Name = "Estimated Duration (Minutes)")]
    public int? EstimatedDuration { get; set; }

    [Display(Name = "Student Count")]
    public int? StudentCount { get; set; }

    [Display(Name = "Stop Count")]
    public int? StopCount { get; set; }

    [Display(Name = "AM Begin Time")]
    public TimeSpan? AMBeginTime { get; set; }

    [Display(Name = "PM Begin Time")]
    public TimeSpan? PMBeginTime { get; set; }

    [StringLength(100)]
    [Display(Name = "Driver Name")]
    public string? DriverName { get; set; }

    [StringLength(20)]
    [Display(Name = "Bus Number")]
    public string? BusNumber { get; set; }

    // Navigation properties
    public virtual Bus? AMVehicle { get; set; }
    public virtual Bus? PMVehicle { get; set; }
    public virtual Driver? AMDriver { get; set; }
    public virtual Driver? PMDriver { get; set; }
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    // Computed properties with null safety
    public string SafeRouteName => string.IsNullOrWhiteSpace(RouteName) ? $"Route-{RouteId}" : RouteName;
    public string DateFormatted => Date.ToString("yyyy-MM-dd");
    public bool HasAMAssignment => AMVehicleId.HasValue && AMDriverId.HasValue;
    public bool HasPMAssignment => PMVehicleId.HasValue && PMDriverId.HasValue;
    public decimal TotalMiles => (AMEndMiles - AMBeginMiles ?? 0) + (PMEndMiles - PMBeginMiles ?? 0);

    [StringLength(100)]
    [Display(Name = "School")]
    public string? School { get; set; }

    [StringLength(200)]
    [Display(Name = "Route Description")]
    public string? RouteDescription { get; set; }

    [StringLength(200)]
    [Display(Name = "Boundaries")]
    public string? Boundaries { get; set; }

    [StringLength(300)]
    [Display(Name = "Path")]
    public string? Path { get; set; }

    // Geo JSON for waypoints polyline (MVP-friendly)
    [StringLength(4000)]
    [Display(Name = "Waypoints GeoJSON")]
    public string? WaypointsJson { get; set; }

    // Optional shapefile hints (paths) for boundaries
    [StringLength(500)]
    [Display(Name = "District Boundary Shapefile")]
    public string? DistrictBoundaryShapefilePath { get; set; }

    [StringLength(500)]
    [Display(Name = "Town Boundary Shapefile")]
    public string? TownBoundaryShapefilePath { get; set; }

    // INotifyPropertyChanged implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Backwards-compatible aliases for older WPF naming
    [NotMapped]
    public int? AMBusId
    {
        get => AMVehicleId;
        set => AMVehicleId = value;
    }

    [NotMapped]
    public int? PMBusId
    {
        get => PMVehicleId;
        set => PMVehicleId = value;
    }
}
