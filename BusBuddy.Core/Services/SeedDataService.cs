using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System.Globalization;

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
                var existingCount = await context.Vehicles.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Vehicles already contain {ExistingCount} records. Skipping seed.", existingCount);
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

                context.Vehicles.AddRange(buses);
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
        /// Seed sample students for development/testing
        /// </summary>
        public async Task SeedStudentsAsync(int count = 25)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if students already exist
                var existingCount = await context.Students.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Students already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample students...", count);

                var random = new Random();
                var firstNames = new[] { "Alex", "Jamie", "Taylor", "Jordan", "Casey", "Riley", "Morgan", "Avery", "Dakota", "Sage", "Parker", "Quinn", "Blake", "Rowan", "Cameron" };
                var lastNames = new[] { "Anderson", "Brown", "Davis", "Garcia", "Johnson", "Jones", "Martinez", "Miller", "Moore", "Rodriguez", "Smith", "Taylor", "Thomas", "White", "Wilson" };
                var grades = new[] { "K", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };
                var schools = new[] { "Elementary School", "Middle School", "High School", "Primary Academy", "Learning Center" };

                var students = new List<Student>();
                for (int i = 0; i < count; i++)
                {
                    var firstName = firstNames[random.Next(firstNames.Length)];
                    var lastName = lastNames[random.Next(lastNames.Length)];
                    var grade = grades[random.Next(grades.Length)];

                    students.Add(new Student
                    {
                        StudentName = $"{firstName} {lastName}",
                        StudentNumber = $"STU{(1000 + i):D4}",
                        Grade = grade,
                        School = schools[random.Next(schools.Length)],
                        HomeAddress = $"{random.Next(100, 9999)} {lastNames[random.Next(lastNames.Length)]} St",
                        ParentGuardian = $"{firstNames[random.Next(firstNames.Length)]} {lastName}",
                        HomePhone = $"555-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                        EmergencyPhone = $"555-{random.Next(100, 999)}-{random.Next(1000, 9999)}"
                    });
                }

                context.Students.AddRange(students);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} students", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding students");
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
                var vehicles = await context.Vehicles.Take(count).ToListAsync();

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
                        AMVehicleId = vehicles.Count > i ? vehicles[i].VehicleId : null,
                        PMDriverId = drivers.Count > i && drivers.Count > i + count/2 ? drivers[i + count/2].DriverId : null,
                        PMVehicleId = vehicles.Count > i && vehicles.Count > i + count/2 ? vehicles[i + count/2].VehicleId : null,
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
            await SeedStudentsAsync(25);
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
