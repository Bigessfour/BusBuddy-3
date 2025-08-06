
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
    /// Deletes a student from the database
    /// </summary>
    /// <param name="studentId">ID of the student to delete</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteStudentAsync(int studentId);

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
    /// Gets students with missing required information
    /// </summary>
    /// <returns>List of students with incomplete data</returns>
    Task<List<Student>> GetStudentsWithMissingInfoAsync();

    /// <summary>
    /// Exports student data to CSV format
    /// </summary>
    /// <returns>CSV string containing student data</returns>
    Task<string> ExportStudentsToCsvAsync();

    /// <summary>
    /// Assigns a student to a specific bus stop
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="busStop">Bus stop name</param>
    /// <returns>True if successful</returns>
    Task<bool> AssignStudentToBusStopAsync(int studentId, string? busStop);

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

    /// <summary>
    /// Updates a student's primary contact information
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="parentGuardian">Parent or guardian name</param>
    /// <param name="homePhone">Home phone number</param>
    /// <param name="emergencyPhone">Emergency phone number</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateStudentContactInfoAsync(int studentId, string parentGuardian, string homePhone, string emergencyPhone);

    /// <summary>
    /// Updates a student's emergency contact information
    /// </summary>
    /// <param name="studentId">Student ID</param>
    /// <param name="alternativeContact">Alternative contact name</param>
    /// <param name="alternativePhone">Alternative contact phone</param>
    /// <param name="doctorName">Doctor name</param>
    /// <param name="doctorPhone">Doctor phone</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateEmergencyContactAsync(int studentId, string alternativeContact, string alternativePhone, string doctorName, string doctorPhone);

    /// <summary>
    /// Gets students assigned to a specific route by route ID and context
    /// </summary>
    /// <param name="context">Database context</param>
    /// <param name="routeId">Route ID</param>
    /// <returns>List of students assigned to the route</returns>
    Task<List<Student>> GetStudentsForRouteAsync(BusBuddyDbContext context, int routeId);

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

    /// <summary>
    /// Seeds student data from Wiley School District registration forms
    /// </summary>
    /// <returns>Result of the seeding operation</returns>
    Task<SeedResult> SeedWileySchoolDistrictDataAsync();
}

/// <summary>
/// Result of a data seeding operation
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
