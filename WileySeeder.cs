using System;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WileySeeder;

/// <summary>
/// Console application for seeding Wiley School District data
/// Uses dependency injection and resilient database patterns
/// </summary>
class Program
{
    private static readonly string LogFile = "runtime-errors-fixed.log";

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(LogFile, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("🚌 Starting Wiley School District Data Seeder");

            // Build host with dependency injection
            var host = CreateHostBuilder(args).Build();

            // Run the seeding operation
            var exitCode = await RunSeedingOperation(host);

            Log.Information("🚌 Wiley Seeder completed with exit code: {ExitCode}", exitCode);
            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "🚨 Fatal error in Wiley Seeder");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Add configuration
                var configuration = context.Configuration;

                // Add Entity Framework
                services.AddDbContextFactory<BusBuddyDbContext>(options =>
                {
                    var connectionString = configuration.GetConnectionString("DefaultConnection")
                        ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";
                    options.UseSqlServer(connectionString);
                });

                // Add context factory
                services.AddScoped<IBusBuddyDbContextFactory, BusBuddyDbContextFactory>();

                // Add services
                services.AddScoped<IStudentService, StudentService>();
                services.AddScoped<SeedDataService>();
            });

    static async Task<int> RunSeedingOperation(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            Log.Information("🔧 Initializing services and database");

            // Get services
            var contextFactory = services.GetRequiredService<IBusBuddyDbContextFactory>();
            var seedDataService = services.GetRequiredService<SeedDataService>();

            // Ensure database is migrated
            using (LogContext.PushProperty("Operation", "DatabaseMigration"))
            {
                Log.Information("🗄️ Ensuring database migrations are applied");
                using var context = contextFactory.CreateDbContext();
                await context.Database.MigrateAsync();
                Log.Information("✅ Database migrations completed");
            }

            // Check if data file exists
            var dataFile = Path.Combine("BusBuddy.Core", "Data", "wiley-school-district-data.json");
            if (!File.Exists(dataFile))
            {
                Log.Error("❌ Data file not found: {DataFile}", dataFile);
                return 1;
            }

            Log.Information("📂 Found data file: {DataFile}", dataFile);

            // Execute seeding with resilient patterns
            var result = await ResilientDbExecution.ExecuteWithResilienceAsync(
                async () => await seedDataService.SeedWileySchoolDistrictDataAsync(),
                "WileySchoolDistrictSeeding",
                maxRetries: 3
            );

            if (result.Success)
            {
                Log.Information("✅ Seeding completed successfully!");
                Log.Information("📊 Results: {StudentsSeeded} students, {FamiliesProcessed} families processed",
                    result.StudentsSeeded, result.FamiliesProcessed);
                Log.Information("⏱️ Duration: {Duration}", result.Duration);

                // Verify data was inserted
                await VerifyDataInsertion(contextFactory);

                return 0;
            }
            else
            {
                Log.Error("❌ Seeding failed: {ErrorMessage}", result.ErrorMessage);
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 Unexpected error during seeding operation");
            return 1;
        }
    }

    static async Task VerifyDataInsertion(IBusBuddyDbContextFactory contextFactory)
    {
        using (LogContext.PushProperty("Operation", "DataVerification"))
        {
            Log.Information("🔍 Verifying data insertion");

            try
            {
                using var context = contextFactory.CreateDbContext();

                // Count students
                var studentCount = await context.Students
                    .Where(s => s.School == "Wiley School District")
                    .CountAsync();

                // Count routes
                var routeCount = await context.Routes
                    .Where(r => r.School == "Wiley School District")
                    .CountAsync();

                // Count vehicles
                var vehicleCount = await context.Vehicles.CountAsync();

                Log.Information("📊 Verification Results:");
                Log.Information("   👥 Wiley Students: {StudentCount}", studentCount);
                Log.Information("   🚌 Wiley Routes: {RouteCount}", routeCount);
                Log.Information("   🚐 Total Vehicles: {VehicleCount}", vehicleCount);

                // Verify expected student count
                if (studentCount >= 5)
                {
                    Log.Information("✅ Expected student count verified (>= 5 students)");
                }
                else
                {
                    Log.Warning("⚠️ Lower than expected student count: {StudentCount}", studentCount);
                }

                // Sample some student data
                var sampleStudents = await context.Students
                    .Where(s => s.School == "Wiley School District")
                    .Take(3)
                    .Select(s => new { s.StudentName, s.Grade, s.HomeAddress })
                    .ToListAsync();

                Log.Information("📝 Sample Students:");
                foreach (var student in sampleStudents)
                {
                    Log.Information("   • {StudentName} (Grade {Grade}) - {Address}",
                        student.StudentName, student.Grade, student.HomeAddress);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error during data verification");
            }
        }
    }
}
