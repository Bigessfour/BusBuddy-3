using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Phase 1 Data Seeding Service
/// Populates BusBuddy with real-world transportation data for demonstration
/// 15-20 drivers, 10-15 vehicles, 25-30 activities
/// </summary>
public class Phase1DataSeedingService
{
    private readonly BusBuddyDbContext _context;
    private static readonly ILogger Logger = Log.ForContext<Phase1DataSeedingService>();

    public Phase1DataSeedingService(BusBuddyDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Seeds the database with Phase 1 demonstration data
    /// </summary>
    public async Task SeedPhase1DataAsync()
    {
        try
        {
            Logger.Information("🗂️ Starting Phase 1 data seeding...");

            // Ensure database and tables are created
            Logger.Information("🔧 Ensuring database schema exists...");
            await _context.Database.EnsureCreatedAsync();
            Logger.Information("✅ Database schema ready");

            // Check if data already exists
            if (await _context.Drivers.AnyAsync())
            {
                Logger.Information("📊 Data already exists, skipping seeding");
                return;
            }

            await SeedDriversAsync();
            await SeedVehiclesAsync();
            await SeedActivitiesAsync();

            await _context.SaveChangesAsync();
            Logger.Information("✅ Phase 1 data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ Error during Phase 1 data seeding");
            throw;
        }
    }

    private async Task SeedDriversAsync()
    {
        Logger.Information("🚌 Seeding drivers...");

        var drivers = new[]
        {
            new Driver { DriverName = "Sarah Johnson", DriverPhone = "(555) 123-4567", DriverEmail = "sarah.johnson@schoolbus.com", Address = "123 Main St", City = "Springfield", State = "IL", Zip = "62701", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Michael Rodriguez", DriverPhone = "(555) 234-5678", DriverEmail = "m.rodriguez@schoolbus.com", Address = "456 Oak Ave", City = "Springfield", State = "IL", Zip = "62702", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Jennifer Williams", DriverPhone = "(555) 345-6789", DriverEmail = "j.williams@schoolbus.com", Address = "789 Pine Rd", City = "Springfield", State = "IL", Zip = "62703", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "David Chen", DriverPhone = "(555) 456-7890", DriverEmail = "david.chen@schoolbus.com", Address = "321 Elm St", City = "Springfield", State = "IL", Zip = "62704", DriversLicenceType = "CDL-B", TrainingComplete = false, Status = "Training" },
            new Driver { DriverName = "Lisa Thompson", DriverPhone = "(555) 567-8901", DriverEmail = "lisa.thompson@schoolbus.com", Address = "654 Maple Dr", City = "Springfield", State = "IL", Zip = "62705", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Robert Anderson", DriverPhone = "(555) 678-9012", DriverEmail = "r.anderson@schoolbus.com", Address = "987 Cedar Ln", City = "Springfield", State = "IL", Zip = "62706", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Maria Garcia", DriverPhone = "(555) 789-0123", DriverEmail = "maria.garcia@schoolbus.com", Address = "147 Birch Way", City = "Springfield", State = "IL", Zip = "62707", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "James Wilson", DriverPhone = "(555) 890-1234", DriverEmail = "james.wilson@schoolbus.com", Address = "258 Spruce St", City = "Springfield", State = "IL", Zip = "62708", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Patricia Davis", DriverPhone = "(555) 901-2345", DriverEmail = "p.davis@schoolbus.com", Address = "369 Ash Blvd", City = "Springfield", State = "IL", Zip = "62709", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Christopher Brown", DriverPhone = "(555) 012-3456", DriverEmail = "c.brown@schoolbus.com", Address = "741 Walnut Ave", City = "Springfield", State = "IL", Zip = "62710", DriversLicenceType = "CDL-B", TrainingComplete = false, Status = "Training" },
            new Driver { DriverName = "Amanda Miller", DriverPhone = "(555) 123-4567", DriverEmail = "amanda.miller@schoolbus.com", Address = "852 Hickory Rd", City = "Springfield", State = "IL", Zip = "62711", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Kevin Martinez", DriverPhone = "(555) 234-5678", DriverEmail = "k.martinez@schoolbus.com", Address = "963 Poplar Dr", City = "Springfield", State = "IL", Zip = "62712", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Susan Taylor", DriverPhone = "(555) 345-6789", DriverEmail = "susan.taylor@schoolbus.com", Address = "174 Willow Ln", City = "Springfield", State = "IL", Zip = "62713", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Daniel Lee", DriverPhone = "(555) 456-7890", DriverEmail = "daniel.lee@schoolbus.com", Address = "285 Sycamore St", City = "Springfield", State = "IL", Zip = "62714", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Michelle White", DriverPhone = "(555) 567-8901", DriverEmail = "m.white@schoolbus.com", Address = "396 Magnolia Way", City = "Springfield", State = "IL", Zip = "62715", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Thomas Jackson", DriverPhone = "(555) 678-9012", DriverEmail = "t.jackson@schoolbus.com", Address = "507 Dogwood Dr", City = "Springfield", State = "IL", Zip = "62716", DriversLicenceType = "CDL-B", TrainingComplete = false, Status = "Training" },
            new Driver { DriverName = "Rebecca Moore", DriverPhone = "(555) 789-0123", DriverEmail = "rebecca.moore@schoolbus.com", Address = "618 Redwood Ave", City = "Springfield", State = "IL", Zip = "62717", DriversLicenceType = "CDL-B", TrainingComplete = true, Status = "Active" },
            new Driver { DriverName = "Anthony Clark", DriverPhone = "(555) 890-1234", DriverEmail = "a.clark@schoolbus.com", Address = "729 Fir St", City = "Springfield", State = "IL", Zip = "62718", DriversLicenceType = "CDL-A", TrainingComplete = true, Status = "Active" }
        };

        await _context.Drivers.AddRangeAsync(drivers);
        Logger.Information($"✅ Added {drivers.Length} drivers");
    }

    private async Task SeedVehiclesAsync()
    {
        Logger.Information("🚐 Seeding vehicles...");

        var vehicles = new[]
        {
            new Bus { BusNumber = "SB-001", Make = "Blue Bird", Model = "Vision", LicenseNumber = "SB-001", SeatingCapacity = 72, VINNumber = "1BAANKCL7LF123456", Year = 2020, Status = "Active" },
            new Bus { BusNumber = "SB-002", Make = "IC Bus", Model = "CE Series", LicenseNumber = "SB-002", SeatingCapacity = 84, VINNumber = "1BAANKCL7LF123457", Year = 2019, Status = "Active" },
            new Bus { BusNumber = "SB-003", Make = "Thomas Built", Model = "Saf-T-Liner C2", LicenseNumber = "SB-003", SeatingCapacity = 77, VINNumber = "1BAANKCL7LF123458", Year = 2021, Status = "Active" },
            new Bus { BusNumber = "SB-004", Make = "Blue Bird", Model = "All American", LicenseNumber = "SB-004", SeatingCapacity = 90, VINNumber = "1BAANKCL7LF123459", Year = 2020, Status = "Active" },
            new Bus { BusNumber = "SB-005", Make = "IC Bus", Model = "RE Series", LicenseNumber = "SB-005", SeatingCapacity = 35, VINNumber = "1BAANKCL7LF123460", Year = 2018, Status = "Active" },
            new Bus { BusNumber = "SB-006", Make = "Thomas Built", Model = "Minotour", LicenseNumber = "SB-006", SeatingCapacity = 24, VINNumber = "1BAANKCL7LF123461", Year = 2022, Status = "Active" },
            new Bus { BusNumber = "SB-007", Make = "Blue Bird", Model = "Micro Bird", LicenseNumber = "SB-007", SeatingCapacity = 30, VINNumber = "1BAANKCL7LF123462", Year = 2021, Status = "Active" },
            new Bus { BusNumber = "SB-008", Make = "IC Bus", Model = "AC Series", LicenseNumber = "SB-008", SeatingCapacity = 48, VINNumber = "1BAANKCL7LF123463", Year = 2019, Status = "Active" },
            new Bus { BusNumber = "SB-009", Make = "Thomas Built", Model = "Saf-T-Liner HDX", LicenseNumber = "SB-009", SeatingCapacity = 81, VINNumber = "1BAANKCL7LF123464", Year = 2020, Status = "Active" },
            new Bus { BusNumber = "SB-010", Make = "Blue Bird", Model = "Vision", LicenseNumber = "SB-010", SeatingCapacity = 72, VINNumber = "1BAANKCL7LF123465", Year = 2021, Status = "Active" },
            new Bus { BusNumber = "SB-011", Make = "IC Bus", Model = "CE Series", LicenseNumber = "SB-011", SeatingCapacity = 84, VINNumber = "1BAANKCL7LF123466", Year = 2018, Status = "Active" },
            new Bus { BusNumber = "SB-012", Make = "Thomas Built", Model = "Saf-T-Liner C2", LicenseNumber = "SB-012", SeatingCapacity = 77, VINNumber = "1BAANKCL7LF123467", Year = 2022, Status = "Active" },
            new Bus { BusNumber = "SB-013", Make = "Blue Bird", Model = "All American", LicenseNumber = "SB-013", SeatingCapacity = 90, VINNumber = "1BAANKCL7LF123468", Year = 2020, Status = "Active" }
        };

        await _context.Vehicles.AddRangeAsync(vehicles);
        Logger.Information($"✅ Added {vehicles.Length} vehicles");
    }

    private async Task SeedActivitiesAsync()
    {
        Logger.Information("📅 Seeding activities...");

        var baseDate = DateTime.Today;
        var activities = new List<Activity>();

        // Morning Routes (7:00 AM - 8:30 AM)
        for (int day = 0; day < 5; day++) // Weekdays
        {
            var date = baseDate.AddDays(day);

            activities.AddRange(new[]
            {
                new Activity { ActivityType = "Transport", Description = "Elementary schools pickup route", Date = date, LeaveTime = new TimeSpan(7, 0, 0), EventTime = new TimeSpan(8, 0, 0), Destination = "Lincoln Elementary", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Transport", Description = "Middle school pickup route", Date = date, LeaveTime = new TimeSpan(7, 15, 0), EventTime = new TimeSpan(8, 15, 0), Destination = "Roosevelt Middle School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Transport", Description = "High school pickup route", Date = date, LeaveTime = new TimeSpan(6, 30, 0), EventTime = new TimeSpan(7, 30, 0), Destination = "Washington High School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 3 },
                new Activity { ActivityType = "Special Transport", Description = "Special needs students transport", Date = date, LeaveTime = new TimeSpan(7, 30, 0), EventTime = new TimeSpan(8, 30, 0), Destination = "Multiple Locations", RequestedBy = "Special Ed Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Field Trip", Description = "5th grade field trip to science museum", Date = date, LeaveTime = new TimeSpan(9, 0, 0), EventTime = new TimeSpan(15, 0, 0), Destination = "Science Museum", RequestedBy = "Ms. Johnson", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Athletics", Description = "Soccer team away game transport", Date = date, LeaveTime = new TimeSpan(14, 0, 0), EventTime = new TimeSpan(18, 0, 0), Destination = "Away School", RequestedBy = "Coach Smith", Status = "Scheduled", AssignedVehicleId = 3 }
            });
        }

        // Afternoon Routes (2:30 PM - 4:00 PM)
        for (int day = 0; day < 5; day++) // Weekdays
        {
            var date = baseDate.AddDays(day);

            activities.AddRange(new[]
            {
                new Activity { ActivityType = "Transport", Description = "Elementary schools dropoff route", Date = date, LeaveTime = new TimeSpan(14, 30, 0), EventTime = new TimeSpan(15, 30, 0), Destination = "Lincoln Elementary", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Transport", Description = "Middle school dropoff route", Date = date, LeaveTime = new TimeSpan(15, 0, 0), EventTime = new TimeSpan(16, 0, 0), Destination = "Roosevelt Middle School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Transport", Description = "High school dropoff route", Date = date, LeaveTime = new TimeSpan(15, 15, 0), EventTime = new TimeSpan(16, 15, 0), Destination = "Washington High School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 3 },
                new Activity { ActivityType = "Special Transport", Description = "Special needs students return transport", Date = date, LeaveTime = new TimeSpan(15, 30, 0), EventTime = new TimeSpan(16, 30, 0), Destination = "Multiple Locations", RequestedBy = "Special Ed Dept", Status = "Scheduled", AssignedVehicleId = 1 }
            });
        }

        await _context.Activities.AddRangeAsync(activities);
        Logger.Information($"✅ Added {activities.Count} activities");
    }

    /// <summary>
    /// Gets a summary of seeded data for verification
    /// </summary>
    public async Task<string> GetDataSummaryAsync()
    {
        var driverCount = await _context.Drivers.CountAsync();
        var vehicleCount = await _context.Vehicles.CountAsync();
        var activityCount = await _context.Activities.CountAsync();

        return $"📊 Phase 1 Data Summary:\n" +
               $"🚌 Drivers: {driverCount}\n" +
               $"🚐 Vehicles: {vehicleCount}\n" +
               $"📅 Activities: {activityCount}";
    }
}
