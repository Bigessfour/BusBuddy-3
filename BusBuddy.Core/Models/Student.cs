using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a student in the school bus system
/// Based on Students Table from BusBuddy Tables schema
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support
/// </summary>
[Table("Students")]
public class Student : INotifyPropertyChanged
{
    [Key]
    public int StudentId { get; set; }

    [Required(ErrorMessage = "Student name is required")]
    [StringLength(100, ErrorMessage = "Student name cannot exceed 100 characters")]
    [Display(Name = "Student Name")]
    public string StudentName { get; set; } = string.Empty;

    // Family association can be added later during edit
    public int? FamilyId { get; set; }
    public Family? Family { get; set; }

    /// <summary>
    /// Shared district pickup stop. Null = home pickup.
    /// A student home is not a catalog stop unless a clerk publishes that address as PickupStop.
    /// </summary>
    public int? PickupStopId { get; set; }

    [ForeignKey(nameof(PickupStopId))]
    public PickupStop? PickupStop { get; set; }

    /// <summary>Assigned campus from Destinations (DestinationType = School).</summary>
    public int? DestinationId { get; set; }

    [ForeignKey(nameof(DestinationId))]
    public Destination? Destination { get; set; }
    [StringLength(20, ErrorMessage = "Student number cannot exceed 20 characters")]
    [Display(Name = "Student Number")]
    public string? StudentNumber { get; set; }

    [StringLength(20, ErrorMessage = "Grade cannot exceed 20 characters")]
    [Display(Name = "Grade")]
    public string? Grade { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(10, ErrorMessage = "Gender cannot exceed 10 characters")]
    [Display(Name = "Gender")]
    public string? Gender { get; set; }

    [StringLength(200, ErrorMessage = "Home address cannot exceed 200 characters")]
    [Display(Name = "Home Address")]
    public string? HomeAddress
    {
        get => _homeAddress;
        set => SetField(ref _homeAddress, value);
    }

    private string? _homeAddress;

    // Geo coordinates for mapping (WGS84). Use decimal for SQL precision.
    [Column(TypeName = "decimal(10,8)")]
    [Display(Name = "Latitude")]
    public decimal? Latitude
    {
        get => _latitude;
        set
        {
            if (SetField(ref _latitude, value))
            {
                OnPropertyChanged(nameof(HasValidatedHomeCoordinates));
                OnPropertyChanged(nameof(HasUnvalidatedHomeAddress));
            }
        }
    }

    private decimal? _latitude;

    [Column(TypeName = "decimal(11,8)")]
    [Display(Name = "Longitude")]
    public decimal? Longitude
    {
        get => _longitude;
        set
        {
            if (SetField(ref _longitude, value))
            {
                OnPropertyChanged(nameof(HasValidatedHomeCoordinates));
                OnPropertyChanged(nameof(HasUnvalidatedHomeAddress));
            }
        }
    }

    private decimal? _longitude;

    /// <summary>
    /// Google place id for this home address (Address Validation / Places). May be stored indefinitely;
    /// lat/lng are refreshed separately under Maps Platform cache policy.
    /// </summary>
    [StringLength(256)]
    [Display(Name = "Place Id")]
    public string? PlaceId { get; set; }

    /// <summary>
    /// Plotted home is a clerk map click (driveway or gate), not the Address Validation geocode.
    /// Re-validation of the same street keeps this point. A new street address clears it.
    /// </summary>
    public bool HomePickupClerkAdjusted { get; set; }

    [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
    [Display(Name = "City")]
    public string? City
    {
        get => _city;
        set => SetField(ref _city, value);
    }

    private string? _city;

    [StringLength(2, ErrorMessage = "State cannot exceed 2 characters")]
    [Display(Name = "State")]
    public string? State
    {
        get => _state;
        set => SetField(ref _state, value);
    }

    private string? _state;

    [StringLength(10, ErrorMessage = "ZIP code cannot exceed 10 characters")]
    [Display(Name = "Zip Code")]
    public string? Zip
    {
        get => _zip;
        set => SetField(ref _zip, value);
    }

    private string? _zip;

    [StringLength(20, ErrorMessage = "Home phone cannot exceed 20 characters")]
    [Display(Name = "Home Phone")]
    public string? HomePhone { get; set; }

    [StringLength(20, ErrorMessage = "Cell phone cannot exceed 20 characters")]
    [Phone]
    [Display(Name = "Parent/Guardian Cell")]
    public string? CellPhone { get; set; }

    [StringLength(100, ErrorMessage = "Parent/Guardian name cannot exceed 100 characters")]
    [Display(Name = "Parent/Guardian")]
    public string? ParentGuardian { get; set; }

    [StringLength(100, ErrorMessage = "Parent email cannot exceed 100 characters")]
    [EmailAddress]
    [Display(Name = "Parent/Guardian Email")]
    public string? ParentEmail { get; set; }

    /// <summary>
    /// Emergency contact person (often different from primary parent/guardian).
    /// Kept separate from <see cref="ParentGuardian"/> per school emergency-card practice.
    /// </summary>
    [StringLength(100, ErrorMessage = "Emergency contact name cannot exceed 100 characters")]
    [Display(Name = "Emergency Contact Name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(20, ErrorMessage = "Emergency phone cannot exceed 20 characters")]
    [Display(Name = "Emergency Contact Phone")]
    public string? EmergencyPhone { get; set; }

    [StringLength(100, ErrorMessage = "School name cannot exceed 100 characters")]
    [Display(Name = "School")]
    public string? School { get; set; }

    [StringLength(50, ErrorMessage = "Bus stop cannot exceed 50 characters")]
    [Display(Name = "Bus Stop")]
    public string? BusStop { get; set; }

    [StringLength(50, ErrorMessage = "AM Route cannot exceed 50 characters")]
    [Display(Name = "AM Route")]
    public string? AMRoute { get; set; }

    [StringLength(50, ErrorMessage = "PM Route cannot exceed 50 characters")]
    [Display(Name = "PM Route")]
    public string? PMRoute { get; set; }

    /// <summary>
    /// Morning route assignment by identity. <see cref="AMRoute"/> is the denormalised route name kept
    /// in step with this; the name alone cannot identify a route, so renames and deletes resolve riders
    /// through this key instead. The name columns are dropped in a later phase — see the "Route FK
    /// follow-up" item in docs/action-items.md.
    /// </summary>
    [Display(Name = "AM Route Id")]
    public int? AmRouteId { get; set; }

    /// <summary>Afternoon route assignment by identity. See <see cref="AmRouteId"/>.</summary>
    [Display(Name = "PM Route Id")]
    public int? PmRouteId { get; set; }

    /// <summary>
    /// Morning ride eligibility. Independent of <see cref="RidesPm"/> and independent of whether
    /// <see cref="AMRoute"/> has been assigned yet — eligibility is not assignment.
    /// </summary>
    [Display(Name = "Rides AM")]
    public bool RidesAm
    {
        get => _ridesAm;
        set
        {
            if (_ridesAm == value)
            {
                return;
            }

            _ridesAm = value;
            OnPropertyChanged();
        }
    }

    // Eligibility is stated, never assumed: a CSV or JSON intake that says nothing about the PM run
    // must not silently mark a child eligible for it. Matches the database default in
    // 20260907150000_StudentRideEligibilityAndSchoolYear. The new-student *form* opts both on for
    // clerk convenience, where a human sees the checkboxes before saving.
    private bool _ridesAm;

    /// <summary>Afternoon ride eligibility. Independent of <see cref="RidesAm"/>.</summary>
    [Display(Name = "Rides PM")]
    public bool RidesPm
    {
        get => _ridesPm;
        set
        {
            if (_ridesPm == value)
            {
                return;
            }

            _ridesPm = value;
            OnPropertyChanged();
        }
    }

    private bool _ridesPm;

    /// <summary>School year this assignment belongs to, e.g. <c>2026-2027</c>.</summary>
    [StringLength(9, ErrorMessage = "School year must look like 2026-2027")]
    [Display(Name = "School Year")]
    public string? SchoolYear { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>True when bus staff should review medical / allergy / medication fields.</summary>
    [Display(Name = "Has Medical Needs")]
    public bool HasMedicalNeeds { get; set; }

    /// <summary>Student must ride a special-needs route (wheelchair lift, aide, etc.).</summary>
    [Display(Name = "Rides Special Needs Bus")]
    public bool RequiresSpecialNeedsBus
    {
        get => _requiresSpecialNeedsBus;
        set
        {
            if (_requiresSpecialNeedsBus == value)
            {
                return;
            }

            _requiresSpecialNeedsBus = value;
            OnPropertyChanged();
        }
    }

    private bool _requiresSpecialNeedsBus;

    [Display(Name = "Wheelchair Accessible")]
    public bool RequiresWheelchair { get; set; }

    [Display(Name = "Seat Belt Required")]
    public bool RequiresSeatBelt { get; set; }

    [Display(Name = "Aide Required")]
    public bool RequiresAide { get; set; }

    public string SpecialNeeds { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Special accommodations cannot exceed 1000 characters")]
    [Display(Name = "Special Accommodations")]
    public string? SpecialAccommodations { get; set; }

    [StringLength(200, ErrorMessage = "Allergies cannot exceed 200 characters")]
    [Display(Name = "Allergies")]
    public string? Allergies { get; set; }

    [StringLength(200, ErrorMessage = "Medications cannot exceed 200 characters")]
    [Display(Name = "Medications")]
    public string? Medications { get; set; }

    [StringLength(1000, ErrorMessage = "Medical notes cannot exceed 1000 characters")]
    [Display(Name = "Medical Notes")]
    public string? MedicalNotes { get; set; }

    [StringLength(100, ErrorMessage = "Doctor name cannot exceed 100 characters")]
    [Display(Name = "Doctor Name")]
    public string? DoctorName { get; set; }

    [StringLength(20, ErrorMessage = "Doctor phone cannot exceed 20 characters")]
    [Display(Name = "Doctor Phone")]
    public string? DoctorPhone { get; set; }

    public bool FieldTripPermission { get; set; }

    public bool PhotoPermission { get; set; }

    [StringLength(200, ErrorMessage = "Pickup address cannot exceed 200 characters")]
    [Display(Name = "Pickup Address")]
    public string? PickupAddress { get; set; }

    [StringLength(200, ErrorMessage = "Dropoff address cannot exceed 200 characters")]
    [Display(Name = "Dropoff Address")]
    public string? DropoffAddress { get; set; }

    [StringLength(1000, ErrorMessage = "Transportation notes cannot exceed 1000 characters")]
    [Display(Name = "Transportation Notes")]
    public string? TransportationNotes { get; set; }

    [StringLength(100, ErrorMessage = "Alternative contact cannot exceed 100 characters")]
    [Display(Name = "Alternative Contact")]
    public string? AlternativeContact { get; set; }

    [StringLength(20, ErrorMessage = "Alternative phone cannot exceed 20 characters")]
    [Display(Name = "Alternative Phone")]
    public string? AlternativePhone { get; set; }

    public DateTime? EnrollmentDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100, ErrorMessage = "Created by cannot exceed 100 characters")]
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [StringLength(100, ErrorMessage = "Updated by cannot exceed 100 characters")]
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<StudentSchedule> StudentSchedules { get; set; } = new List<StudentSchedule>();

    // Assignment to route
    public int? RouteAssignmentId { get; set; }
    [ForeignKey("RouteAssignmentId")]
    public RouteAssignment? RouteAssignment { get; set; }

    [NotMapped]
    public string FullAddress => string.Join(", ", new[] { HomeAddress, City, State, Zip }.Where(s => !string.IsNullOrWhiteSpace(s))!);

    /// <summary>Home vs catalog stop. Special needs always uses home pickup on a special-needs route.</summary>
    [NotMapped]
    public string PickupMode =>
        PickupStopId.HasValue && !RequiresSpecialNeedsBus
            ? LocationTypes.PickupModeCatalogStop
            : LocationTypes.PickupModeHome;

    [NotMapped]
    public string LocationType => LocationTypes.StudentHome;

    [NotMapped]
    public bool HasValidatedHomeCoordinates => LocationCoordinate.IsValidated(Latitude, Longitude);

    [NotMapped]
    public string HomeCoordinateStatus => LocationTypes.ValidationStatus(HasValidatedHomeCoordinates);

    /// <summary>
    /// A home address was entered but Address Validation has not produced lat/lng yet.
    /// The record saves and stays editable; it just cannot be plotted.
    /// </summary>
    [NotMapped]
    public bool HasUnvalidatedHomeAddress =>
        !string.IsNullOrWhiteSpace(HomeAddress) && !HasValidatedHomeCoordinates;

    /// <summary>
    /// True when the record is missing something a clerk still has to supply. Incomplete records are
    /// saved, not rejected, and are excluded from map plotting until coordinates exist.
    /// </summary>
    [NotMapped]
    public bool IsIntakeIncomplete =>
        string.IsNullOrWhiteSpace(SchoolYear)
        || DestinationId is null
        || !RidesAnySession
        || (PickupStopId is null && !HasValidatedHomeCoordinates);

    /// <summary>
    /// True when the record states at least one session of ride eligibility. Neither flag set means
    /// eligibility was never stated — imports that carry no route columns land here — and the record
    /// must surface as incomplete rather than quietly matching no route at all.
    /// </summary>
    [NotMapped]
    public bool RidesAnySession => RidesAm || RidesPm;

    /// <summary>Short grid/status label describing why a record is not yet route-ready.</summary>
    [NotMapped]
    [Display(Name = "Intake Status")]
    public string IntakeStatus
    {
        get
        {
            if (HasUnvalidatedHomeAddress && PickupStopId is null)
            {
                return "Address unvalidated";
            }

            if (string.IsNullOrWhiteSpace(SchoolYear))
            {
                return "School year missing";
            }

            if (DestinationId is null)
            {
                return "School unassigned";
            }

            if (!RidesAnySession)
            {
                return "Ride eligibility unstated";
            }

            return PickupStopId is null && !HasValidatedHomeCoordinates
                ? "Pickup place incomplete"
                : "Complete";
        }
    }

    // INotifyPropertyChanged implementation for Syncfusion data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
