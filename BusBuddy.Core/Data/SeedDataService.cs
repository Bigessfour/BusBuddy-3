using System.IO;
using System.Text.Json;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Data;

/// <summary>
/// Service for seeding real-world transportation data into BusBuddy database
/// Provides infrastructure for importing, validating, and managing seed data
/// </summary>
public class SeedDataService
{
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private static readonly ILogger Logger = Log.ForContext<SeedDataService>();

    public SeedDataService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Analyzes current data structure requirements for real-world data integration
    /// </summary>
    public async Task<DataStructureAnalysis> AnalyzeDataStructureAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        Logger.Information("Analyzing data structure for real-world transportation data");

        var analysis = new DataStructureAnalysis
        {
            AnalysisDate = DateTime.UtcNow,
            DriverRequirements = await AnalyzeDriverRequirementsAsync(context),
            VehicleRequirements = await AnalyzeVehicleRequirementsAsync(context),
            ActivityScheduleRequirements = await AnalyzeActivityScheduleRequirementsAsync(context)
        };

        Logger.Information("Data structure analysis completed. Ready for real-world data integration");
        return analysis;
    }

    /// <summary>
    /// Prepares database for real-world data seeding
    /// </summary>
    public async Task<bool> PrepareForRealWorldDataAsync()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            Logger.Information("Preparing database for real-world transportation data");

            // Ensure database is created and migrated
            await context.Database.MigrateAsync();

            // Validate table structures
            var hasDrivers = await context.Drivers.AnyAsync();
            var hasVehicles = await context.Vehicles.AnyAsync();
            var hasActivities = await context.ActivitySchedule.AnyAsync();

            Logger.Information("Database preparation complete. Ready for real-world data: Drivers={HasDrivers}, Vehicles={HasVehicles}, Activities={HasActivities}",
                hasDrivers, hasVehicles, hasActivities);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to prepare database for real-world data");
            return false;
        }
    }

    /// <summary>
    /// Seeds real-world transportation data from provided JSON file
    /// </summary>
    public async Task<SeedDataResult> SeedRealWorldDataAsync(string jsonFilePath)
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            Logger.Information("Seeding real-world transportation data from {FilePath}", jsonFilePath);

            if (!File.Exists(jsonFilePath))
            {
                Logger.Error("Real-world data file not found: {FilePath}", jsonFilePath);
                return new SeedDataResult { Success = false, ErrorMessage = $"File not found: {jsonFilePath}" };
            }

            var result = new SeedDataResult { StartTime = DateTime.UtcNow };
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);

            // Parse and validate the JSON structure
            var realWorldData = System.Text.Json.JsonSerializer.Deserialize<RealWorldTransportationData>(jsonContent);
            if (realWorldData == null)
            {
                Logger.Error("Failed to parse real-world data from JSON file");
                return new SeedDataResult { Success = false, ErrorMessage = "Invalid JSON format" };
            }

            // Seed real-world data in correct order (drivers and vehicles first, then activities)
            result.DriversSeeded = await SeedRealWorldDriversAsync(context, realWorldData.Drivers);
            result.VehiclesSeeded = await SeedRealWorldVehiclesAsync(context, realWorldData.Vehicles);

            // Save drivers and vehicles first to ensure IDs are available for activities
            await context.SaveChangesAsync();

            result.ActivitiesSeeded = await SeedRealWorldActivitiesAsync(context, realWorldData.Activities);

            await context.SaveChangesAsync();
            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            Logger.Information("Real-world data seeding completed: {DriversSeeded} drivers, {VehiclesSeeded} vehicles, {ActivitiesSeeded} activities",
                result.DriversSeeded, result.VehiclesSeeded, result.ActivitiesSeeded);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to seed real-world transportation data");
            return new SeedDataResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Seeds sample transportation data for development and testing
    /// </summary>
    public async Task<SeedDataResult> SeedSampleDataAsync()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            Logger.Information("Seeding sample transportation data");

            var result = new SeedDataResult { StartTime = DateTime.UtcNow };

            // Seed Drivers (15-20 real-world driver profiles)
            result.DriversSeeded = await SeedSampleDriversAsync(context);

            // Seed Vehicles (10-15 realistic bus fleet)
            result.VehiclesSeeded = await SeedSampleVehiclesAsync(context);

            // Seed Activity Schedules (25-30 realistic transportation activities)
            result.ActivitiesSeeded = await SeedSampleActivitiesAsync(context);

            // Seed Routes (3 realistic routes)
            result.RoutesSeeded = await SeedSampleRoutesAsync(context);

            await context.SaveChangesAsync();
            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            Logger.Information("Sample data seeding completed: {DriversSeeded} drivers, {VehiclesSeeded} vehicles, {ActivitiesSeeded} activities, {RoutesSeeded} routes",
                result.DriversSeeded, result.VehiclesSeeded, result.ActivitiesSeeded, result.RoutesSeeded);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to seed sample transportation data");
            return new SeedDataResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<DriverRequirements> AnalyzeDriverRequirementsAsync(BusBuddyDbContext context)
    {
        var existingCount = await context.Drivers.CountAsync();
        return new DriverRequirements
        {
            ExistingCount = existingCount,
            RequiredFields = new[] { "DriverName", "DriverPhone", "DriversLicenceType", "TrainingComplete" },
            OptionalFields = new[] { "DriverEmail", "Address", "City", "State", "Zip" },
            DataValidationRules = new[]
            {
                "DriverName: Required, Max 100 characters",
                "DriverPhone: Optional, Max 20 characters",
                "DriverEmail: Optional, Valid email format",
                "DriversLicenceType: Required, Valid license type (CDL-A, CDL-B, etc.)",
                "TrainingComplete: Required boolean"
            }
        };
    }

    private async Task<VehicleRequirements> AnalyzeVehicleRequirementsAsync(BusBuddyDbContext context)
    {
        var existingCount = await context.Vehicles.CountAsync();
        return new VehicleRequirements
        {
            ExistingCount = existingCount,
            RequiredFields = new[] { "BusNumber", "Year", "Make", "Model", "SeatingCapacity", "VinNumber" },
            OptionalFields = new[] { "LicenseNumber", "DateLastInspection", "CurrentOdometer", "PurchaseDate", "PurchasePrice" },
            DataValidationRules = new[]
            {
                "BusNumber: Required, Max 20 characters, Unique",
                "Year: Required, Range 1990-2030",
                "Make: Required, Max 50 characters",
                "Model: Required, Max 50 characters",
                "SeatingCapacity: Required, Range 1-100",
                "VinNumber: Required, Max 50 characters, Unique",
                "LicenseNumber: Optional, Max 20 characters"
            }
        };
    }

    private async Task<ActivityScheduleRequirements> AnalyzeActivityScheduleRequirementsAsync(BusBuddyDbContext context)
    {
        var existingCount = await context.ActivitySchedule.CountAsync();
        return new ActivityScheduleRequirements
        {
            ExistingCount = existingCount,
            RequiredFields = new[] { "ScheduledDate", "TripType", "ScheduledVehicleId", "ScheduledDestination", "ScheduledDriverId", "RequestedBy" },
            OptionalFields = new[] { "ScheduledRiders", "Notes", "Status" },
            DataValidationRules = new[]
            {
                "ScheduledDate: Required, Valid future date",
                "TripType: Required, Max 50 characters (Sports Trip, Field Trip, etc.)",
                "ScheduledDestination: Required, Max 200 characters",
                "ScheduledLeaveTime: Required, Valid time",
                "ScheduledEventTime: Required, Valid time",
                "ScheduledVehicleId: Required, Must reference existing vehicle",
                "ScheduledDriverId: Required, Must reference existing driver"
            }
        };
    }

    private Task<int> SeedSampleDriversAsync(BusBuddyDbContext context)
    {
        // Implementation will be provided when real-world data is available
        // This method will create realistic driver profiles based on provided data
        Logger.Information("Sample driver seeding ready for real-world data integration");
        return Task.FromResult(0); // Will return actual count when implemented
    }

    private async Task<int> SeedSampleVehiclesAsync(BusBuddyDbContext context)
    {
        using (LogContext.PushProperty("Operation", "SeedSampleVehicles"))
        {
            Logger.Information("Starting sample vehicle seeding with baseline fleet data");

            try
            {
                // Check if vehicles already exist to avoid duplicates
                var existingVehicleCount = await context.Vehicles.CountAsync();
                if (existingVehicleCount > 0)
                {
                    Logger.Information("Vehicles already exist ({Count}), skipping vehicle seeding", existingVehicleCount);
                    return 0;
                }

                var vehicles = new List<Bus>
                {
                    new()
                    {
                        BusNumber = "17",
                        Year = 2021,
                        Make = "Thomas",
                        Model = "School Bus",
                        SeatingCapacity = 84,
                        VINNumber = "1T88Y9D23M1169830",
                        LicenseNumber = "BB-017",
                        Status = "Active",
                        DateLastInspection = DateTime.UtcNow.AddDays(-30),
                        PurchaseDate = new DateTime(2021, 8, 15),
                        Description = "Truck Plaza Route"
                    },
                    new()
                    {
                        BusNumber = "Route 2 Bus",
                        Year = 2023,
                        Make = "GMC",
                        Model = "Savana",
                        SeatingCapacity = 14,
                        VINNumber = "7GZ67UB78PN013733",
                        LicenseNumber = "BB-R02",
                        Status = "Active",
                        DateLastInspection = DateTime.UtcNow.AddDays(-15),
                        PurchaseDate = new DateTime(2023, 6, 10),
                        Description = "Big Bend Route"
                    },
                    new()
                    {
                        BusNumber = "Route 3 Bus",
                        Year = 2024,
                        Make = "Bluebird",
                        Model = "Vision",
                        SeatingCapacity = 14,
                        VINNumber = "1GDJG31UX41120693",
                        LicenseNumber = "BB-R03",
                        Status = "Active",
                        DateLastInspection = DateTime.UtcNow.AddDays(-7),
                        PurchaseDate = new DateTime(2024, 1, 20),
                        Description = "East Route"
                    }
                };

                await context.Vehicles.AddRangeAsync(vehicles);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} baseline vehicles", vehicles.Count);
                return vehicles.Count;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during sample vehicle seeding");
                throw;
            }
        }
    }

    private Task<int> SeedSampleActivitiesAsync(BusBuddyDbContext context)
    {
        // Implementation will be provided when real-world data is available
        // This method will create realistic activity schedules based on provided data
        Logger.Information("Sample activity seeding ready for real-world data integration");
        return Task.FromResult(0); // Will return actual count when implemented
    }

    private async Task<int> SeedSampleRoutesAsync(BusBuddyDbContext context)
    {
        using (LogContext.PushProperty("Operation", "SeedSampleRoutes"))
        {
            Logger.Information("Starting sample route seeding with baseline route data");
            try
            {
                // Check if routes already exist to avoid duplicates
                var existingRouteCount = await context.Routes.CountAsync();
                if (existingRouteCount > 0)
                {
                    Logger.Information("Routes already exist ({Count}), skipping route seeding", existingRouteCount);
                    return 0;
                }

                var routes = new List<Route>
                {
                    new()
                    {
                        RouteName = "Truck Plaza Route",
                        RouteDescription = "Truck Plaza Route",
                        Boundaries = "South district boundary",
                        Path = "CR 196 to CR 7 south to Ports to Plains Plaza, return north",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Wiley School District"
                    },
                    new()
                    {
                        RouteName = "Big Bend Route",
                        RouteDescription = "Big Bend Route",
                        Boundaries = "West of 287, north to segment, south to river",
                        Path = "North-west-south-east loop west of Hwy 287",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Wiley School District"
                    },
                    new()
                    {
                        RouteName = "East Route",
                        RouteDescription = "East Route",
                        Boundaries = "East of 287, north to Kiowa line segment, south to Arkansas River",
                        Path = "East-north-west-south loop east of Hwy 287 to CR 6",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Wiley School District"
                    }
                };

                await context.Routes.AddRangeAsync(routes);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} baseline routes", routes.Count);
                return routes.Count;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during sample route seeding");
                throw;
            }
        }
    }

    private Task<int> SeedRealWorldDriversAsync(BusBuddyDbContext context, RealWorldDriver[] drivers)
    {
        if (drivers == null || drivers.Length == 0)
        {
            return Task.FromResult(0);
        }

        var count = 0;
        foreach (var driverData in drivers)
        {
            var driver = new Driver
            {
                DriverName = driverData.DriverName,
                DriverPhone = driverData.DriverPhone,
                DriverEmail = driverData.DriverEmail,
                Address = driverData.Address,
                City = driverData.City,
                State = driverData.State,
                Zip = driverData.Zip,
                DriversLicenceType = driverData.DriversLicenceType,
                TrainingComplete = driverData.TrainingComplete,
                Status = "Active"
            };

            context.Drivers.Add(driver);
            count++;
        }

        Logger.Information("Prepared {Count} real-world drivers for seeding", count);
        return Task.FromResult(count);
    }

    private Task<int> SeedRealWorldVehiclesAsync(BusBuddyDbContext context, RealWorldVehicle[] vehicles)
    {
        if (vehicles == null || vehicles.Length == 0)
        {
            return Task.FromResult(0);
        }

        var count = 0;
        foreach (var vehicleData in vehicles)
        {
            var vehicle = new Bus
            {
                BusNumber = vehicleData.BusNumber,
                Year = vehicleData.Year,
                Make = vehicleData.Make,
                Model = vehicleData.Model,
                SeatingCapacity = vehicleData.SeatingCapacity,
                VINNumber = vehicleData.VinNumber,
                LicenseNumber = vehicleData.LicenseNumber ?? string.Empty,
                DateLastInspection = vehicleData.DateLastInspection,
                CurrentOdometer = vehicleData.CurrentOdometer,
                PurchaseDate = vehicleData.PurchaseDate,
                Status = "Active"
            };

            context.Vehicles.Add(vehicle);
            count++;
        }

        Logger.Information("Prepared {Count} real-world vehicles for seeding", count);
        return Task.FromResult(count);
    }

    private async Task<int> SeedRealWorldActivitiesAsync(BusBuddyDbContext context, RealWorldActivity[] activities)
    {
        if (activities == null || activities.Length == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var activityData in activities)
        {
            // Find the referenced driver and vehicle by their identifiers
            var driver = await context.Drivers.FirstOrDefaultAsync(d => d.DriverId == activityData.ScheduledDriverId);
            var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == activityData.ScheduledVehicleId);

            if (driver == null || vehicle == null)
            {
                Logger.Warning("Skipping activity {Destination} - referenced driver or vehicle not found", activityData.ScheduledDestination);
                continue;
            }

            var activity = new ActivitySchedule
            {
                ScheduledDate = activityData.ScheduledDate,
                TripType = activityData.TripType,
                ScheduledVehicleId = vehicle.VehicleId,
                ScheduledDestination = activityData.ScheduledDestination,
                ScheduledLeaveTime = activityData.ScheduledLeaveTime,
                ScheduledEventTime = activityData.ScheduledEventTime,
                ScheduledRiders = activityData.ScheduledRiders,
                ScheduledDriverId = driver.DriverId,
                RequestedBy = activityData.RequestedBy,
                Status = activityData.Status ?? "Scheduled",
                Notes = activityData.Notes
            };

            context.ActivitySchedule.Add(activity);
            count++;
        }

        Logger.Information("Prepared {Count} real-world activities for seeding", count);
        return count;
    }

    /// <summary>
    /// Seeds student and family data from Wiley School District registration forms
    /// </summary>
    public async Task<StudentSeedResult> SeedWileySchoolDistrictDataAsync()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            Logger.Information("Seeding Wiley School District student registration data");

            var result = new StudentSeedResult { StartTime = DateTime.UtcNow };

            // Load the JSON data
            var jsonPath = Path.Combine("Data", "wiley-school-district-data.json");
            if (!File.Exists(jsonPath))
            {
                Logger.Warning("Wiley School District data file not found: {JsonPath}", jsonPath);
                return new StudentSeedResult { Success = false, ErrorMessage = "Data file not found" };
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var wileyData = System.Text.Json.JsonSerializer.Deserialize<WileySchoolDistrictData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (wileyData?.Students == null)
            {
                Logger.Warning("No student data found in Wiley School District file");
                return new StudentSeedResult { Success = false, ErrorMessage = "No student data found" };
            }

            // Clear existing test data to avoid conflicts
            var existingStudents = await context.Students.Where(s => s.School == "Wiley School District").ToListAsync();
            if (existingStudents.Any())
            {
                Logger.Information("Removing {Count} existing Wiley School District students", existingStudents.Count);
                context.Students.RemoveRange(existingStudents);
                await context.SaveChangesAsync();
            }

            // Seed students with proper data validation
            var seededCount = SeedWileyStudentsAsync(context, wileyData.Students);
            result.StudentsSeeded = seededCount;
            result.FamiliesProcessed = wileyData.Families?.Length ?? 0;

            // Load routes for assignment
            var routes = await context.Routes.ToListAsync();
            var students = await context.Students.Where(s => s.School == "Wiley School District").ToListAsync();

            // Assign students to routes using existing context and simple operations
            // Note: For MVP, use direct database operations instead of complex service dependencies
            var assignedCount = await SeedWileyStudentAssignmentsAsync(context);
            result.StudentsSeeded += assignedCount;

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            Logger.Information("Wiley School District data seeding completed: {StudentsSeeded} students from {FamiliesProcessed} families",
                result.StudentsSeeded, result.FamiliesProcessed);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to seed Wiley School District data");
            return new StudentSeedResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private int SeedWileyStudentsAsync(BusBuddyDbContext context, WileyStudent[] students)
    {
        if (students == null || students.Length == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var studentData in students)
        {
            // Skip students with severely garbled data
            if (studentData.DataQuality == "poor" && (string.IsNullOrEmpty(studentData.FirstName) || studentData.FirstName == "Unknown"))
            {
                Logger.Information("Skipping student with poor data quality: {StudentName}", studentData.StudentName);
                continue;
            }

            var student = new Student
            {
                StudentName = !string.IsNullOrEmpty(studentData.StudentName) ? studentData.StudentName : $"{studentData.FirstName} {studentData.LastName}".Trim(),
                Grade = !string.IsNullOrEmpty(studentData.Grade) ? studentData.Grade : null,
                School = studentData.School ?? "Wiley School District",
                HomeAddress = studentData.HomeAddress ?? string.Empty,
                City = studentData.City ?? string.Empty,
                State = studentData.State ?? "CO",
                Zip = studentData.Zip ?? string.Empty,
                ParentGuardian = studentData.ParentGuardian ?? string.Empty,
                HomePhone = studentData.HomePhone ?? string.Empty,
                EmergencyPhone = studentData.EmergencyPhone ?? string.Empty,
                TransportationNotes = BuildTransportationNotes(studentData),
                Active = studentData.Active,
                EnrollmentDate = studentData.EnrollmentDate,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "System - Wiley District Import"
            };

            // Set pickup/dropoff addresses same as home address for now
            student.PickupAddress = student.HomeAddress;
            student.DropoffAddress = student.HomeAddress;

            // Add data quality notes
            if (!string.IsNullOrEmpty(studentData.DataQuality))
            {
                var qualityNote = $"Data Quality: {studentData.DataQuality}";
                student.TransportationNotes = string.IsNullOrEmpty(student.TransportationNotes)
                    ? qualityNote
                    : $"{student.TransportationNotes}; {qualityNote}";
            }

            context.Students.Add(student);
            count++;
            Logger.Debug("Added student: {StudentName} from {Address}", student.StudentName, student.HomeAddress);
        }

        Logger.Information("Prepared {Count} Wiley School District students for seeding", count);
        return count;
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
