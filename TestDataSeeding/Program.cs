using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace BusBuddy.TestDataSeeding
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/seed-test-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Console.WriteLine("🚌 Testing Wiley School District Data Seeding...");
                Console.WriteLine();

                // Test JSON data loading

                // Try both the original relative path and the solution-root-relative path
                var dataPath = Path.Combine("..", "..", "..", "BusBuddy.Core", "Data", "wiley-school-district-data.json");
                var altDataPath = Path.Combine("BusBuddy.Core", "Data", "wiley-school-district-data.json");
                string usedPath = null;
                if (File.Exists(dataPath))
                {
                    usedPath = dataPath;
                }
                else if (File.Exists(altDataPath))
                {
                    usedPath = altDataPath;
                }
                else
                {
                    Console.WriteLine($"❌ Data file not found: {dataPath} or {altDataPath}");
                    return 1;
                }

                Console.WriteLine($"✅ Found data file: {usedPath}");

                // Try to load and parse the JSON (supports root object or root array)
                var jsonContent = await File.ReadAllTextAsync(usedPath);
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                WileyStudent[] students;
                int familiesCount = 0;

                using (var jsonDoc = JsonDocument.Parse(jsonContent))
                {
                    var root = jsonDoc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        // Root array of student-like objects — deserialize directly
                        students = JsonSerializer.Deserialize<WileyStudent[]>(jsonContent, jsonOptions) ?? Array.Empty<WileyStudent>();
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        // Root object — attempt WileySchoolDistrictData shape
                        var wileyData = JsonSerializer.Deserialize<WileySchoolDistrictData>(jsonContent, jsonOptions);
                        students = wileyData?.Students ?? Array.Empty<WileyStudent>();
                        familiesCount = wileyData?.Families?.Length ?? 0;
                    }
                    else
                    {
                        Console.WriteLine("❌ Unsupported JSON root type");
                        return 1;
                    }
                }

                if (students.Length == 0)
                {
                    Console.WriteLine("❌ No student data found in file");
                    return 1;
                }

                Console.WriteLine($"✅ Loaded {students.Length} students from {familiesCount} families");

                // Show data quality summary
                var goodCount = 0;
                var poorCount = 0;

                foreach (var student in students)
                {
                    if (string.Equals(student.DataQuality, "poor", StringComparison.OrdinalIgnoreCase))
                        poorCount++;
                    else
                        goodCount++;
                }

                Console.WriteLine($"📊 Data Quality: {goodCount} good/partial quality, {poorCount} poor quality");
                Console.WriteLine();

                // Show sample students
                Console.WriteLine("👥 Sample Students:");
                var count = 0;
                foreach (var student in students)
                {
                    if (count >= 3) break;
                    Console.WriteLine($"  • {student.StudentName} - {student.City}, {student.State} (Quality: {student.DataQuality ?? "unknown"})");
                    count++;
                }

                Console.WriteLine();
                Console.WriteLine("✅ JSON data structure validation successful!");

                // Now perform actual DB seeding using BusBuddyDbContext and JsonDataImporter
                Console.WriteLine("🌱 Seeding database using JsonDataImporter...");

                // Build configuration (reads appsettings and environment variables)
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{environment}.json", optional: true)
                    .AddJsonFile("appsettings.azure.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var connString =
                    Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION")
                    ?? config.GetConnectionString("DefaultConnection")
                    ?? config.GetConnectionString("AzureConnection")
                    ?? config.GetConnectionString("LocalConnection")
                    ?? config.GetConnectionString("BusBuddyDb");

                if (string.IsNullOrWhiteSpace(connString))
                {
                    Console.WriteLine("❌ No connection string found (BUSBUDDY_CONNECTION or appsettings). Aborting seeding.");
                    return 1;
                }

                // Expand placeholders like ${AZURE_SQL_USER} and %AZURE_SQL_USER%
                static string ExpandPlaceholders(string input)
                {
                    if (string.IsNullOrEmpty(input)) return input;
                    // Expand %VAR% style
                    var expanded = Environment.ExpandEnvironmentVariables(input);
                    // Expand ${VAR} style
                    expanded = Regex.Replace(expanded, @"\$\{(?<name>[A-Za-z0-9_]+)\}", m =>
                    {
                        var name = m.Groups["name"].Value;
                        var value = Environment.GetEnvironmentVariable(name);
                        return value ?? m.Value;
                    });
                    return expanded;
                }

                var effectiveConnString = ExpandPlaceholders(connString);

                var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                    .UseSqlServer(effectiveConnString, sql =>
                    {
                        sql.CommandTimeout(60);
                        sql.EnableRetryOnFailure();
                    })
                    .Options;

                using (var ctx = new BusBuddyDbContext(options))
                {
                    // If database already has data, skip unless explicitly forced
                    var forceSeed =
                        (args != null && Array.Exists(args, a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase))) ||
                        string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_FORCE_SEED"), "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_FORCE_SEED"), "1", StringComparison.OrdinalIgnoreCase);

                    try
                    {
                        var existingStudents = await ctx.Students.CountAsync();
                        if (existingStudents > 0 && !forceSeed)
                        {
                            Console.WriteLine($"ℹ️ Seeding skipped: database already has {existingStudents} students. Use --force or BUSBUDDY_FORCE_SEED=1 to reseed.");
                            return 0;
                        }
                    }
                    catch (Exception preCheckEx)
                    {
                        Console.WriteLine($"⚠️ Unable to check existing data: {preCheckEx.Message}. Continuing with seeding attempt...");
                    }

                    // Optional clean step (delete existing Students then Families) to avoid dedupe preventing inserts
                    // Trigger with either --clean arg or BUSBUDDY_CLEAN_BEFORE_SEED=true/1
                    var cleanRequested =
                        (args != null && Array.Exists(args, a => string.Equals(a, "--clean", StringComparison.OrdinalIgnoreCase))) ||
                        string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_CLEAN_BEFORE_SEED"), "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_CLEAN_BEFORE_SEED"), "1", StringComparison.OrdinalIgnoreCase);

                    if (cleanRequested)
                    {
                        Console.WriteLine("🧹 Cleaning existing data (Students -> Families) before seeding...");
                        try
                        {
                            // Delete children first to satisfy FK constraints
                            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Students");
                            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Families");
                            Console.WriteLine("✅ Clean complete.");
                        }
                        catch (Exception cleanEx)
                        {
                            Console.WriteLine($"⚠️ Clean step failed: {cleanEx.Message}");
                        }
                    }

                    var importResult = await JsonDataImporter.ImportStudentDataAsync(ctx, usedPath);
                    if (importResult.Success)
                    {
                        Console.WriteLine($"✅ Seeding complete. Added {importResult.ImportedStudents} students and {importResult.ImportedFamilies} families. Skipped {importResult.SkippedStudents} students, {importResult.SkippedFamilies} families.");
                        try
                        {
                            var sCount = await ctx.Students.CountAsync();
                            var fCount = await ctx.Families.CountAsync();
                            Console.WriteLine($"📈 Totals now: {sCount} students, {fCount} families.");
                        }
                        catch (Exception countEx)
                        {
                            Console.WriteLine($"⚠️ Unable to retrieve totals: {countEx.Message}");
                        }
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine("❌ Seeding failed:");
                        foreach (var msg in importResult.ErrorMessages)
                        {
                            Console.WriteLine("  - " + msg);
                        }
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error: {ex.Message}");
                Log.Error(ex, "Test failed");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    // Simplified data models for testing
    public class WileySchoolDistrictData
    {
        public WileyMetadata? Metadata { get; set; }
        public WileyFamily[]? Families { get; set; }
        public WileyStudent[]? Students { get; set; }
        public WileyRoute[]? Routes { get; set; }
        public WileyBusStop[]? BusStops { get; set; }
        public string[]? DataQualityNotes { get; set; }
    }

    public class WileyMetadata
    {
        public string? Version { get; set; }
        public string? Description { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public string? ExtractedDate { get; set; }
        public string? DataSource { get; set; }
    }

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
}
