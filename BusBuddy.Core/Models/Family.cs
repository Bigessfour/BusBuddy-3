using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a family unit for importing student data from forms
/// This model maps to the structure of extracted form data JSON
/// </summary>
/// <summary>
/// Represents a family unit for importing student data from forms
/// This model maps to the structure of extracted form data JSON
/// </summary>
[Table("Families")]
public class Family
{
    [Key]
    public int FamilyId { get; set; }

    [StringLength(100, ErrorMessage = "Parent/Guardian name cannot exceed 100 characters")]
    [Display(Name = "Parent/Guardian")]
    public string ParentGuardian { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "County cannot exceed 50 characters")]
    [Display(Name = "County")]
    public string County { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Home phone cannot exceed 20 characters")]
    [Display(Name = "Home Phone")]
    public string? HomePhone { get; set; }

    [StringLength(20, ErrorMessage = "Cell phone cannot exceed 20 characters")]
    [Display(Name = "Cell Phone")]
    public string? CellPhone { get; set; }

    [StringLength(100, ErrorMessage = "Emergency contact cannot exceed 100 characters")]
    [Display(Name = "Emergency Contact")]
    public string? EmergencyContact { get; set; }

    [StringLength(100, ErrorMessage = "Joint parent cannot exceed 100 characters")]
    [Display(Name = "Joint Parent")]
    public string? JointParent { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100, ErrorMessage = "Created by cannot exceed 100 characters")]
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [StringLength(100, ErrorMessage = "Updated by cannot exceed 100 characters")]
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
    public virtual ICollection<Guardian> Guardians { get; set; } = new List<Guardian>();
}

/// <summary>
/// DTO for importing student data from JSON forms
/// Maps to the structure expected from extracted form data
/// </summary>
public class StudentImportDto
{
    // Removed JsonPropertyName
    public string FirstName { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string LastName { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string Grade { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string FullTime { get; set; } = string.Empty; // e.g., "AM_PM"

    // Removed JsonPropertyName
    public string? Infrequently { get; set; }

    // Removed JsonPropertyName
    public DateTime? DateOfBirth { get; set; }

    // Removed JsonPropertyName
    public bool SpecialNeeds { get; set; }

    // Removed JsonPropertyName
    public string? MedicalNotes { get; set; }

    // Removed JsonPropertyName
    public string? Allergies { get; set; }

    // Removed JsonPropertyName
    public string? Medications { get; set; }

    // Removed JsonPropertyName
    public string? TransportationNotes { get; set; }
}

/// <summary>
/// DTO for importing family data from JSON forms
/// </summary>
public class FamilyImportDto
{
    // Removed JsonPropertyName
    public string ParentGuardian { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string Address { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string City { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string County { get; set; } = string.Empty;

    // Removed JsonPropertyName
    public string? HomePhone { get; set; }

    // Removed JsonPropertyName
    public string? CellPhone { get; set; }

    // Removed JsonPropertyName
    public string? EmergencyContact { get; set; }

    // Removed JsonPropertyName
    public string? JointParent { get; set; }

    // Removed JsonPropertyName
    public List<StudentImportDto> Students { get; set; } = new List<StudentImportDto>();
}
