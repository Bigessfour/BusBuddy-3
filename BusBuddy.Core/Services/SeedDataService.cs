using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for seeding development data when running in development mode
    /// Helps populate empty databases with sample data for testing
    /// </summary>
    public partial class SeedDataService : ISeedDataService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IConfiguration? _configuration;
        private static readonly ILogger Logger = Log.ForContext<SeedDataService>();
        // Cache JsonSerializerOptions to avoid repeated allocations (fixes CA1869)
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public SeedDataService(IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public SeedDataService(IBusBuddyDbContextFactory contextFactory, IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Seed students from a JSON file specified by configuration key "StudentJsonPath".
        /// If Students table already contains any records, this method exits without changes.
        /// </summary>
        public async Task SeedFromJsonAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Skip if any students already exist
                int existingCount;
                try { existingCount = await context.Students.CountAsync(); }
                catch (InvalidOperationException) { existingCount = context.Students.Count(); }
                if (existingCount > 0)
                {
                    Logger.Information("Students table already has {Count} records. Skipping JSON seed.", existingCount);
                    return;
                }

                // Resolve JSON path
                string? jsonPath = _configuration?["StudentJsonPath"];
                if (string.IsNullOrWhiteSpace(jsonPath))
                {
                    // Fallback: try local appsettings.json next to the running app
                    try
                    {
                        var config = new ConfigurationBuilder()
                            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddEnvironmentVariables()
                            .Build();
                        jsonPath = config["StudentJsonPath"];
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed to load configuration for StudentJsonPath fallback");
                    }
                }

                if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                {
                    Logger.Warning("StudentJsonPath not found or file missing: {Path}", jsonPath);
                    return;
                }

                var json = await File.ReadAllTextAsync(jsonPath);

                // Try to deserialize as a plain array of Student first
                List<Student>? students = null;
                try
                {
                    students = JsonSerializer.Deserialize<List<Student>>(json, _jsonOptions);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Direct List<Student> deserialization failed; will try wrapper");
                }

                // If null, try wrapper object with a Students property
                if (students == null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Students", out var studentsElement) &&
                            studentsElement.ValueKind == JsonValueKind.Array)
                        {
                            students = JsonSerializer.Deserialize<List<Student>>(studentsElement.GetRawText(), _jsonOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed to parse Students array from JSON wrapper");
                    }
                }

                if (students == null || students.Count == 0)
                {
                    Logger.Warning("No student records found in JSON at {Path}", jsonPath);
                    return;
                }

                // A JSON roster written before RidesAm/RidesPm existed states only the route columns.
                // Without this the whole seed lands not-eligible for either run.
                var inferred = students.Count(StudentRideModeHelper.ApplyRouteDerivedEligibility);

                context.Students.AddRange(students);
                await context.SaveChangesAsync();
                Logger.Information(
                    "Seeded {Count} students from JSON: {Path} (eligibility inferred from routes for {Inferred})",
                    students.Count,
                    jsonPath,
                    inferred);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during JSON seeding in SeedFromJsonAsync");
                throw;
            }
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
                int existingCount;
                try
                {
                    existingCount = await context.ActivityLogs.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    // Fallback for mocked sets without async provider
                    existingCount = context.ActivityLogs.Count();
                }
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
                int existingCount;
                try
                {
                    existingCount = await context.Drivers.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    existingCount = context.Drivers.Count();
                }
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
                int existingCount;
                try
                {
                    existingCount = await context.Buses.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    existingCount = context.Buses.Count();
                }
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

        public async Task<int> EnsureRoutesForStudentAssignmentsAsync()
        {
            using var context = _contextFactory.CreateWriteDbContext();

            // AsTracking because BusBuddyDbContext defaults to NoTracking, and the canonical-spelling
            // rewrite below has to persist.
            var assigned = await context.Students
                .AsTracking()
                .Where(s => s.AMRoute != null || s.PMRoute != null)
                .ToListAsync();

            var created = await ReconcileRouteAssignmentsAsync(
                context, assigned, "EnsureRoutesForStudentAssignments");

            var rewritten = await context.SaveChangesAsync();
            Logger.Information(
                "Route assignment repair complete StudentsInspected={Students} RoutesCreated={Created} " +
                "StudentRowsRewritten={Rewritten}",
                assigned.Count,
                created,
                rewritten);

            return created;
        }

        private static async Task<int> NextStudentNumberAsync(BusBuddyDbContext context)
        {
            List<string> existingNumbers;
            try
            {
                existingNumbers = await context.Students
                    .Where(s => s.StudentNumber != null && s.StudentNumber.StartsWith("STU"))
                    .Select(s => s.StudentNumber!)
                    .ToListAsync();
            }
            catch (InvalidOperationException)
            {
                existingNumbers = context.Students
                    .Where(s => s.StudentNumber != null && s.StudentNumber.StartsWith("STU"))
                    .Select(s => s.StudentNumber!)
                    .ToList();
            }

            var max = 0;
            foreach (var number in existingNumbers)
            {
                if (number.Length > 3 &&
                    int.TryParse(number.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                    value > max)
                {
                    max = value;
                }
            }

            return max + 1;
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
                    var isSpecialNeedsRoute = routeName.Contains("Special Needs", StringComparison.OrdinalIgnoreCase);

                    routes.Add(new Route
                    {
                        Date = Route.NormalizeRouteDate(DateTime.UtcNow.AddDays(-random.Next(0, 30))),
                        RouteName = routeName,
                        Description = $"Daily route for {routeName}",
                        School = schools[random.Next(schools.Length)],
                        IsSpecialNeedsRoute = isSpecialNeedsRoute,
                        AMDriverId = drivers.Count > i ? drivers[i].DriverId : null,
                        AMVehicleId = buses.Count > i ? buses[i].BusId : null,
                        PMDriverId = drivers.Count > i && drivers.Count > i + count / 2 ? drivers[i + count / 2].DriverId : null,
                        PMVehicleId = buses.Count > i && buses.Count > i + count / 2 ? buses[i + count / 2].BusId : null,
                        AMRiders = random.Next(5, 25),
                        PMRiders = random.Next(5, 25),
                        IsActive = random.Next(0, 10) > 1 // 90% active
                    });
                    routes[^1].Session = RouteSession.Infer(routes[^1]);
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
        /// Seed all development data.
        /// </summary>
        public async Task SeedAllAsync()
        {
            Logger.Information("Starting full development data seeding...");

            await SeedActivityLogsAsync(100);
            await SeedDriversAsync(15);
            await SeedBusesAsync(12);
            // Students are clerk-entered (Add Student / Import CSV). Do not top up the roster.
            await SeedRoutesAsync(8);
            // Activities (one-off trips) are not seeded: the Trip Board is clerk-entered only.
            await EnsureMapDemoGeoAsync();

            // Leave no student pointing at a route name that is missing or shared, or the form
            // refuses to save them (ValidateStudentAsync accepts a key, or a unique route name).
            await EnsureRoutesForStudentAssignmentsAsync();

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
