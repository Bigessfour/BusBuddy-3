using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using System.IO;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text;
using System.Linq; // Added for FirstOrDefault in seeding path resolution

namespace BusBuddy.Core.Services;

/// <summary>
/// Service implementation for managing student transportation records
/// Provides CRUD operations and business logic for student management
/// </summary>
public class StudentService : IStudentService
{
    private static readonly ILogger Logger = Log.ForContext<StudentService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public StudentService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    #region Read Operations

    public async Task<List<Student>> GetAllStudentsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            Logger.Information("Retrieving all students from database");
            var context = _contextFactory.CreateDbContext();
            try
            {
                return await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                // Properly dispose the context when done
                await context.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving all students");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Student?> GetStudentByIdAsync(int studentId)
    {
        try
        {
            Logger.Information("Retrieving student with ID: {StudentId}", studentId);
            var context = _contextFactory.CreateDbContext();
            try
            {
                return await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);
            }
            finally
            {
                // Properly dispose the context when done
                await context.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving student with ID: {StudentId}", studentId);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsByGradeAsync(string grade)
    {
        try
        {
            Logger.Information("Retrieving students in grade: {Grade}", grade);
            var context = _contextFactory.CreateDbContext();
            try
            {
                return await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .Where(s => s.Grade == grade)
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                // Properly dispose the context when done
                await context.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students by grade: {Grade}", grade);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsByRouteAsync(string routeName)
    {
        try
        {
            Logger.Information("Retrieving students on route: {RouteName}", routeName);
            // Don't dispose the context here as it might be needed after the method returns
            var context = _contextFactory.CreateDbContext();
            return await context.Students
                .Where(s => s.AMRoute == routeName || s.PMRoute == routeName)
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students by route: {RouteName}", routeName);
            throw;
        }
    }

    public async Task<List<Student>> GetActiveStudentsAsync()
    {
        try
        {
            Logger.Information("Retrieving active students");
            // Don't dispose the context here as it might be needed after the method returns
            var context = _contextFactory.CreateDbContext();
            return await context.Students
                .Where(s => s.Active)
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving active students");
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsBySchoolAsync(string school)
    {
        try
        {
            Logger.Information("Retrieving students from school: {School}", school);
            // Don't dispose the context here as it might be needed after the method returns
            var context = _contextFactory.CreateDbContext();
            return await context.Students
                .Where(s => s.School == school)
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students by school: {School}", school);
            throw;
        }
    }

    public async Task<List<Student>> SearchStudentsAsync(string searchTerm)
    {
        try
        {
            Logger.Information("Searching students with term: {SearchTerm}", searchTerm);

            // Don't dispose the context here as it might be needed after the method returns
            var context = _contextFactory.CreateDbContext();
            return await context.Students
                .Where(s => s.StudentName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           (s.StudentNumber != null && s.StudentNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error searching students with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsForRouteAsync(BusBuddyDbContext context, int routeId)
    {
        try
        {
            Logger.Information("Retrieving students for route ID: {RouteId}", routeId);
            // Find route name for the given routeId
            var route = await context.Routes.FindAsync(routeId);
            if (route == null || string.IsNullOrEmpty(route.RouteName))
            {
                return new List<Student>();
            }

            var routeName = route.RouteName;
            return await context.Students
                .Where(s => s.AMRoute == routeName || s.PMRoute == routeName)
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students for route ID: {RouteId}", routeId);
            return new List<Student>();
        }
    }

    #endregion

    #region Write Operations

    public async Task<Student> AddStudentAsync(Student student)
    {
        try
        {
            Logger.Information("Adding new student: {StudentName}", student.StudentName);

            // Validate student data
            var validationErrors = await ValidateStudentAsync(student);
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            // Set default values
            if (student.EnrollmentDate == null)
            {
                student.EnrollmentDate = DateTime.Today;
            }

            using var context = _contextFactory.CreateWriteDbContext();
            context.Students.Add(student);
            await context.SaveChangesAsync();

            Logger.Information("Successfully added student with ID: {StudentId}", student.StudentId);
            return student;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding student: {StudentName}", student.StudentName);
            throw;
        }
    }

    public async Task<bool> UpdateStudentAsync(Student student)
    {
        try
        {
            Logger.Information("Updating student with ID: {StudentId}", student.StudentId);

            // Validate student data
            var validationErrors = await ValidateStudentAsync(student);
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            using var context = _contextFactory.CreateWriteDbContext();
            context.Students.Update(student);
            var result = await context.SaveChangesAsync();

            var success = result > 0;
            if (success)
            {
                Logger.Information("Successfully updated student: {StudentName}", student.StudentName);
            }
            else
            {
                Logger.Warning("No changes were made when updating student: {StudentId}", student.StudentId);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating student with ID: {StudentId}", student.StudentId);
            throw;
        }
    }

    public async Task<bool> DeleteStudentAsync(int studentId)
    {
        try
        {
            Logger.Information("Deleting student with ID: {StudentId}", studentId);

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student != null)
            {
                context.Students.Remove(student);
                var result = await context.SaveChangesAsync();

                var success = result > 0;
                if (success)
                {
                    Logger.Information("Successfully deleted student: {StudentName}", student.StudentName);
                }

                return success;
            }

            Logger.Warning("Student with ID {StudentId} not found for deletion", studentId);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting student with ID: {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Validation and Business Logic

    public async Task<List<string>> ValidateStudentAsync(Student student)
    {
        var errors = new List<string>();

        try
        {
            // Required field validation
            if (string.IsNullOrWhiteSpace(student.StudentName))
            {
                errors.Add("Student name is required");
            }

            // Create a context that will be disposed at the end of this method,
            // since the validation results are returned as a new list (not dependent on the context)
            using var context = _contextFactory.CreateDbContext();

            // Student number uniqueness check (if provided)
            if (!string.IsNullOrWhiteSpace(student.StudentNumber))
            {
                var existingStudent = await context.Students
                    .Where(s => s.StudentNumber == student.StudentNumber && s.StudentId != student.StudentId)
                    .FirstOrDefaultAsync();

                if (existingStudent != null)
                {
                    errors.Add($"Student number '{student.StudentNumber}' is already in use");
                }
            }

            // Grade validation
            if (!string.IsNullOrWhiteSpace(student.Grade))
            {
                var validGrades = new[] { "Pre-K", "K", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };
                if (!validGrades.Contains(student.Grade))
                {
                    errors.Add("Invalid grade level");
                }
            }

            // Phone number format validation
            if (!string.IsNullOrWhiteSpace(student.HomePhone))
            {
                var phonePattern = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(student.HomePhone, phonePattern))
                {
                    errors.Add("Invalid home phone number format");
                }
            }

            if (!string.IsNullOrWhiteSpace(student.EmergencyPhone))
            {
                var phonePattern = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(student.EmergencyPhone, phonePattern))
                {
                    errors.Add("Invalid emergency phone number format");
                }
            }

            // State validation
            if (!string.IsNullOrWhiteSpace(student.State))
            {
                if (student.State.Length != 2)
                {
                    errors.Add("State must be a 2-letter abbreviation");
                }
            }

            // ZIP code validation
            if (!string.IsNullOrWhiteSpace(student.Zip))
            {
                var zipPattern = @"^\d{5}(-\d{4})?$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(student.Zip, zipPattern))
                {
                    errors.Add("Invalid ZIP code format");
                }
            }

            // Route validation (if routes exist in database)
            if (!string.IsNullOrWhiteSpace(student.AMRoute))
            {
                try
                {
                    var amRouteExists = await context.Routes.AnyAsync(r => r.RouteName == student.AMRoute);
                    if (!amRouteExists)
                    {
                        errors.Add($"AM Route '{student.AMRoute}' does not exist");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error validating AM route: {AMRoute}", student.AMRoute);
                    errors.Add($"AM Route '{student.AMRoute}' does not exist");
                }
            }

            if (!string.IsNullOrWhiteSpace(student.PMRoute))
            {
                try
                {
                    var pmRouteExists = await context.Routes.AnyAsync(r => r.RouteName == student.PMRoute);
                    if (!pmRouteExists)
                    {
                        errors.Add($"PM Route '{student.PMRoute}' does not exist");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error validating PM route: {PMRoute}", student.PMRoute);
                    errors.Add($"PM Route '{student.PMRoute}' does not exist");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during basic student validation");
            errors.Add("Validation error occurred");
        }

        return errors;
    }

    #endregion

    #region Statistics and Reporting

    public async Task<Dictionary<string, int>> GetStudentStatisticsAsync()
    {
        try
        {
            Logger.Information("Calculating student statistics");

            using var context = _contextFactory.CreateDbContext();
            var stats = new Dictionary<string, int>
            {
                ["TotalStudents"] = await context.Students.CountAsync(),
                ["ActiveStudents"] = await context.Students.CountAsync(s => s.Active),
                ["InactiveStudents"] = await context.Students.CountAsync(s => !s.Active),
                ["StudentsWithRoutes"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute)),
                ["StudentsWithoutRoutes"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute))
            };

            // Grade level counts
            var gradeCounts = await context.Students
                .Where(s => !string.IsNullOrEmpty(s.Grade))
                .GroupBy(s => s.Grade)
                .Select(g => new { Grade = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var gradeCount in gradeCounts)
            {
                stats[$"Grade_{gradeCount.Grade}"] = gradeCount.Count;
            }

            return stats;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error calculating student statistics");
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsWithMissingInfoAsync()
    {
        try
        {
            Logger.Information("Finding students with missing required information");

            using var context = _contextFactory.CreateDbContext();
            return await context.Students
                .Where(s => string.IsNullOrEmpty(s.ParentGuardian) ||
                           string.IsNullOrEmpty(s.EmergencyPhone) ||
                           string.IsNullOrEmpty(s.HomeAddress) ||
                           string.IsNullOrEmpty(s.Grade))
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error finding students with missing information");
            throw;
        }
    }

    #endregion

    #region Route Assignment

    public async Task<bool> AssignStudentToRouteAsync(int studentId, string? amRoute, string? pmRoute)
    {
        try
        {
            Logger.Information("Assigning student {StudentId} to routes - AM: {AMRoute}, PM: {PMRoute}",
                studentId, amRoute, pmRoute);

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.AMRoute = amRoute;
            student.PMRoute = pmRoute;

            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully assigned routes for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning routes for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> AssignStudentToBusStopAsync(int studentId, string? busStop)
    {
        try
        {
            Logger.Information("Assigning student {StudentId} to bus stop: {BusStop}", studentId, busStop);

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.BusStop = busStop;

            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully assigned bus stop for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error assigning bus stop for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> UpdateStudentActiveStatusAsync(int studentId, bool isActive)
    {
        try
        {
            Logger.Information("Updating active status for student {StudentId} to {IsActive}", studentId, isActive);

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.Active = isActive;
            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully updated active status for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating active status for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<RouteAssignmentResult> AssignStudentsToRoutesAsync(BusBuddyDbContext context, IEnumerable<Student> students, IEnumerable<Route> routes, BusService busService)
    {
        var updatedStudents = new List<Student>();
        var newAssignments = new List<RouteAssignment>();

        foreach (var student in students)
        {
            // Example: If address contains "east of Hwy 287", assign East Route
            var route = routes.FirstOrDefault(r => student.HomeAddress != null && r.Boundaries != null && IsAddressInRouteBoundary(student.HomeAddress, r.Boundaries));
            if (route == null)
            {
                // Log and skip if no route matches
                continue;
            }

            // Find bus for route
            var bus = await context.Buses.FirstOrDefaultAsync(v => v.Make == route.RouteName || v.BusNumber == route.RouteName || v.BusNumber == route.RouteName.Replace(" Route", ""));
            if (bus == null)
            {
                continue;
            }

            // Check bus capacity
            var assignedCount = await busService.GetAssignedStudentCountAsync(context, bus.VehicleId);
            if (assignedCount >= bus.SeatingCapacity)
            {
                continue;
            }

            // Create assignment
            var assignment = new RouteAssignment
            {
                RouteId = route.RouteId,
                VehicleId = bus.VehicleId,
                AssignmentDate = System.DateTime.Today
            };
            newAssignments.Add(assignment);

            student.RouteAssignmentId = assignment.RouteAssignmentId;
            student.BusStop = "Assigned by address";
            updatedStudents.Add(student);
        }

        return new RouteAssignmentResult
        {
            UpdatedStudents = updatedStudents,
            NewAssignments = newAssignments
        };
    }

    private bool IsAddressInRouteBoundary(string address, string boundaries)
    {
        // Simple example: match keywords (expand as needed)
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(boundaries)) { return false; }
        address = address.ToLower();
        boundaries = boundaries.ToLower();
        if (boundaries.Contains("east") && address.Contains("east")) { return true; }
        if (boundaries.Contains("west") && address.Contains("west")) { return true; }
        if (boundaries.Contains("south") && address.Contains("south")) { return true; }
        if (boundaries.Contains("north") && address.Contains("north")) { return true; }
        // Add more logic as needed
        return false;
    }

    public class RouteAssignmentResult
    {
        public List<Student> UpdatedStudents { get; set; } = new();
        public List<RouteAssignment> NewAssignments { get; set; } = new();
    }

    #endregion

    #region Address and Contact Management

    public async Task<bool> UpdateStudentAddressAsync(int studentId, string homeAddress, string city, string state, string zip)
    {
        try
        {
            Logger.Information("Updating address information for student {StudentId}", studentId);

            // Validate address format
            var addressValidation = ValidateAddress(homeAddress, city, state, zip);
            if (!addressValidation.IsValid)
            {
                throw new ArgumentException($"Address validation failed: {addressValidation.ErrorMessage}");
            }

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.HomeAddress = homeAddress;
            student.City = city;
            student.State = state;
            student.Zip = zip;

            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully updated address for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating address for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> UpdateStudentContactInfoAsync(int studentId, string parentGuardian, string homePhone, string emergencyPhone)
    {
        try
        {
            Logger.Information("Updating contact information for student {StudentId}", studentId);

            // Validate phone number formats
            var phonePattern = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";
            if (!string.IsNullOrWhiteSpace(homePhone) && !System.Text.RegularExpressions.Regex.IsMatch(homePhone, phonePattern))
            {
                throw new ArgumentException("Invalid home phone number format");
            }

            if (!string.IsNullOrWhiteSpace(emergencyPhone) && !System.Text.RegularExpressions.Regex.IsMatch(emergencyPhone, phonePattern))
            {
                throw new ArgumentException("Invalid emergency phone number format");
            }

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.ParentGuardian = parentGuardian;
            student.HomePhone = homePhone;
            student.EmergencyPhone = emergencyPhone;

            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully updated contact information for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating contact information for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> UpdateEmergencyContactAsync(int studentId, string alternativeContact, string alternativePhone, string doctorName, string doctorPhone)
    {
        try
        {
            Logger.Information("Updating emergency contact information for student {StudentId}", studentId);

            // Validate phone number formats
            var phonePattern = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";
            if (!string.IsNullOrWhiteSpace(alternativePhone) && !System.Text.RegularExpressions.Regex.IsMatch(alternativePhone, phonePattern))
            {
                throw new ArgumentException("Invalid alternative contact phone number format");
            }

            if (!string.IsNullOrWhiteSpace(doctorPhone) && !System.Text.RegularExpressions.Regex.IsMatch(doctorPhone, phonePattern))
            {
                throw new ArgumentException("Invalid doctor phone number format");
            }

            using var context = _contextFactory.CreateWriteDbContext();
            var student = await context.Students.FindAsync(studentId);
            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found", studentId);
                return false;
            }

            student.AlternativeContact = alternativeContact;
            student.AlternativePhone = alternativePhone;
            student.DoctorName = doctorName;
            student.DoctorPhone = doctorPhone;

            var result = await context.SaveChangesAsync();
            var success = result > 0;

            if (success)
            {
                Logger.Information("Successfully updated emergency contact information for student: {StudentName}", student.StudentName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating emergency contact information for student {StudentId}", studentId);
            throw;
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidateAddress(string address, string city, string state, string zip)
    {
        // Implement basic address validation
        if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Address cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return (false, "City cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(state) || state.Length != 2)
        {
            return (false, "State must be a 2-letter abbreviation");
        }

        var zipPattern = @"^\d{5}(-\d{4})?$";
        if (string.IsNullOrWhiteSpace(zip) || !System.Text.RegularExpressions.Regex.IsMatch(zip, zipPattern))
        {
            return (false, "Invalid ZIP code format");
        }

        // In a real implementation, you might also validate against an address verification service
        // For now, we'll just return valid if basic checks pass
        return (true, null);
    }

    #endregion

    #region Export

    public async Task<string> ExportStudentsToCsvAsync()
    {
        try
        {
            Logger.Information("Exporting students to CSV format");

            var students = await GetAllStudentsAsync();
            var csv = new StringBuilder();

            // CSV Header
            csv.AppendLine("Student ID,Student Number,Student Name,Grade,School,Home Address,City,State,ZIP," +
                          "Home Phone,Parent/Guardian,Emergency Phone,AM Route,PM Route,Bus Stop," +
                          "Medical Notes,Transportation Notes,Active,Enrollment Date");

            // CSV Data
            foreach (var student in students)
            {
                csv.AppendLine($"{student.StudentId}," +
                              $"\"{student.StudentNumber ?? ""}\"," +
                              $"\"{student.StudentName}\"," +
                              $"\"{student.Grade ?? ""}\"," +
                              $"\"{student.School ?? ""}\"," +
                              $"\"{student.HomeAddress ?? ""}\"," +
                              $"\"{student.City ?? ""}\"," +
                              $"\"{student.State ?? ""}\"," +
                              $"\"{student.Zip ?? ""}\"," +
                              $"\"{student.HomePhone ?? ""}\"," +
                              $"\"{student.ParentGuardian ?? ""}\"," +
                              $"\"{student.EmergencyPhone ?? ""}\"," +
                              $"\"{student.AMRoute ?? ""}\"," +
                              $"\"{student.PMRoute ?? ""}\"," +
                              $"\"{student.BusStop ?? ""}\"," +
                              $"\"{student.MedicalNotes ?? ""}\"," +
                              $"\"{student.TransportationNotes ?? ""}\"," +
                              $"{student.Active}," +
                              $"{student.EnrollmentDate?.ToString("yyyy-MM-dd") ?? ""}");
            }

            Logger.Information("Successfully exported {Count} students to CSV", students.Count);
            return csv.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting students to CSV");
            throw;
        }
    }

    #endregion

    #region DEBUG Instrumentation

#if DEBUG
    /// <summary>
    /// Provides detailed diagnostic information about a student record
    /// Only available in DEBUG builds
    /// </summary>
    public async Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(int studentId)
    {
        try
        {
            Logger.Debug("Retrieving diagnostic information for student {StudentId}", studentId);

            using var context = _contextFactory.CreateDbContext();
            var student = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found for diagnostics", studentId);
                return new Dictionary<string, object> { { "Error", "Student not found" } };
            }

            // Create a comprehensive diagnostic report
            var diagnostics = new Dictionary<string, object>
            {
                { "StudentId", student.StudentId },
                { "StudentName", student.StudentName },
                { "RecordCreationTime", student.CreatedDate },
                { "LastUpdateTime", student.UpdatedDate ?? DateTime.MinValue },
                { "RecordAgeInDays", (DateTime.UtcNow - student.CreatedDate).TotalDays },
                { "RecordCompleteness", CalculateRecordCompleteness(student) },
                { "HasRequiredFields", !string.IsNullOrEmpty(student.ParentGuardian) &&
                                      !string.IsNullOrEmpty(student.EmergencyPhone) &&
                                      !string.IsNullOrEmpty(student.HomeAddress) &&
                                      !string.IsNullOrEmpty(student.Grade) },
                { "HasRouteAssignment", !string.IsNullOrEmpty(student.AMRoute) || !string.IsNullOrEmpty(student.PMRoute) },
                { "HasBusStopAssignment", !string.IsNullOrEmpty(student.BusStop) },
                { "HasMedicalNotes", !string.IsNullOrEmpty(student.MedicalNotes) },
                { "HasSpecialNeeds", student.SpecialNeeds },
                { "HasTransportationNotes", !string.IsNullOrEmpty(student.TransportationNotes) },
                { "IsActive", student.Active },
                { "ModelState", SerializeStudentForDiagnostics(student) }
            };

            // Add related data counts
            try
            {
                // Just check if there are any related entries in other tables
                // This would need to be adjusted based on your actual data model
                diagnostics.Add("HasRelatedRecords", false);
            }
            catch (Exception ex)
            {
                diagnostics.Add("RelatedDataCountError", ex.Message);
            }

            return diagnostics;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating diagnostics for student {StudentId}", studentId);
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    /// <summary>
    /// Calculates the completeness percentage of a student record
    /// Only available in DEBUG builds
    /// </summary>
    private double CalculateRecordCompleteness(Student student)
    {
        var requiredFields = new[]
        {
            student.StudentName,
            student.Grade,
            student.School,
            student.HomeAddress,
            student.City,
            student.State,
            student.Zip,
            student.HomePhone,
            student.ParentGuardian,
            student.EmergencyPhone
        };

        var optionalFields = new[]
        {
            student.StudentNumber,
            student.AMRoute,
            student.PMRoute,
            student.BusStop,
            student.MedicalNotes,
            student.TransportationNotes,
            student.DateOfBirth.HasValue ? "HasValue" : null,
            student.Gender,
            student.PickupAddress,
            student.DropoffAddress,
            student.SpecialAccommodations,
            student.Allergies,
            student.Medications,
            student.DoctorName,
            student.DoctorPhone,
            student.AlternativeContact,
            student.AlternativePhone
        };

        // Calculate completeness (required fields have more weight)
        var requiredFieldsCount = requiredFields.Length;
        var filledRequiredFieldsCount = requiredFields.Count(f => !string.IsNullOrWhiteSpace(f));

        var optionalFieldsCount = optionalFields.Length;
        var filledOptionalFieldsCount = optionalFields.Count(f => !string.IsNullOrWhiteSpace(f));

        var requiredCompleteness = filledRequiredFieldsCount / (double)requiredFieldsCount;
        var optionalCompleteness = filledOptionalFieldsCount / (double)optionalFieldsCount;

        // Weight required fields as 70% of total score, optional as 30%
        return (requiredCompleteness * 0.7) + (optionalCompleteness * 0.3);
    }

    /// <summary>
    /// Serializes a student object for diagnostic viewing
    /// Only available in DEBUG builds
    /// </summary>
    private object SerializeStudentForDiagnostics(Student student)
    {
        return new
        {
            // Basic Info
            student.StudentId,
            student.StudentName,
            student.StudentNumber,
            student.Grade,
            student.School,

            // Contact Info
            Address = new
            {
                student.HomeAddress,
                student.City,
                student.State,
                student.Zip
            },
            Contact = new
            {
                student.HomePhone,
                student.ParentGuardian,
                student.EmergencyPhone
            },
            EmergencyContacts = new
            {
                student.AlternativeContact,
                student.AlternativePhone,
                student.DoctorName,
                student.DoctorPhone
            },

            // Transportation Info
            TransportationDetails = new
            {
                student.AMRoute,
                student.PMRoute,
                student.BusStop,
                student.PickupAddress,
                student.DropoffAddress,
                student.TransportationNotes
            },

            // Medical Info
            MedicalDetails = new
            {
                student.MedicalNotes,
                student.SpecialNeeds,
                student.SpecialAccommodations,
                student.Allergies,
                student.Medications
            },

            // Status Info
            StatusInfo = new
            {
                student.Active,
                student.EnrollmentDate,
                student.CreatedDate,
                student.UpdatedDate,
                student.CreatedBy,
                student.UpdatedBy
            }
        };
    }

    /// <summary>
    /// Provides student data operation metrics for system diagnostics
    /// Only available in DEBUG builds
    /// </summary>
    public async Task<Dictionary<string, object>> GetStudentOperationMetricsAsync()
    {
        try
        {
            Logger.Debug("Retrieving student operation metrics");

            var metrics = new Dictionary<string, object>();
            using var context = _contextFactory.CreateDbContext();

            // Student Record Metrics
            metrics["TotalStudentCount"] = await context.Students.CountAsync();
            metrics["ActiveStudentCount"] = await context.Students.CountAsync(s => s.Active);
            metrics["InactiveStudentCount"] = await context.Students.CountAsync(s => !s.Active);
            metrics["StudentsWithRoutes"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithoutRoutes"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithBusStops"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithoutBusStops"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithSpecialNeeds"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.SpecialNeeds));

            // Database Performance Metrics
            var sw = new System.Diagnostics.Stopwatch();

            sw.Start();
            await context.Students.AsNoTracking().ToListAsync();
            sw.Stop();
            metrics["AllStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => s.Active).ToListAsync();
            sw.Stop();
            metrics["ActiveStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => !string.IsNullOrEmpty(s.AMRoute)).ToListAsync();
            sw.Stop();
            metrics["StudentsWithAMRouteQueryTimeMs"] = sw.ElapsedMilliseconds;

            // Record Completeness Distribution
            var students = await context.Students.AsNoTracking().ToListAsync();
            var completenessScores = new List<double>();

            foreach (var student in students)
            {
                completenessScores.Add(CalculateRecordCompleteness(student));
            }

            metrics["AverageRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Average() : 0;
            metrics["MaxRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Max() : 0;
            metrics["MinRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Min() : 0;

            // Group completeness into ranges
            var completenessDistribution = new Dictionary<string, int>
            {
                { "0-25%", completenessScores.Count(s => s >= 0 && s < 0.25) },
                { "25-50%", completenessScores.Count(s => s >= 0.25 && s < 0.5) },
                { "50-75%", completenessScores.Count(s => s >= 0.5 && s < 0.75) },
                { "75-100%", completenessScores.Count(s => s >= 0.75 && s <= 1.0) }
            };

            metrics["CompletenessDistribution"] = completenessDistribution;

            return metrics;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating student operation metrics");
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }
#endif

    #endregion

    #region Data Seeding

    /// <summary>
    /// Seeds student data from Wiley School District registration forms
    /// Uses resilient execution patterns for reliable data import
    /// </summary>
    public async Task<SeedResult> SeedWileySchoolDistrictDataAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int recordsSeeded = 0;
        try
        {
            Logger.Information("Starting Wiley School District data seeding operation");
            // Updated path resolution: try multiple documented candidate locations
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDir, "Data", "wiley-school-district-data.json"),
                Path.Combine(baseDir, "BusBuddy.Core", "Data", "wiley-school-district-data.json"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "BusBuddy.Core", "Data", "wiley-school-district-data.json"))
            };
            var jsonPath = candidatePaths.FirstOrDefault(File.Exists) ?? string.Empty;
            if (string.IsNullOrEmpty(jsonPath))
            {
                Logger.Error("Wiley JSON file not found. Paths tried: {Paths}", string.Join(" | ", candidatePaths));
                return new SeedResult { Success = false, ErrorMessage = "JSON file not found", RecordsSeeded = 0, Duration = stopwatch.Elapsed, CompletedAt = DateTime.UtcNow };
            }
            var json = await File.ReadAllTextAsync(jsonPath);
            var wileyData = System.Text.Json.JsonSerializer.Deserialize<WileyDataRoot>(json);
            if (wileyData == null || wileyData.families == null)
            {
                Logger.Error("Wiley JSON deserialization failed");
                return new SeedResult { Success = false, ErrorMessage = "JSON deserialization failed", RecordsSeeded = 0, Duration = stopwatch.Elapsed, CompletedAt = DateTime.UtcNow };
            }
            var studentsToSeed = wileyData.families.Take(5).Select((fam, idx) => new Student {
                StudentName = fam.parentGuardian + " Child",
                FamilyId = fam.id,
                HomeAddress = fam.address,
                City = fam.city,
                State = fam.state,
                ParentGuardian = fam.parentGuardian,
                HomePhone = fam.homePhone,
                EmergencyPhone = fam.cellPhone,
                School = "Wiley School District",
                Grade = "K",
                StudentNumber = $"WILEY{1000+idx}",
            }).ToList();
            var result = await ResilientDbExecution.ExecuteWithResilienceAsync(async () => {
                using var context = _contextFactory.CreateDbContext();
                var existing = await context.Students.CountAsync();
                if (existing >= 5)
                {
                    return new SeedResult { Success = true, RecordsSeeded = 0, ErrorMessage = "Already seeded" };
                }

                context.Students.AddRange(studentsToSeed);
                recordsSeeded = studentsToSeed.Count;
                await context.SaveChangesAsync();
                return new SeedResult { Success = true, RecordsSeeded = recordsSeeded };
            }, "SeedWileySchoolDistrictData", maxRetries: 3);
            stopwatch.Stop();
            return new SeedResult {
                Success = result.Success,
                RecordsSeeded = result.RecordsSeeded,
                ErrorMessage = result.ErrorMessage,
                Duration = stopwatch.Elapsed,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error(ex, "Error during Wiley School District data seeding");
            return new SeedResult {
                Success = false,
                RecordsSeeded = recordsSeeded,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    // Helper for JSON deserialization
public class WileyDataRoot {
    public required List<Family> families { get; set; }
}
public class Family {
    public int id { get; set; }
    public required string parentGuardian { get; set; }
    public required string address { get; set; }
    public required string city { get; set; }
    public required string state { get; set; }
    public required string homePhone { get; set; }
    public required string cellPhone { get; set; }
}

    #endregion
}
