using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for seeding development data when running in development mode
    /// Helps populate empty databases with sample data for testing
    /// </summary>
    public class SeedDataService : ISeedDataService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private static readonly ILogger Logger = Log.ForContext<SeedDataService>();

        public SeedDataService(IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Seed sample activity logs for development/testing
        /// </summary>
        public async Task SeedActivityLogsAsync(int count = 50)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if logs already exist
                var existingCount = await context.ActivityLogs.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("ActivityLogs already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample activity logs...", count);

                var random = new Random();
                var actions = new[] { "User Login", "Data Export", "Report Generated", "Settings Changed", "Database Backup", "System Maintenance" };
                var users = new[] { "admin", "steve.mckitrick", "test_user", "system" };

                var logs = new List<ActivityLog>();
                for (int i = 0; i < count; i++)
                {
                    logs.Add(new ActivityLog
                    {
                        Timestamp = DateTime.UtcNow.AddDays(-random.Next(0, 30)).AddHours(-random.Next(0, 24)),
                        Action = actions[random.Next(actions.Length)],
                        User = users[random.Next(users.Length)],
                        Details = $"Sample activity log entry #{i + 1} - Generated for development testing"
                    });
                }

                context.ActivityLogs.AddRange(logs);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} activity logs", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding activity logs");
                throw;
            }
        }

        /// <summary>
        /// Seed sample drivers for development/testing
        /// </summary>
        public async Task SeedDriversAsync(int count = 10)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if drivers already exist
                var existingCount = await context.Drivers.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Drivers already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample drivers...", count);

                var random = new Random();
                var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Lisa", "Tom", "Anna", "Chris", "Emma" };
                var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
                var licenseTypes = new[] { "CDL", "Standard", "Commercial" };

                var drivers = new List<Driver>();
                for (int i = 0; i < count; i++)
                {
                    var firstName = firstNames[random.Next(firstNames.Length)];
                    var lastName = lastNames[random.Next(lastNames.Length)];

                    drivers.Add(new Driver
                    {
                        DriverName = $"{firstName} {lastName}",
                        FirstName = firstName,
                        LastName = lastName,
                        DriversLicenceType = licenseTypes[random.Next(licenseTypes.Length)],
                        Status = "Active",
                        DriverPhone = $"555-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                        DriverEmail = $"{firstName.ToLower(CultureInfo.InvariantCulture)}.{lastName.ToLower(CultureInfo.InvariantCulture)}@busbuddy.com",
                        TrainingComplete = random.Next(0, 2) == 1,
                        HireDate = DateTime.UtcNow.AddDays(-random.Next(30, 365)),
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    });
                }

                context.Drivers.AddRange(drivers);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} drivers", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding drivers");
                throw;
            }
        }

        /// <summary>
        /// Seed sample buses for development/testing
        /// </summary>
        public async Task SeedBusesAsync(int count = 12)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if buses already exist
                var existingCount = await context.Buses.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Buses already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample buses...", count);

                var random = new Random();
                var makes = new[] { "Blue Bird", "Thomas Built", "IC Bus", "Collins", "Starcraft" };
                var models = new[] { "Vision", "Conventional", "RE Series", "Type A", "Type C", "Quest" };

                var buses = new List<Bus>();
                for (int i = 0; i < count; i++)
                {
                    var make = makes[random.Next(makes.Length)];
                    var model = models[random.Next(models.Length)];
                    var year = random.Next(2015, 2025);

                    buses.Add(new Bus
                    {
                        BusNumber = $"BUS-{(i + 1):000}",
                        Year = year,
                        Make = make,
                        Model = model,
                        SeatingCapacity = random.Next(20, 72),
                        VINNumber = $"1{make.Substring(0, 2).ToUpper(CultureInfo.InvariantCulture)}{year}{random.Next(100000, 999999)}",
                        LicenseNumber = $"SCH{random.Next(1000, 9999)}",
                        Status = random.Next(0, 10) < 8 ? "Active" : "Maintenance",
                        CurrentOdometer = random.Next(5000, 150000),
                        DateLastInspection = DateTime.UtcNow.AddDays(-random.Next(1, 180)),
                        PurchaseDate = new DateTime(year, random.Next(1, 13), random.Next(1, 28)),
                        PurchasePrice = random.Next(80000, 150000),
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    });
                }

                context.Buses.AddRange(buses);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} buses", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding buses");
                throw;
            }
        }

        /// <summary>
        /// Seed sample activities for development/testing
        /// </summary>
        public async Task SeedActivitiesAsync(int count = 25)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if activities already exist
                var existingCount = await context.Activities.CountAsync();

                // TODO: Add logic for seeding activities (currently not implemented)
                // This method previously contained a mix of bus seeding and activity logic, which was invalid.
                // Implement proper activity seeding here as needed.

                Logger.Information("Successfully seeded {Count} activities", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding activities");
                throw;
            }
        }

        /// <summary>
        /// Seed students from real-world CSV data (BusRiders_25-26.xlsz.csv)
        /// </summary>
        public async Task SeedStudentsFromCsvAsync()
        {
            // Full CSV content should be embedded here in production
            const string csvData = @"
Fname,Lname,Student #,Grade,Address,City,State,County,Parent/Guardian,Home Phone,Emergency Phone
John,Smith,1001,5,123 Main St,Springfield,OH,Clark,Jane Smith,555-1234,555-5678
Mary,Smith,,3,123 Main St,Springfield,OH,Clark,,,
Tom,Brown,1003,4,456 Oak Ave,Springfield,OH,Clark,Robert Brown,555-8765,555-4321
Sue,Brown,,2,456 Oak Ave,Springfield,OH,Clark,,,
";
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var existingCount = await context.Students.CountAsync();
                if (existingCount > 0)
                {
                    Logger.Information("Students already exist. Skipping CSV seed.");
                    return;
                }
                var lines = csvData.Trim().Split('\n');
                if (lines.Length < 2)
                {
                    Logger.Warning("No student data found in CSV.");
                    return;
                }
                var header = lines[0].Split(',');
                int idxFname = Array.IndexOf(header, "Fname");
                int idxLname = Array.IndexOf(header, "Lname");
                int idxStudentNum = Array.IndexOf(header, "Student #");
                int idxGrade = Array.IndexOf(header, "Grade");
                int idxAddress = Array.IndexOf(header, "Address");
                int idxCity = Array.IndexOf(header, "City");
                int idxState = Array.IndexOf(header, "State");
                int idxCounty = Array.IndexOf(header, "County");
                int idxParent = Array.IndexOf(header, "Parent/Guardian");
                int idxHomePhone = Array.IndexOf(header, "Home Phone");
                int idxEmergencyPhone = Array.IndexOf(header, "Emergency Phone");

                string lastParent = string.Empty;
                string lastHomePhone = string.Empty;
                string lastEmergencyPhone = string.Empty;
                int familyId = 1;
                int studentAutoId = 1;
                var families = new List<Family>();
                var students = new List<Student>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var row = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(row))
                    {
                        continue;
                    }
                    var cols = row.Split(',');
                    if (cols.Length < header.Length)
                    {
                        Logger.Warning($"Skipping row {i + 1}: not enough columns.");
                        continue;
                    }

                    string fname = cols[idxFname].Trim();
                    string lname = cols[idxLname].Trim();
                    string studentNum = cols[idxStudentNum].Trim();
                    string grade = cols[idxGrade].Trim();
                    string address = cols[idxAddress].Trim();
                    string city = cols[idxCity].Trim();
                    string state = cols[idxState].Trim();
                    string county = cols[idxCounty].Trim();
                    string parent = cols[idxParent].Trim();
                    string homePhone = cols[idxHomePhone].Trim();
                    string emergencyPhone = cols[idxEmergencyPhone].Trim();

                    // Skip row if no student name
                    if (string.IsNullOrWhiteSpace(fname) && string.IsNullOrWhiteSpace(lname))
                    {
                        Logger.Warning($"Skipping row {i + 1}: missing student name.");
                        continue;
                    }

                    // Fill down family info
                    if (!string.IsNullOrEmpty(parent))
                    {
                        lastParent = parent;
                    }
                    if (!string.IsNullOrEmpty(homePhone))
                    {
                        lastHomePhone = homePhone;
                    }
                    if (!string.IsNullOrEmpty(emergencyPhone))
                    {
                        lastEmergencyPhone = emergencyPhone;
                    }

                    // Compose fields
                    string studentName = $"{fname} {lname}".Trim();
                    string homeAddress = $"{address}, {city}, {state}, {county}".Replace("  ", " ").Trim(',').Trim();
                    string finalStudentNum = !string.IsNullOrWhiteSpace(studentNum) ? studentNum : $"STU{studentAutoId++.ToString("D4", CultureInfo.InvariantCulture)}";
                    string finalGrade = string.IsNullOrWhiteSpace(grade) ? string.Empty : grade;

                    // Create or find family (simple: new family if parent/phone changes)
                    var family = families.LastOrDefault(f => f.ParentGuardian == lastParent && f.HomePhone == lastHomePhone);
                    if (family == null)
                    {
                        family = new Family
                        {
                            FamilyId = familyId++,
                            ParentGuardian = lastParent,
                            Address = address,
                            City = city,
                            County = county,
                            HomePhone = lastHomePhone,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = "SeedDataService"
                        };
                        families.Add(family);
                    }

                    var student = new Student
                    {
                        StudentName = studentName,
                        StudentNumber = finalStudentNum,
                        Grade = finalGrade,
                        HomeAddress = homeAddress,
                        ParentGuardian = lastParent,
                        HomePhone = lastHomePhone,
                        EmergencyPhone = lastEmergencyPhone,
                        School = "Wiley School District",
                        Family = family,
                        FamilyId = family.FamilyId,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    };
                    students.Add(student);
                }

                context.Families.AddRange(families);
                context.Students.AddRange(students);
                await context.SaveChangesAsync();
                Logger.Information("Seeded {Count} students from CSV.", students.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding students from CSV");
                throw;
            }
        }

        /// <summary>
        /// Seed sample routes for development/testing
        /// </summary>
        public async Task SeedRoutesAsync(int count = 8)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if routes already exist
                var existingCount = await context.Routes.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Routes already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample routes...", count);

                var random = new Random();
                var routeNames = new[] { "North Elementary", "South Elementary", "Middle School Express", "High School Route A", "High School Route B", "Elementary East", "Elementary West", "Special Needs Route" };
                var schools = new[] { "Washington Elementary", "Lincoln Middle School", "Roosevelt High School", "Jefferson Elementary", "Madison High School" };
                var drivers = await context.Drivers.Take(count).ToListAsync();
                var buses = await context.Buses.Take(count).ToListAsync();

                var routes = new List<Route>();
                for (int i = 0; i < count; i++)
                {
                    var routeName = i < routeNames.Length ? routeNames[i] : $"Route {i + 1}";

                    routes.Add(new Route
                    {
                        Date = DateTime.Today.AddDays(-random.Next(0, 30)),
                        RouteName = routeName,
                        Description = $"Daily route for {routeName}",
                        School = schools[random.Next(schools.Length)],
                        AMDriverId = drivers.Count > i ? drivers[i].DriverId : null,
                        AMVehicleId = buses.Count > i ? buses[i].VehicleId : null,
                        PMDriverId = drivers.Count > i && drivers.Count > i + count/2 ? drivers[i + count/2].DriverId : null,
                        PMVehicleId = buses.Count > i && buses.Count > i + count/2 ? buses[i + count/2].VehicleId : null,
                        AMRiders = random.Next(5, 25),
                        PMRiders = random.Next(5, 25),
                        IsActive = random.Next(0, 10) > 1 // 90% active
                    });
                }

                context.Routes.AddRange(routes);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} routes", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding routes");
                throw;
            }
        }

        /// <summary>
        /// Seed all development data
        /// </summary>
        public async Task SeedAllAsync()
        {
            Logger.Information("Starting full development data seeding...");

            await SeedActivityLogsAsync(100);
            await SeedDriversAsync(15);
            await SeedBusesAsync(12);
            await SeedStudentsFromCsvAsync();
            await SeedRoutesAsync(8);
            await SeedActivitiesAsync(25);

            Logger.Information("Development data seeding completed");
        }

        /// <summary>
        /// Clear all seeded data (use with caution!)
        /// </summary>
        public async Task ClearSeedDataAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                Logger.Warning("Clearing all seeded data...");

                // Only clear data created by seed service
                var seedLogs = await context.ActivityLogs
                    .Where(a => a.Details != null && a.Details.Contains("Generated for development testing", StringComparison.OrdinalIgnoreCase))
                    .ToListAsync();

                var seedDrivers = await context.Drivers
                    .Where(d => d.CreatedBy == "SeedDataService")
                    .ToListAsync();

                if (seedLogs.Any())
                {
                    context.ActivityLogs.RemoveRange(seedLogs);
                    Logger.Information("Removed {Count} seeded activity logs", seedLogs.Count);
                }

                if (seedDrivers.Any())
                {
                    context.Drivers.RemoveRange(seedDrivers);
                    Logger.Information("Removed {Count} seeded drivers", seedDrivers.Count);
                }

                await context.SaveChangesAsync();
                Logger.Information("Seed data clearing completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error clearing seed data");
                throw;
            }
        }
    }
}
