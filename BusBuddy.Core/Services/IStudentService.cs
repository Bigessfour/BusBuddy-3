
using BusBuddy.Core.Models;
using BusBuddy.Core.Data;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service interface for managing student transportation records
/// Provides CRUD operations and business logic for student management
/// </summary>
public interface IStudentService
{
    /// <summary>
    /// Gets all students from the database
    /// </summary>
    /// <returns>List of all students</returns>
    Task<List<Student>> GetAllStudentsAsync();

    /// <summary>
    /// Gets a specific student by ID
    /// </summary>
    /// <param name="studentId">The student ID</param>
    /// <returns>Student if found, null otherwise</returns>
    Task<Student?> GetStudentByIdAsync(int studentId);

    /// <summary>
    /// Gets students by grade level
    /// </summary>
    /// <param name="grade">Grade level to filter by</param>
    /// <returns>List of students in the specified grade</returns>
    Task<List<Student>> GetStudentsByGradeAsync(string grade);

    /// <summary>
    /// Gets students assigned to a specific route
    /// </summary>
    /// <param name="routeName">Route name to filter by</param>
    /// <returns>List of students on the specified route</returns>
    Task<List<Student>> GetStudentsByRouteAsync(string routeName);

    /// <summary>
    /// Gets active students only
    /// </summary>
    /// <returns>List of active students</returns>
    Task<List<Student>> GetActiveStudentsAsync();

    /// <summary>
    /// Gets students by school
    /// </summary>
    /// <param name="school">School name to filter by</param>
    /// <returns>List of students at the specified school</returns>
    Task<List<Student>> GetStudentsBySchoolAsync(string school);

    /// <summary>
    /// Searches students by name or student number
    /// </summary>
    /// <param name="searchTerm">Term to search for</param>
    /// <returns>List of matching students</returns>
    Task<List<Student>> SearchStudentsAsync(string searchTerm);

    /// <summary>
    /// Adds a new student to the database
    /// </summary>
    /// <param name="student">Student to add</param>
    /// <returns>The created student with ID</returns>
    Task<Student> AddStudentAsync(Student student);

    /// <summary>
    /// Updates an existing student
    /// </summary>
    /// <param name="student">Student to update</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdateStudentAsync(Student student);

    /// <summary>
    /// Writes only geocoded home coordinates (and PlaceId) from Address Validation.
    /// Does not run full intake validation — clerks must be able to persist lat/lng on an
    /// otherwise incomplete roster row.
    /// </summary>
    Task<bool> UpdateHomeGeocodeAsync(int studentId, decimal? latitude, decimal? longitude, string? placeId);

    /// <summary>
    /// Archives a student who may return. The row stays on the roster with Active=false.
    /// </summary>
    /// <param name="studentId">ID of the student to archive</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> ArchiveStudentAsync(int studentId);

    /// <summary>
    /// Returns an archived student to active service.
    /// </summary>
    /// <param name="studentId">ID of the student to restore</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> RestoreStudentAsync(int studentId);

    /// <summary>
    /// Permanently removes a student row after a clerk-chosen reason. specs/students.md: Mistake,
    /// Moved, or Not attending. Writes <see cref="StudentDeletionLog"/> and a Serilog entry. Related
    /// assignment and rider-exception rows are removed with the student; published routes remain.
    /// Prefer <see cref="ArchiveStudentAsync"/> when the child may return.
    /// </summary>
    /// <param name="studentId">ID of the student record to delete</param>
    /// <param name="reason">Required closed-set reason</param>
    /// <param name="notes">Optional brief clerk note (max 200 characters)</param>
    /// <returns>True if a row was removed</returns>
    Task<bool> DeleteStudentAsync(int studentId, StudentDeletionReason reason, string? notes = null);

    /// <summary>
    /// Validates student data before save
    /// </summary>
    /// <param name="student">Student to validate</param>
    /// <returns>List of validation errors, empty if valid</returns>
    Task<List<string>> ValidateStudentAsync(Student student);

    /// <summary>
    /// Gets student count statistics
    /// </summary>
    /// <returns>Dictionary with count statistics</returns>
    Task<Dictionary<string, int>> GetStudentStatisticsAsync();

    /// <summary>
    /// Assigns a student to a route
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="amRoute">AM route name</param>
    /// <param name="pmRoute">PM route name</param>
    /// <returns>True if successful</returns>
    Task<bool> AssignStudentToRouteAsync(int studentId, string? amRoute, string? pmRoute);

    /// <summary>
    /// Updates student active status
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="isActive">New active status</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateStudentActiveStatusAsync(int studentId, bool isActive);

    /// <summary>
    /// Gets students whose intake is incomplete — missing required fields, no school destination, or
    /// an address that has never produced coordinates. Backs the "incomplete records" surface that
    /// specs/students.md requires for unvalidated addresses.
    /// </summary>
    /// <returns>List of active students with incomplete data</returns>
    Task<List<Student>> GetStudentsWithMissingInfoAsync();

    /// <summary>
    /// Exports student data to CSV format
    /// </summary>
    /// <returns>CSV string containing student data</returns>
    Task<string> ExportStudentsToCsvAsync();

    /// <summary>
    /// Updates a student's address information
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="homeAddress">Home address</param>
    /// <param name="city">City</param>
    /// <param name="state">State (2-letter abbreviation)</param>
    /// <param name="zip">ZIP code</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateStudentAddressAsync(int studentId, string homeAddress, string city, string state, string zip);

#if DEBUG
    /// <summary>
    /// Provides detailed diagnostic information about a student record
    /// Only available in DEBUG builds
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <returns>Dictionary with diagnostic information</returns>
    Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(int studentId);

    /// <summary>
    /// Provides student data operation metrics for system diagnostics
    /// Only available in DEBUG builds
    /// </summary>
    /// <returns>Dictionary with operation metrics</returns>
    Task<Dictionary<string, object>> GetStudentOperationMetricsAsync();
#endif

}

/// <summary>
/// Result of a data seeding operation. Kept here (rather than removed with the retired
/// <c>SeedDistrictDataAsync</c>) because it is the shared seed-outcome DTO for the namespace.
/// </summary>
public class SeedResult
{
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsSeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; }
}
