using BusBuddy.Core.Models;
using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Repository for managing Student entities with comprehensive CRUD operations
/// </summary>
public class StudentRepository : Repository<Student>, IStudentRepository
{
    private static readonly ILogger Logger = Log.ForContext<StudentRepository>();

    public StudentRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    /// <summary>
    /// Gets all students with optional schedule information
    /// </summary>
    public async Task<List<Student>> GetAllAsync(bool includeSchedules = false)
    {
        try
        {
            Logger.Information("Retrieving all students with includeSchedules: {IncludeSchedules}", includeSchedules);

            var query = Context.Students.AsQueryable();

            if (includeSchedules)
            {
                query = query.Include(s => s.StudentSchedules);
            }

            var students = await query.ToListAsync();
            Logger.Information("Retrieved {Count} students from database", students.Count);

            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving all students");
            throw;
        }
    }

    /// <summary>
    /// Gets a student by ID with optional schedule information
    /// </summary>
    public async Task<Student?> GetByIdAsync(int id, bool includeSchedules = false)
    {
        try
        {
            Logger.Information("Retrieving student with ID: {StudentId}, includeSchedules: {IncludeSchedules}", id, includeSchedules);

            var query = Context.Students.AsQueryable();

            if (includeSchedules)
            {
                query = query.Include(s => s.StudentSchedules);
            }

            var student = await query.FirstOrDefaultAsync(s => s.StudentId == id);

            if (student != null)
            {
                Logger.Information("Found student: {StudentName} (ID: {StudentId})", student.StudentName, student.StudentId);
            }
            else
            {
                Logger.Warning("Student with ID {StudentId} not found", id);
            }

            return student;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving student with ID {StudentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new student
    /// </summary>
    public async Task<Student> CreateAsync(Student student)
    {
        try
        {
            Logger.Information("Creating new student: {StudentName}", student.StudentName);

            student.CreatedDate = DateTime.Now;
            Context.Students.Add(student);
            await Context.SaveChangesAsync();

            Logger.Information("Successfully created student: {StudentName} with ID: {StudentId}", student.StudentName, student.StudentId);

            return student;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating student: {StudentName}", student.StudentName);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing student
    /// </summary>
    public async Task<Student> UpdateAsync(Student student)
    {
        try
        {
            Logger.Information("Updating student: {StudentName} (ID: {StudentId})", student.StudentName, student.StudentId);

            student.UpdatedDate = DateTime.Now;
            Context.Students.Update(student);
            await Context.SaveChangesAsync();

            Logger.Information("Successfully updated student: {StudentName} (ID: {StudentId})", student.StudentName, student.StudentId);

            return student;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating student: {StudentName} (ID: {StudentId})", student.StudentName, student.StudentId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a student by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            Logger.Information("Deleting student with ID: {StudentId}", id);

            var student = await Context.Students.FindAsync(id);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found for deletion", id);
                return false;
            }

            Context.Students.Remove(student);
            await Context.SaveChangesAsync();

            Logger.Information("Successfully deleted student: {StudentName} (ID: {StudentId})", student.StudentName, student.StudentId);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting student with ID {StudentId}", id);
            throw;
        }
    }

    /// <summary>
    /// Searches students by name
    /// </summary>
    public async Task<List<Student>> SearchByNameAsync(string searchTerm)
    {
        try
        {
            Logger.Information("Searching students with term: {SearchTerm}", searchTerm);

            var students = await Context.Students
                .Where(s => s.StudentName.Contains(searchTerm))
                .Include(s => s.StudentSchedules)
                .ToListAsync();

            Logger.Information("Found {Count} students matching search term: {SearchTerm}", students.Count, searchTerm);

            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error searching students with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    /// <summary>
    /// Gets students by grade
    /// </summary>
    public async Task<List<Student>> GetByGradeAsync(string grade)
    {
        try
        {
            Logger.Information("Retrieving students for grade: {Grade}", grade);

            var students = await Context.Students
                .Where(s => s.Grade == grade)
                .ToListAsync();

            Logger.Information("Found {Count} students for grade: {Grade}", students.Count, grade);

            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students for grade {Grade}", grade);
            throw;
        }
    }

    /// <summary>
    /// Gets active students only
    /// </summary>
    public async Task<List<Student>> GetActiveStudentsAsync()
    {
        try
        {
            Logger.Information("Retrieving active students");

            var students = await Context.Students
                .Where(s => s.Active)
                .ToListAsync();

            Logger.Information("Found {Count} active students", students.Count);

            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving active students");
            throw;
        }
    }

    // Interface implementation for IStudentRepository
    Task<IEnumerable<Student>> IStudentRepository.GetActiveStudentsAsync() =>
        GetActiveStudentsAsync().ContinueWith(t => t.Result.AsEnumerable());

    public async Task<IEnumerable<Student>> GetStudentsByGradeAsync(string grade)
    {
        var result = await GetByGradeAsync(grade);
        return result.AsEnumerable();
    }

    public async Task<IEnumerable<Student>> GetStudentsByRouteAsync(int? routeId)
    {
        try
        {
            Logger.Information("Retrieving students for route ID: {RouteId}", routeId);

            var students = await Context.Students
                .Where(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString())
                .ToListAsync();

            Logger.Information("Found {Count} students for route ID: {RouteId}", students.Count, routeId);

            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students for route ID {RouteId}", routeId);
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsWithoutRouteAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students without routes");
            throw;
        }
    }

    public async Task<Student?> GetStudentByNameAsync(string studentName)
    {
        try
        {
            return await Context.Students.FirstOrDefaultAsync(s => s.StudentName == studentName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving student by name: {StudentName}", studentName);
            throw;
        }
    }

    public async Task<IEnumerable<Student>> SearchStudentsByNameAsync(string searchTerm)
    {
        var result = await SearchByNameAsync(searchTerm);
        return result.AsEnumerable();
    }

    public async Task<IEnumerable<Student>> GetStudentsWithSpecialNeedsAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => !string.IsNullOrEmpty(s.SpecialNeeds) || !string.IsNullOrEmpty(s.MedicalNotes))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students with special needs");
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsWithMedicalConditionsAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => !string.IsNullOrEmpty(s.MedicalNotes) || !string.IsNullOrEmpty(s.Medications))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students with medical conditions");
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsRequiringSpecialTransportationAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => !string.IsNullOrEmpty(s.TransportationNotes) || !string.IsNullOrEmpty(s.SpecialNeeds))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students requiring special transportation");
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsWithEmergencyContactsAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => !string.IsNullOrEmpty(s.EmergencyPhone))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students with emergency contacts");
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsWithoutEmergencyContactsAsync()
    {
        try
        {
            var students = await Context.Students
                .Where(s => string.IsNullOrEmpty(s.EmergencyPhone))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students without emergency contacts");
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsByTransportationTypeAsync(string transportationType)
    {
        try
        {
            var students = await Context.Students
                .Where(s => s.TransportationNotes != null && s.TransportationNotes.Contains(transportationType))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students by transportation type: {TransportationType}", transportationType);
            throw;
        }
    }

    public async Task<IEnumerable<Student>> GetStudentsEligibleForRouteAsync(int routeId)
    {
        try
        {
            var students = await Context.Students
                .Where(s => s.Active && (string.IsNullOrEmpty(s.AMRoute) || string.IsNullOrEmpty(s.PMRoute)))
                .ToListAsync();
            return students;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students eligible for route: {RouteId}", routeId);
            throw;
        }
    }

    public async Task<int> GetStudentCountByRouteAsync(int routeId)
    {
        try
        {
            return await Context.Students
                .CountAsync(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error counting students for route: {RouteId}", routeId);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetStudentCountByRouteAsync()
    {
        try
        {
            var routes = await Context.Students
                .Where(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute))
                .GroupBy(s => s.AMRoute ?? s.PMRoute)
                .Select(g => new { Route = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Route ?? "Unknown", x => x.Count);
            return routes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting student count by routes");
            throw;
        }
    }

    public async Task<int> GetTotalStudentCountAsync() => await Context.Students.CountAsync();
    public async Task<int> GetActiveStudentCountAsync() => await Context.Students.CountAsync(s => s.Active);

    public async Task<Dictionary<string, int>> GetStudentCountByGradeAsync()
    {
        return await Context.Students
            .Where(s => !string.IsNullOrEmpty(s.Grade))
            .GroupBy(s => s.Grade)
            .Select(g => new { Grade = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Grade ?? "Unknown", x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetStudentCountByTransportationTypeAsync()
    {
        return await Context.Students
            .Where(s => !string.IsNullOrEmpty(s.TransportationNotes))
            .GroupBy(s => s.TransportationNotes)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type ?? "Unknown", x => x.Count);
    }

    public async Task<IEnumerable<Student>> GetStudentsByAgeRangeAsync(int minAge, int maxAge)
    {
        var currentDate = DateTime.Now;
        var maxBirthDate = currentDate.AddYears(-minAge);
        var minBirthDate = currentDate.AddYears(-maxAge - 1);

        return await Context.Students
            .Where(s => s.DateOfBirth.HasValue && s.DateOfBirth >= minBirthDate && s.DateOfBirth <= maxBirthDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsByParentEmailAsync(string email)
    {
        return await Context.Students
            .Where(s => s.ParentGuardian != null && s.ParentGuardian.Contains(email))
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsByParentPhoneAsync(string phone)
    {
        return await Context.Students
            .Where(s => s.HomePhone == phone || s.EmergencyPhone == phone || s.AlternativePhone == phone)
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsWithIncompleteContactInfoAsync()
    {
        return await Context.Students
            .Where(s => string.IsNullOrEmpty(s.ParentGuardian) || string.IsNullOrEmpty(s.HomePhone))
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsBySchoolAsync(string schoolName)
    {
        return await Context.Students
            .Where(s => s.School == schoolName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsWithActivityPermissionsAsync()
    {
        return await Context.Students
            .Where(s => s.FieldTripPermission)
            .ToListAsync();
    }

    public async Task<IEnumerable<Student>> GetStudentsWithoutActivityPermissionsAsync()
    {
        return await Context.Students
            .Where(s => !s.FieldTripPermission)
            .ToListAsync();
    }

    // Synchronous methods for Syncfusion data binding
    public IEnumerable<Student> GetActiveStudents() => Context.Students.Where(s => s.Active);
    public IEnumerable<Student> GetStudentsByGrade(string grade) => Context.Students.Where(s => s.Grade == grade);
    public IEnumerable<Student> GetStudentsByRoute(int? routeId) => Context.Students.Where(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());
    public IEnumerable<Student> GetStudentsWithoutRoute() => Context.Students.Where(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
    public IEnumerable<Student> GetStudentsWithSpecialNeeds() => Context.Students.Where(s => !string.IsNullOrEmpty(s.SpecialNeeds));
    public IEnumerable<Student> SearchStudentsByName(string searchTerm) => Context.Students.Where(s => s.StudentName.Contains(searchTerm));
    public int GetStudentCountByRoute(int routeId) => Context.Students.Count(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());

    // IRepository<Student> implementation
    public override async Task<IEnumerable<Student>> GetAllAsync() => await GetAllAsync(false);
    public override async Task<Student?> GetByIdAsync(object id) => await GetByIdAsync((int)id, false);
    public override async Task<Student> AddAsync(Student entity) => await CreateAsync(entity);
    public async Task<bool> DeleteAsync(object id) => await DeleteAsync((int)id);
    public async Task<bool> ExistsAsync(object id) => await Context.Students.AnyAsync(s => s.StudentId == (int)id);
    public override IQueryable<Student> Query() => Context.Students.AsQueryable();
    public override async Task<int> CountAsync() => await Context.Students.CountAsync();
    public async Task SaveChangesAsync() => await Context.SaveChangesAsync();
}
