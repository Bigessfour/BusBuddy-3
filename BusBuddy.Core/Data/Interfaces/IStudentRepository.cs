
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Student-specific repository interface
/// Extends generic repository with student-specific operations
/// </summary>
public interface IStudentRepository : IRepository<Student>
{
    // Student-specific queries
    Task<IEnumerable<Student>> GetActiveStudentsAsync();
    Task<IEnumerable<Student>> GetStudentsByGradeAsync(string grade);
    Task<IEnumerable<Student>> GetStudentsByRouteAsync(int? routeId);
    Task<IEnumerable<Student>> GetStudentsWithoutRouteAsync();
    Task<Student?> GetStudentByNameAsync(string studentName);
    Task<IEnumerable<Student>> SearchStudentsByNameAsync(string searchTerm);

    // Special needs and medical
    Task<IEnumerable<Student>> GetStudentsWithSpecialNeedsAsync();
    Task<IEnumerable<Student>> GetStudentsWithMedicalConditionsAsync();
    Task<IEnumerable<Student>> GetStudentsRequiringSpecialTransportationAsync();
    Task<IEnumerable<Student>> GetStudentsWithEmergencyContactsAsync();
    Task<IEnumerable<Student>> GetStudentsWithoutEmergencyContactsAsync();

    // Transportation and routing
    Task<IEnumerable<Student>> GetStudentsByTransportationTypeAsync(string transportationType);
    Task<IEnumerable<Student>> GetStudentsEligibleForRouteAsync(int routeId);
    Task<int> GetStudentCountByRouteAsync(int routeId);
    Task<Dictionary<string, int>> GetStudentCountByRouteAsync();

    // Demographics and statistics
    Task<int> GetTotalStudentCountAsync();
    Task<int> GetActiveStudentCountAsync();
    Task<Dictionary<string, int>> GetStudentCountByGradeAsync();
    Task<Dictionary<string, int>> GetStudentCountByTransportationTypeAsync();
    Task<IEnumerable<Student>> GetStudentsByAgeRangeAsync(int minAge, int maxAge);

    // Contact and communication
    Task<IEnumerable<Student>> GetStudentsByParentEmailAsync(string email);
    Task<IEnumerable<Student>> GetStudentsByParentPhoneAsync(string phone);
    Task<IEnumerable<Student>> GetStudentsWithIncompleteContactInfoAsync();

    // Academic and scheduling
    Task<IEnumerable<Student>> GetStudentsBySchoolAsync(string schoolName);
    Task<IEnumerable<Student>> GetStudentsWithActivityPermissionsAsync();
    Task<IEnumerable<Student>> GetStudentsWithoutActivityPermissionsAsync();

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Student> GetActiveStudents();
    IEnumerable<Student> GetStudentsByGrade(string grade);
    IEnumerable<Student> GetStudentsByRoute(int? routeId);
    IEnumerable<Student> GetStudentsWithoutRoute();
    IEnumerable<Student> GetStudentsWithSpecialNeeds();
    IEnumerable<Student> SearchStudentsByName(string searchTerm);
    int GetStudentCountByRoute(int routeId);
}
