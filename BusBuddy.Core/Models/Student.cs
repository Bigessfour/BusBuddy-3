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
  // For compatibility with legacy and ViewModel code
  [NotMapped]
  public int? RouteId {
    get => RouteAssignmentId;
    set => RouteAssignmentId = value;
  }
  [Key]
  public int StudentId { get; set; }

  [Required(ErrorMessage = "Student name is required")]
  [StringLength(100, ErrorMessage = "Student name cannot exceed 100 characters")]
  [Display(Name = "Student Name")]
  public string StudentName { get; set; } = string.Empty;

  // Optional for MVP: family association can be added later during edit
  public int? FamilyId { get; set; }
  public Family? Family { get; set; }
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
  public string? HomeAddress { get; set; }

  // Geo coordinates for mapping (WGS84). Use decimal for SQL precision.
  [Column(TypeName = "decimal(10,8)")]
  [Display(Name = "Latitude")]
  public decimal? Latitude { get; set; }

  [Column(TypeName = "decimal(11,8)")]
  [Display(Name = "Longitude")]
  public decimal? Longitude { get; set; }

  [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
  [Display(Name = "City")]
  public string? City { get; set; }

  [StringLength(2, ErrorMessage = "State cannot exceed 2 characters")]
  [Display(Name = "State")]
  public string? State { get; set; }

  [StringLength(10, ErrorMessage = "ZIP code cannot exceed 10 characters")]
  [Display(Name = "Zip Code")]
  public string? Zip { get; set; }

  [StringLength(20, ErrorMessage = "Home phone cannot exceed 20 characters")]
  [Display(Name = "Home Phone")]
  public string? HomePhone { get; set; }

  [StringLength(100, ErrorMessage = "Parent/Guardian name cannot exceed 100 characters")]
  [Display(Name = "Parent/Guardian")]
  public string? ParentGuardian { get; set; }

  [StringLength(20, ErrorMessage = "Emergency phone cannot exceed 20 characters")]
  [Display(Name = "Emergency Phone")]
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

  public bool Active { get; set; } = true;

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

  // INotifyPropertyChanged implementation for Syncfusion data binding
  public event PropertyChangedEventHandler? PropertyChanged;

  protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
  {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
