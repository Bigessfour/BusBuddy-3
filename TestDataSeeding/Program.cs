using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Serilog;

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
                var dataPath = Path.Combine("..", "..", "..", "BusBuddy.Core", "Data", "wiley-school-district-data.json");

                if (!File.Exists(dataPath))
                {
                    Console.WriteLine($"❌ Data file not found: {dataPath}");
                    return 1;
                }

                Console.WriteLine($"✅ Found data file: {dataPath}");

                // Try to load and parse the JSON
                var jsonContent = await File.ReadAllTextAsync(dataPath);
                var wileyData = JsonSerializer.Deserialize<WileySchoolDistrictData>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (wileyData?.Students == null)
                {
                    Console.WriteLine("❌ No student data found in file");
                    return 1;
                }

                Console.WriteLine($"✅ Loaded {wileyData.Students.Length} students from {wileyData.Families?.Length ?? 0} families");

                // Show data quality summary
                var goodCount = 0;
                var poorCount = 0;

                foreach (var student in wileyData.Students)
                {
                    if (student.DataQuality == "poor")
                        poorCount++;
                    else
                        goodCount++;
                }

                Console.WriteLine($"📊 Data Quality: {goodCount} good/partial quality, {poorCount} poor quality");
                Console.WriteLine();

                // Show sample students
                Console.WriteLine("👥 Sample Students:");
                var count = 0;
                foreach (var student in wileyData.Students)
                {
                    if (count >= 3) break;
                    Console.WriteLine($"  • {student.StudentName} - {student.City}, {student.State} (Quality: {student.DataQuality})");
                    count++;
                }

                Console.WriteLine();
                Console.WriteLine("✅ JSON data structure validation successful!");
                Console.WriteLine("📝 Ready for database seeding when build issues are resolved.");

                return 0;
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
