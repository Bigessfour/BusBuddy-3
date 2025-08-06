using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Data
{
    public class SeedDataService
    {
        private readonly BusBuddyDbContext _context;
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(BusBuddyDbContext context, ILogger<SeedDataService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seeds Wiley School District data from JSON file into the database.
        /// Uses resilient execution for database operations.
        /// </summary>
        public async Task SeedWileySchoolDistrictDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting Wiley School District data seeding.");

                string jsonPath = Path.Combine("Data", "wiley-school-district-data.json");
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning("JSON file not found: {JsonPath}", jsonPath);
                    return;
                }

                var students = JsonDataImporter.Import<List<Student>>(jsonPath);

                if (students == null || students.Count == 0)
                {
                    _logger.LogWarning("No data to seed from JSON.");
                    return;
                }

                await ResilientDbExecution.ExecuteAsync(async () =>
                {
                    _context.Students.AddRange(students);
                    await _context.SaveChangesAsync();
                }, _logger);

                _logger.LogInformation("Seeded {Count} students successfully.", students.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Wiley School District data seeding.");
                throw;
            }
        }
    }
}

    private static string BuildTransportationNotes(WileyStudent studentData)
    {
        var notes = new List<string>();

        if (!string.IsNullOrEmpty(studentData.FullTimeTransport))
        {
            notes.Add($"Full Time: {studentData.FullTimeTransport}");
        }

        if (!string.IsNullOrEmpty(studentData.InfrequentTransport))
        {
            notes.Add($"Infrequent: {studentData.InfrequentTransport}");
        }

        if (!string.IsNullOrEmpty(studentData.TransportationNotes))
        {
            notes.Add(studentData.TransportationNotes);
        }

        return string.Join("; ", notes);
    }

    private async Task<int> SeedWileyStudentAssignmentsAsync(BusBuddyDbContext context)
    {
        var studentsToAssign = new[]
        {
            new { Name = "Alice Smith", Grade = "3", Route = "Truck Plaza Route", Stop = "Ports to Plains Plaza" },
            new { Name = "Bob Johnson", Grade = "6", Route = "Big Bend Route", Stop = "North-west country" },
            new { Name = "Charlie Brown", Grade = "4", Route = "Truck Plaza Route", Stop = "Ports to Plains Plaza" },
            new { Name = "David Wilson", Grade = "7", Route = "East Route", Stop = "East country near CR 6" },
            new { Name = "Eve Davis", Grade = "2", Route = "Big Bend Route", Stop = "South-west near river" }
        };

        int assignedCount = 0;
        foreach (var entry in studentsToAssign)
        {
            var student = await context.Students.FirstOrDefaultAsync(s => s.StudentName == entry.Name && s.Grade == entry.Grade);
            var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteName == entry.Route);
            if (student != null && route != null)
            {
                var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.BusNumber == route.RouteName || v.BusNumber == route.RouteName.Replace(" Route", ""));
                if (vehicle != null)
                {
                var assignment = new RouteAssignment
                {
                    RouteId = route.RouteId,
                    VehicleId = vehicle.VehicleId,
                    AssignmentDate = DateTime.Today
                };
                await context.RouteAssignments.AddAsync(assignment);
                await context.SaveChangesAsync();
                student.RouteAssignmentId = assignment.RouteAssignmentId;
                student.BusStop = entry.Stop;
                context.Students.Update(student);
                assignedCount++;
                }
            }
        }
        await context.SaveChangesAsync();
        return assignedCount;
    }
}

/// <summary>
/// Analysis of data structure requirements for real-world transportation data
/// </summary>
public class DataStructureAnalysis
{
    public DateTime AnalysisDate { get; set; }
    public DriverRequirements DriverRequirements { get; set; } = new();
    public VehicleRequirements VehicleRequirements { get; set; } = new();
    public ActivityScheduleRequirements ActivityScheduleRequirements { get; set; } = new();
}

/// <summary>
/// Requirements analysis for driver data structure
/// </summary>
public class DriverRequirements
{
    public int ExistingCount { get; set; }
    public string[] RequiredFields { get; set; } = Array.Empty<string>();
    public string[] OptionalFields { get; set; } = Array.Empty<string>();
    public string[] DataValidationRules { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Requirements analysis for vehicle data structure
/// </summary>
public class VehicleRequirements
{
    public int ExistingCount { get; set; }
    public string[] RequiredFields { get; set; } = Array.Empty<string>();
    public string[] OptionalFields { get; set; } = Array.Empty<string>();
    public string[] DataValidationRules { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Requirements analysis for activity schedule data structure
/// </summary>
public class ActivityScheduleRequirements
{
    public int ExistingCount { get; set; }
    public string[] RequiredFields { get; set; } = Array.Empty<string>();
    public string[] OptionalFields { get; set; } = Array.Empty<string>();
    public string[] DataValidationRules { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Result of data seeding operation
/// </summary>
public class SeedDataResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DriversSeeded { get; set; }
    public int VehiclesSeeded { get; set; }
    public int ActivitiesSeeded { get; set; }
    public int RoutesSeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Real-world transportation data structure for JSON import
/// </summary>
public class RealWorldTransportationData
{
    public RealWorldDriver[] Drivers { get; set; } = Array.Empty<RealWorldDriver>();
    public RealWorldVehicle[] Vehicles { get; set; } = Array.Empty<RealWorldVehicle>();
    public RealWorldActivity[] Activities { get; set; } = Array.Empty<RealWorldActivity>();
}

/// <summary>
/// Real-world driver data structure
/// </summary>
public class RealWorldDriver
{
    public string DriverName { get; set; } = string.Empty;
    public string? DriverPhone { get; set; }
    public string? DriverEmail { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string DriversLicenceType { get; set; } = string.Empty;
    public bool TrainingComplete { get; set; }
}

/// <summary>
/// Real-world vehicle data structure
/// </summary>
public class RealWorldVehicle
{
    public string BusNumber { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int SeatingCapacity { get; set; }
    public string VinNumber { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public DateTime? DateLastInspection { get; set; }
    public int? CurrentOdometer { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
}

/// <summary>
/// Real-world activity/trip data structure
/// </summary>
public class RealWorldActivity
{
    public DateTime ScheduledDate { get; set; }
    public string TripType { get; set; } = string.Empty;
    public int ScheduledVehicleId { get; set; }
    public string ScheduledDestination { get; set; } = string.Empty;
    public TimeSpan ScheduledLeaveTime { get; set; }
    public TimeSpan ScheduledEventTime { get; set; }
    public int? ScheduledRiders { get; set; }
    public int ScheduledDriverId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Result of student data seeding operation
/// </summary>
public class StudentSeedResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int StudentsSeeded { get; set; }
    public int FamiliesProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Wiley School District data structure for JSON import
/// </summary>
public class WileySchoolDistrictData
{
    public WileyMetadata? Metadata { get; set; }
    public WileyFamily[]? Families { get; set; }
    public WileyStudent[]? Students { get; set; }
    public WileyRoute[]? Routes { get; set; }
    public WileyBusStop[]? BusStops { get; set; }
    public string[]? DataQualityNotes { get; set; }
}

/// <summary>
/// Wiley School District metadata
/// </summary>
public class WileyMetadata
{
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? District { get; set; }
    public string? Location { get; set; }
    public string? ExtractedDate { get; set; }
    public string? DataSource { get; set; }
}

/// <summary>
/// Wiley School District family data structure
/// </summary>
public class WileyFamily
{
    public int Id { get; set; }
    public string? ParentGuardian { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? County { get; set; }
    public string? HomePhone { get; set; }
    public string? CellPhone { get; set; }
    public string? EmergencyContact { get; set; }
    public string? JointParent { get; set; }
    public string? DataQuality { get; set; }
}

/// <summary>
/// Wiley School District student data structure
/// </summary>
public class WileyStudent
{
    public int FamilyId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? StudentName { get; set; }
    public string? Grade { get; set; }
    public string? School { get; set; }
    public string? HomeAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? ParentGuardian { get; set; }
    public string? HomePhone { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? FullTimeTransport { get; set; }
    public string? InfrequentTransport { get; set; }
    public string? TransportationNotes { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? EnrollmentDate { get; set; }
    public string? DataQuality { get; set; }
}

/// <summary>
/// Wiley School District route data structure
/// </summary>
public class WileyRoute
{
    public string? RouteName { get; set; }
    public string? RouteType { get; set; }
    public string? SchoolName { get; set; }
    public string? Driver { get; set; }
    public string? Vehicle { get; set; }
    public bool IsActive { get; set; }
    public string? ServiceArea { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Wiley School District bus stop data structure
/// </summary>
public class WileyBusStop
{
    public string? StopName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? RouteName { get; set; }
    public string[]? Students { get; set; }
    public bool IsActive { get; set; }
}
