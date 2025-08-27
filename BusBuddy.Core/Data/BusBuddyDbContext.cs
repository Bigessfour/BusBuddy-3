using System.IO;
using System.Text.RegularExpressions;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Domain.Trips;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Data
{
    /// <summary>
    /// Enhanced Entity Framework DbContext for BusBuddy application
    /// Supports agile schema evolution with audit fields, soft deletes, and JSON columns
    /// Optimized for Syncfusion Windows Forms with comprehensive indexing and performance features
    /// </summary>
    public class BusBuddyDbContext : DbContext
    {
    /// <summary>
    /// Helper for test code: seed minimal data for a test scenario
    /// </summary>
    public static void SeedTestData(BusBuddyDbContext context, Action<BusBuddyDbContext> seedAction)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(seedAction);
        seedAction(context);
        _ = context.SaveChanges();
    }

    /// <summary>
    /// Controls whether to skip global data seeding (for test isolation)
    /// </summary>
    public static bool SkipGlobalSeedData { get; set; }

    private string _currentAuditUser = "System";

    public BusBuddyDbContext() { }

    public BusBuddyDbContext(DbContextOptions<BusBuddyDbContext> options) : base(options)
    {
        // Configure EF Core tracking behavior for better performance
        // Use default tracking for test scenarios to avoid issues with seeding
        if (options.Extensions.Any(e => e.GetType().Name.Contains("InMemory")))
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            SkipGlobalSeedData = true; // Skip global seeding for in-memory databases
        }
        else
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
    }

    /// <summary>
    /// Set the current audit user for tracking purposes
    /// </summary>
    public void SetAuditUser(string userName)
    {
        _currentAuditUser = userName ?? "System";
    }

    /// <summary>
    /// Get the current audit user
    /// </summary>
    public string GetCurrentAuditUser() => _currentAuditUser;

    // DbSets for all entities
    public virtual DbSet<Bus> Buses { get; set; } = null!;
    public virtual DbSet<ActivityLog> ActivityLogs { get; set; } = null!;
    public virtual DbSet<Driver> Drivers { get; set; } = null!;
    public virtual DbSet<Route> Routes { get; set; } = null!;
    public virtual DbSet<Activity> Activities { get; set; } = null!;
    public virtual DbSet<Fuel> FuelRecords { get; set; } = null!;
    public virtual DbSet<Maintenance> MaintenanceRecords { get; set; } = null!;
    public virtual DbSet<Student> Students { get; set; } = null!;
    public virtual DbSet<Family> Families { get; set; } = null!;
    public virtual DbSet<Schedule> Schedules { get; set; } = null!;
    public virtual DbSet<StudentSchedule> StudentSchedules { get; set; } = null!;
    public virtual DbSet<TripEvent> TripEvents { get; set; } = null!;
    public virtual DbSet<RouteStop> RouteStops { get; set; } = null!;
    public virtual DbSet<SchoolCalendar> SchoolCalendar { get; set; } = null!;
    public virtual DbSet<ActivitySchedule> ActivitySchedule { get; set; } = null!;
    // Removed legacy SportsEvents DbSet; use canonical DbContext only
    public virtual DbSet<Destination> Destinations { get; set; } = null!;
    public virtual DbSet<RouteAssignment> RouteAssignments { get; set; } = null!;

    public virtual DbSet<SportsEvent> SportsEvents { get; set; } = null!;
    public virtual DbSet<AIInsight> AIInsights { get; set; } = null!;

    // Compatibility aliases for legacy code
    // Removed legacy Vehicles property; use Buses only
    public virtual DbSet<Fuel> Fuels => FuelRecords;
    public virtual DbSet<Maintenance> Maintenances => MaintenanceRecords;
    public virtual DbSet<ActivitySchedule> ActivitySchedules => ActivitySchedule;

    // REMOVED: DbSet<Ticket> Tickets - deprecated module

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (optionsBuilder.IsConfigured)
            return;

        var logger = Log.ForContext<BusBuddyDbContext>();
        logger.Information("Starting DbContext configuration");

        try
        {
            var connectionString = GetConnectionString(logger);
            ConfigureDatabase(optionsBuilder, connectionString, logger);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to configure database connection, falling back to LocalDB");
            UseLocalDbFallback(optionsBuilder, logger);
        }
    }

    private static string GetConnectionString(ILogger logger)
    {
        // 1. Environment variable override (highest priority) — simple and fast escape path
        var envOverride = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            logger.Information("Using BUSBUDDY_CONNECTION environment override");
            return ExpandEnvironmentVariables(envOverride, logger);
        }

        // 2. Load configuration (base + environment specific + environment variables)
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    var raw = config.GetConnectionString("BusBuddyDb") ?? config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var chosen = config.GetConnectionString("BusBuddyDb") != null ? "BusBuddyDb" : "DefaultConnection";
            logger.Information("Using connection string from configuration: {ConnectionName}", chosen);
            return ExpandEnvironmentVariables(raw, logger);
    }

        // Nothing found — log diagnostic details then throw
        var busBuddyDb = config.GetConnectionString("BusBuddyDb");
        var defaultConn = config.GetConnectionString("DefaultConnection");
        logger.Warning("Missing connection strings: BusBuddyDb={BusBuddyDb}, DefaultConnection={DefaultConnection}", busBuddyDb, defaultConn);
        throw new InvalidOperationException("No valid connection string found in configuration");
    }

    private static string ExpandEnvironmentVariables(string connectionString, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

    var expanded = Environment.ExpandEnvironmentVariables(connectionString); // expand %VAR% style

        // Expand ${VAR} style variables
        expanded = Regex.Replace(expanded, @"\$\{(?<name>[A-Za-z0-9_]+)\}", match =>
        {
            var name = match.Groups["name"].Value;
            var value = Environment.GetEnvironmentVariable(name);
            return value ?? match.Value;
        });

        if (!string.Equals(connectionString, expanded, StringComparison.Ordinal))
        {
            logger.Information("Expanded environment variables in connection string");
        }

        return expanded;
    }

    private static void ConfigureDatabase(DbContextOptionsBuilder optionsBuilder, string connectionString, ILogger logger)
    {
        // Check database provider from configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var databaseProvider = config["DatabaseProvider"] ?? config["AppSettings:DatabaseProvider"] ?? "Azure";
        var useInMemorySqlite = bool.Parse(config["AppSettings:UseInMemorySqlite"] ?? "false");

        logger.Information("Configuring database with provider: {Provider}, UseInMemorySqlite: {UseInMemory}", databaseProvider, useInMemorySqlite);

        if (useInMemorySqlite || databaseProvider == "Sqlite" || databaseProvider == "InMemory")
        {
            // Use SQLite for testing and in-memory scenarios
            optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(60);
            });
            logger.Information("Using SQLite database provider");
        }
        else
        {
            // Use SQL Server for production
            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(60);
                // Note: EnableRetryOnFailure removed for EF Core 9 compatibility - will be re-added after research
            });
            logger.Information("Using SQL Server database provider");
        }

        // Seeding temporarily disabled for fresh start
        // ConfigureSeedingIfNeeded(optionsBuilder);
        ConfigureEfLogging(optionsBuilder);
    }

    private static void ConfigureSeedingIfNeeded(DbContextOptionsBuilder optionsBuilder)
    {
        if (SkipGlobalSeedData)
            return;

        // Seeding configuration will be implemented after EF Core 9 compatibility is resolved
        // For now, focusing on clean migration setup
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // ActivityLog entity
        _ = modelBuilder.Entity<ActivityLog>(entity =>
        {
            _ = entity.ToTable("ActivityLogs");
            _ = entity.HasKey(e => e.Id);
            _ = entity.Property(e => e.Timestamp).IsRequired();
            _ = entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            _ = entity.Property(e => e.User).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.Details).HasMaxLength(1000);

            // Add index on Timestamp for better query performance
            _ = entity.HasIndex(e => e.Timestamp)
                .IsDescending();
        });

        // Configure global query filters for soft deletes
        ConfigureGlobalQueryFilters(modelBuilder);

        // Configure global NULL handling for better error resilience
        ConfigureNullHandling(modelBuilder);

        // Configure Bus entity with proper table name
        _ = modelBuilder.Entity<Bus>(entity =>
        {
            _ = entity.ToTable("Buses"); // Use proper Buses table name
            _ = entity.HasKey(e => e.BusId); // Primary key

            // Properties with validation and constraints
            _ = entity.Property(e => e.BusNumber).IsRequired().HasMaxLength(20);
            _ = entity.Property(e => e.VINNumber).IsRequired().HasMaxLength(17);
            _ = entity.Property(e => e.LicenseNumber).IsRequired().HasMaxLength(20);
            _ = entity.Property(e => e.Make).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.Model).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Active");
            _ = entity.Property(e => e.FleetType).HasMaxLength(20);
            _ = entity.Property(e => e.FuelType).HasMaxLength(20);
            _ = entity.Property(e => e.Department).HasMaxLength(50);
            _ = entity.Property(e => e.GPSDeviceId).HasMaxLength(100);

            // Decimal properties with precision
            _ = entity.Property(e => e.PurchasePrice).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.FuelCapacity).HasColumnType("decimal(8,2)");
            _ = entity.Property(e => e.MilesPerGallon).HasColumnType("decimal(6,2)");

            // Text properties
            _ = entity.Property(e => e.InsurancePolicyNumber).HasMaxLength(100);
            _ = entity.Property(e => e.SpecialEquipment).HasMaxLength(1000);
            _ = entity.Property(e => e.Notes).HasMaxLength(1000);

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Unique constraints (indexes enforcing uniqueness)
            _ = entity.HasIndex(e => e.BusNumber).IsUnique(); // IX_Buses_BusNumber
            _ = entity.HasIndex(e => e.VINNumber).IsUnique(); // IX_Buses_VINNumber
            _ = entity.HasIndex(e => e.LicenseNumber).IsUnique(); // IX_Buses_LicenseNumber

            // Performance indexes
            _ = entity.HasIndex(e => e.Status); // IX_Buses_Status
            _ = entity.HasIndex(e => e.DateLastInspection); // IX_Buses_DateLastInspection
            _ = entity.HasIndex(e => e.InsuranceExpiryDate); // IX_Buses_InsuranceExpiryDate
            _ = entity.HasIndex(e => e.FleetType); // IX_Buses_FleetType
            _ = entity.HasIndex(e => new { e.Make, e.Model, e.Year }); // IX_Buses_MakeModelYear
            // EF Core index additions (post-refactor) — documentation pattern: https://learn.microsoft.com/ef/core/modeling/indexes
            _ = entity.HasIndex(e => e.Department); // IX_Buses_Department
            _ = entity.HasIndex(e => new { e.Status, e.Department }); // IX_Buses_StatusDepartment composite for filtered queries

            // Geo columns
            _ = entity.Property(e => e.CurrentLatitude).HasColumnType("decimal(10,8)");
            _ = entity.Property(e => e.CurrentLongitude).HasColumnType("decimal(11,8)");
            _ = entity.HasIndex(e => new { e.CurrentLatitude, e.CurrentLongitude }); // IX_Buses_CurrentLocation
        });

        // Configure Driver entity with enhanced features
        _ = modelBuilder.Entity<Driver>(entity =>
        {
            _ = entity.ToTable("Drivers");
            _ = entity.HasKey(e => e.DriverId);
            _ = entity.Property(e => e.DriverId).HasColumnName("DriverID");

            // Properties
            _ = entity.Property(e => e.DriverName).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.DriverPhone).HasMaxLength(20);
            _ = entity.Property(e => e.DriverEmail).HasMaxLength(100);
            _ = entity.Property(e => e.DriversLicenceType).HasMaxLength(20).HasColumnName("DriversLicenseType");
            _ = entity.Property(e => e.Address).HasMaxLength(200);
            _ = entity.Property(e => e.City).HasMaxLength(50);
            _ = entity.Property(e => e.State).HasMaxLength(20);
            _ = entity.Property(e => e.Zip).HasMaxLength(10);
            _ = entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
            _ = entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
            _ = entity.Property(e => e.Notes).HasMaxLength(1000);

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Indexes
            _ = entity.HasIndex(e => e.DriverEmail).HasDatabaseName("IX_Drivers_Email");
            _ = entity.HasIndex(e => e.DriverPhone).HasDatabaseName("IX_Drivers_Phone");
            _ = entity.HasIndex(e => e.DriversLicenceType).HasDatabaseName("IX_Drivers_LicenseType");
            _ = entity.HasIndex(e => e.LicenseExpiryDate).HasDatabaseName("IX_Drivers_LicenseExpiration");
            _ = entity.HasIndex(e => e.TrainingComplete).HasDatabaseName("IX_Drivers_TrainingComplete");

            // Geo columns
            _ = entity.Property(e => e.HomeLatitude).HasColumnType("decimal(10,8)");
            _ = entity.Property(e => e.HomeLongitude).HasColumnType("decimal(11,8)");
            _ = entity.HasIndex(e => new { e.HomeLatitude, e.HomeLongitude }).HasDatabaseName("IX_Drivers_HomeLocation");
        });

        // Configure Route entity with enhanced relationships and indexing
        _ = modelBuilder.Entity<Route>(entity =>
        {
            _ = entity.ToTable("Routes");
            _ = entity.HasKey(e => e.RouteId);
            _ = entity.Property(e => e.RouteId).HasColumnName("RouteID");

            // Properties
            _ = entity.Property(e => e.RouteName).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.Description).HasMaxLength(500);

            // Foreign key column mappings
            _ = entity.Property(e => e.AMVehicleId).HasColumnName("AMVehicleID");
            _ = entity.Property(e => e.AMDriverId).HasColumnName("AMDriverID");
            _ = entity.Property(e => e.PMVehicleId).HasColumnName("PMVehicleID");
            _ = entity.Property(e => e.PMDriverId).HasColumnName("PMDriverID");

            // Decimal properties
            _ = entity.Property(e => e.AMBeginMiles).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.AMEndMiles).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.PMBeginMiles).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.PMEndMiles).HasColumnType("decimal(10,2)");

            // Configure AM relationships
            _ = entity.HasOne(r => r.AMVehicle)
                  .WithMany(v => v.AMRoutes)
                  .HasForeignKey(r => r.AMVehicleId)
                  .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Routes_AMVehicle");

            _ = entity.HasOne(r => r.AMDriver)
                  .WithMany(d => d.AMRoutes)
                  .HasForeignKey(r => r.AMDriverId)
                  .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Routes_AMDriver");

            // Configure PM relationships
            _ = entity.HasOne(r => r.PMVehicle)
                  .WithMany(v => v.PMRoutes)
                  .HasForeignKey(r => r.PMVehicleId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Routes_PMVehicle");

            _ = entity.HasOne(r => r.PMDriver)
                  .WithMany(d => d.PMRoutes)
                  .HasForeignKey(r => r.PMDriverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Routes_PMDriver");

            // Indexes for performance
            _ = entity.HasIndex(e => e.Date).HasDatabaseName("IX_Routes_Date");
            _ = entity.HasIndex(e => e.RouteName).HasDatabaseName("IX_Routes_RouteName");
            _ = entity.HasIndex(e => new { e.Date, e.RouteName }).IsUnique().HasDatabaseName("IX_Routes_DateRouteName");
            _ = entity.HasIndex(e => e.AMVehicleId).HasDatabaseName("IX_Routes_AMVehicleId");
            _ = entity.HasIndex(e => e.PMVehicleId).HasDatabaseName("IX_Routes_PMVehicleId");
            _ = entity.HasIndex(e => e.AMDriverId).HasDatabaseName("IX_Routes_AMDriverId");
            _ = entity.HasIndex(e => e.PMDriverId).HasDatabaseName("IX_Routes_PMDriverId");

            // Geo metadata
            _ = entity.Property(e => e.WaypointsJson).HasMaxLength(4000);
            _ = entity.Property(e => e.DistrictBoundaryShapefilePath).HasMaxLength(500);
            _ = entity.Property(e => e.TownBoundaryShapefilePath).HasMaxLength(500);
        });

        // Configure Activity entity with comprehensive indexing
        _ = modelBuilder.Entity<Activity>(entity =>
        {
            _ = entity.ToTable("Activities");
            _ = entity.HasKey(e => e.ActivityId);

            // Properties
            _ = entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            _ = entity.Property(e => e.RequestedBy).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Scheduled");
            _ = entity.Property(e => e.Notes).HasMaxLength(500);
            _ = entity.Property(e => e.ActivityCategory).HasMaxLength(100);
            _ = entity.Property(e => e.ApprovedBy).HasMaxLength(100);

            // Decimal properties
            _ = entity.Property(e => e.EstimatedCost).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.ActualCost).HasColumnType("decimal(10,2)");

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Relationships
            _ = entity.HasOne(a => a.AssignedVehicle)
                  .WithMany(v => v.Activities)
                  .HasForeignKey(a => a.AssignedVehicleId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Activities_Vehicle");

            _ = entity.HasOne(a => a.Driver)
                  .WithMany(d => d.Activities)
                  .HasForeignKey(a => a.DriverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Activities_Driver");

            _ = entity.HasOne(a => a.Route)
                  .WithMany()
                  .HasForeignKey(a => a.RouteId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Activities_Route");

            // Indexes for scheduling and performance
            _ = entity.HasIndex(e => e.Date).HasDatabaseName("IX_Activities_Date");
            _ = entity.HasIndex(e => e.ActivityType).HasDatabaseName("IX_Activities_ActivityType");
            _ = entity.HasIndex(e => e.Status).HasDatabaseName("IX_Activities_Status");
            _ = entity.HasIndex(e => e.AssignedVehicleId).HasDatabaseName("IX_Activities_VehicleId");
            _ = entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_Activities_DriverId");
            _ = entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_Activities_RouteId");
            _ = entity.HasIndex(e => new { e.Date, e.LeaveTime, e.EventTime }).HasDatabaseName("IX_Activities_DateTimeRange");
            _ = entity.HasIndex(e => new { e.AssignedVehicleId, e.Date, e.LeaveTime }).HasDatabaseName("IX_Activities_BusSchedule");
            _ = entity.HasIndex(e => new { e.DriverId, e.Date, e.LeaveTime }).HasDatabaseName("IX_Activities_DriverSchedule");
            _ = entity.HasIndex(e => e.ApprovalRequired).HasDatabaseName("IX_Activities_ApprovalRequired");
        });

        // Configure Fuel entity
        _ = modelBuilder.Entity<Fuel>(entity =>
        {
            _ = entity.ToTable("Fuel");
            _ = entity.HasKey(e => e.FuelId);

            // Properties
            _ = entity.Property(e => e.FuelLocation).HasMaxLength(100);
            _ = entity.Property(e => e.FuelType).HasMaxLength(20).HasDefaultValue("Gasoline");
            _ = entity.Property(e => e.Notes).HasMaxLength(500);

            // Decimal properties with precision
            _ = entity.Property(e => e.Gallons).HasColumnType("decimal(8,3)");
            _ = entity.Property(e => e.PricePerGallon).HasColumnType("decimal(8,3)");
            _ = entity.Property(e => e.TotalCost).HasColumnType("decimal(10,2)");

            // Relationships
            _ = entity.HasOne(f => f.Vehicle)
                  .WithMany(v => v.FuelRecords)
                  .HasForeignKey(f => f.VehicleFueledId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Fuel_Vehicle");

            // Indexes
            _ = entity.HasIndex(e => e.FuelDate).HasDatabaseName("IX_Fuel_FuelDate");
            _ = entity.HasIndex(e => e.VehicleFueledId).HasDatabaseName("IX_Fuel_VehicleId");
            _ = entity.HasIndex(e => new { e.VehicleFueledId, e.FuelDate }).HasDatabaseName("IX_Fuel_VehicleDate");
            _ = entity.HasIndex(e => e.FuelLocation).HasDatabaseName("IX_Fuel_Location");
            _ = entity.HasIndex(e => e.FuelType).HasDatabaseName("IX_Fuel_Type");
        });

        // Configure Maintenance entity
        _ = modelBuilder.Entity<Maintenance>(entity =>
        {
            _ = entity.ToTable("Maintenance");
            _ = entity.HasKey(e => e.MaintenanceId);

            // Properties
            _ = entity.Property(e => e.MaintenanceCompleted).HasMaxLength(100);
            _ = entity.Property(e => e.Vendor).HasMaxLength(100);
            _ = entity.Property(e => e.Description).HasMaxLength(500);
            _ = entity.Property(e => e.Notes).HasMaxLength(1000);
            _ = entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Normal");

            // Decimal properties
            _ = entity.Property(e => e.RepairCost).HasColumnType("decimal(10,2)");

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Relationships
            _ = entity.HasOne(m => m.Vehicle)
                  .WithMany(v => v.MaintenanceRecords)
                  .HasForeignKey(m => m.VehicleId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Maintenance_Vehicle");

            // Indexes
            _ = entity.HasIndex(e => e.Date).HasDatabaseName("IX_Maintenance_Date");
            _ = entity.HasIndex(e => e.VehicleId).HasDatabaseName("IX_Maintenance_VehicleId");
            _ = entity.HasIndex(e => e.MaintenanceCompleted).HasDatabaseName("IX_Maintenance_Type");
            _ = entity.HasIndex(e => new { e.VehicleId, e.Date }).HasDatabaseName("IX_Maintenance_VehicleDate");
            _ = entity.HasIndex(e => e.Priority).HasDatabaseName("IX_Maintenance_Priority");
        });

        // Configure Student entity
        _ = modelBuilder.Entity<Student>(entity =>
        {
            _ = entity.ToTable("Students");
            _ = entity.HasKey(e => e.StudentId);

            // Properties
            _ = entity.Property(e => e.StudentName).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.Grade).HasMaxLength(20);
            _ = entity.Property(e => e.MedicalNotes).HasMaxLength(1000);
            _ = entity.Property(e => e.TransportationNotes).HasMaxLength(1000);
            _ = entity.Property(e => e.EmergencyPhone).HasMaxLength(20);
            _ = entity.Property(e => e.School).HasMaxLength(100);
            _ = entity.Property(e => e.ParentGuardian).HasMaxLength(100);
            _ = entity.Property(e => e.HomeAddress).HasMaxLength(200);
            _ = entity.Property(e => e.City).HasMaxLength(50);
            _ = entity.Property(e => e.State).HasMaxLength(2);
            _ = entity.Property(e => e.Zip).HasMaxLength(10);

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Indexes
            _ = entity.HasIndex(e => e.StudentName).HasDatabaseName("IX_Students_Name");
            _ = entity.HasIndex(e => e.Grade).HasDatabaseName("IX_Students_Grade");
            _ = entity.HasIndex(e => e.School).HasDatabaseName("IX_Students_School");
            _ = entity.HasIndex(e => e.Active).HasDatabaseName("IX_Students_Active");
        });

        // Configure Family entity for JSON import
        _ = modelBuilder.Entity<Family>(entity =>
        {
            _ = entity.ToTable("Families");
            _ = entity.HasKey(e => e.FamilyId);

            // Properties
            _ = entity.Property(e => e.ParentGuardian).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.Address).IsRequired().HasMaxLength(200);
            _ = entity.Property(e => e.City).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.County).HasMaxLength(50);
            _ = entity.Property(e => e.HomePhone).HasMaxLength(20);
            _ = entity.Property(e => e.CellPhone).HasMaxLength(20);
            _ = entity.Property(e => e.EmergencyContact).HasMaxLength(100);
            _ = entity.Property(e => e.JointParent).HasMaxLength(100);

            // Audit fields
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Indexes
            _ = entity.HasIndex(e => new { e.ParentGuardian, e.Address }).HasDatabaseName("IX_Families_ParentAddress");
            _ = entity.HasIndex(e => e.City).HasDatabaseName("IX_Families_City");

            // Relationships - One Family has many Students
            _ = entity.HasMany(f => f.Students)
                  .WithOne(s => s.Family)
                  .HasForeignKey(s => s.FamilyId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_Students_Family");
        });

        // Configure Schedule entity
        _ = modelBuilder.Entity<Schedule>(entity =>
        {
            _ = entity.ToTable("Schedules");
            _ = entity.HasKey(e => e.ScheduleId);

            // BusId properly references the Buses table
            _ = entity.Property(e => e.BusId).IsRequired();

            // Relationships
            _ = entity.HasOne(s => s.Bus)
                  .WithMany(b => b.Schedules)
                  .HasForeignKey(s => s.BusId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Schedules_Bus");

            _ = entity.HasOne(s => s.Route)
                  .WithMany(r => r.Schedules)
                  .HasForeignKey(s => s.RouteId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Schedules_Route");

            _ = entity.HasOne(s => s.Driver)
                  .WithMany(d => d.Schedules)
                  .HasForeignKey(s => s.DriverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Schedules_Driver");

            // Indexes
            _ = entity.HasIndex(e => new { e.RouteId, e.BusId, e.DepartureTime }).IsUnique().HasDatabaseName("IX_Schedules_RouteBusDeparture");
            _ = entity.HasIndex(e => e.ScheduleDate).HasDatabaseName("IX_Schedules_Date");
            _ = entity.HasIndex(e => e.BusId).HasDatabaseName("IX_Schedules_BusId");
            _ = entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_Schedules_DriverId");
            _ = entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_Schedules_RouteId");
        });

        // Configure StudentSchedule entity
        _ = modelBuilder.Entity<StudentSchedule>(entity =>
        {
            _ = entity.ToTable("StudentSchedules");
            _ = entity.HasKey(e => e.StudentScheduleId);

            // Properties
            _ = entity.Property(e => e.AssignmentType).IsRequired().HasMaxLength(20);
            _ = entity.Property(e => e.PickupLocation).HasMaxLength(100);
            _ = entity.Property(e => e.DropoffLocation).HasMaxLength(100);
            _ = entity.Property(e => e.Notes).HasMaxLength(500);
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            // Relationships
            _ = entity.HasOne(ss => ss.Student)
                  .WithMany(s => s.StudentSchedules)
                  .HasForeignKey(ss => ss.StudentId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_StudentSchedules_Student");

            _ = entity.HasOne(ss => ss.Schedule)
                  .WithMany(s => s.StudentSchedules)
                  .HasForeignKey(ss => ss.ScheduleId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_StudentSchedules_Schedule");

            _ = entity.HasOne(ss => ss.ActivitySchedule)
                  .WithMany(a => a.StudentSchedules)
                  .HasForeignKey(ss => ss.ActivityScheduleId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_StudentSchedules_ActivitySchedule");

            // Indexes for performance
            _ = entity.HasIndex(e => e.StudentId).HasDatabaseName("IX_StudentSchedules_StudentId");
            _ = entity.HasIndex(e => e.ScheduleId).HasDatabaseName("IX_StudentSchedules_ScheduleId");
            _ = entity.HasIndex(e => e.ActivityScheduleId).HasDatabaseName("IX_StudentSchedules_ActivityScheduleId");
            _ = entity.HasIndex(e => e.AssignmentType).HasDatabaseName("IX_StudentSchedules_AssignmentType");
            _ = entity.HasIndex(e => new { e.StudentId, e.ScheduleId }).IsUnique().HasDatabaseName("IX_StudentSchedules_StudentSchedule");
        });

        // Configure TripEvent entity
        _ = modelBuilder.Entity<TripEvent>(entity =>
        {
            _ = entity.ToTable("TripEvents");
            _ = entity.HasKey(e => e.TripEventId);

            // Properties
            _ = entity.Property(e => e.Type).IsRequired();
            _ = entity.Property(e => e.CustomType).HasMaxLength(100);
            _ = entity.Property(e => e.POCName).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.POCPhone).HasMaxLength(20);
            _ = entity.Property(e => e.POCEmail).HasMaxLength(100);
            _ = entity.Property(e => e.Destination).HasMaxLength(200);
            _ = entity.Property(e => e.SpecialRequirements).HasMaxLength(500);
            _ = entity.Property(e => e.TripNotes).HasMaxLength(1000);
            _ = entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Scheduled");
            _ = entity.Property(e => e.ApprovedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");

            // Relationships - TripEvents are now only related through ActivitySchedule
            _ = entity.HasOne(te => te.Vehicle)
                  .WithMany()  // No back-reference from Vehicle to TripEvents
                  .HasForeignKey(te => te.VehicleId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_TripEvents_Vehicle");

            _ = entity.HasOne(te => te.Driver)
                  .WithMany()  // No back-reference from Driver to TripEvents
                  .HasForeignKey(te => te.DriverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_TripEvents_Driver");

            _ = entity.HasOne(te => te.Route)
                  .WithMany()  // No back-reference from Route to TripEvents
                  .HasForeignKey(te => te.RouteId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_TripEvents_Route");

            // Indexes
            _ = entity.HasIndex(e => e.LeaveTime).HasDatabaseName("IX_TripEvents_LeaveTime");
            _ = entity.HasIndex(e => e.Type).HasDatabaseName("IX_TripEvents_Type");
            _ = entity.HasIndex(e => e.Status).HasDatabaseName("IX_TripEvents_Status");
            _ = entity.HasIndex(e => e.VehicleId).HasDatabaseName("IX_TripEvents_VehicleId");
            _ = entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_TripEvents_DriverId");
            _ = entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_TripEvents_RouteId");
            _ = entity.HasIndex(e => new { e.VehicleId, e.LeaveTime }).HasDatabaseName("IX_TripEvents_BusSchedule");
            _ = entity.HasIndex(e => new { e.DriverId, e.LeaveTime }).HasDatabaseName("IX_TripEvents_DriverSchedule");
            _ = entity.HasIndex(e => e.ApprovalRequired).HasDatabaseName("IX_TripEvents_ApprovalRequired");
        });

        // Configure RouteStop entity
        _ = modelBuilder.Entity<RouteStop>(entity =>
        {
            _ = entity.ToTable("RouteStops");
            _ = entity.HasKey(e => e.RouteStopId);

            // Properties
            _ = entity.Property(e => e.StopName).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.StopAddress).HasMaxLength(200);
            _ = entity.Property(e => e.Notes).HasMaxLength(500);

            // Relationships
            _ = entity.HasOne(rs => rs.Route)
                  .WithMany()
                  .HasForeignKey(rs => rs.RouteId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_RouteStops_Route");

            // Indexes
            _ = entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_RouteStops_RouteId");
            _ = entity.HasIndex(e => new { e.RouteId, e.StopOrder }).HasDatabaseName("IX_RouteStops_RouteOrder");
        });

        // Configure SchoolCalendar entity
        _ = modelBuilder.Entity<SchoolCalendar>(entity =>
        {
            _ = entity.ToTable("SchoolCalendar");
            _ = entity.HasKey(e => e.CalendarId);

            // Properties
            _ = entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.EventName).IsRequired().HasMaxLength(100);
            _ = entity.Property(e => e.SchoolYear).IsRequired().HasMaxLength(10);
            _ = entity.Property(e => e.Description).HasMaxLength(200);
            _ = entity.Property(e => e.Notes).HasMaxLength(500);

            // Indexes
            _ = entity.HasIndex(e => e.Date).HasDatabaseName("IX_SchoolCalendar_Date");
            _ = entity.HasIndex(e => e.EventType).HasDatabaseName("IX_SchoolCalendar_EventType");
            _ = entity.HasIndex(e => e.SchoolYear).HasDatabaseName("IX_SchoolCalendar_SchoolYear");
            _ = entity.HasIndex(e => e.RoutesRequired).HasDatabaseName("IX_SchoolCalendar_RoutesRequired");
        });

        // Configure ActivitySchedule entity
        _ = modelBuilder.Entity<ActivitySchedule>(entity =>
        {
            _ = entity.ToTable("ActivitySchedule");
            _ = entity.HasKey(e => e.ActivityScheduleId);

            // Properties
            _ = entity.Property(e => e.TripType).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.ScheduledDestination).IsRequired().HasMaxLength(200);
            _ = entity.Property(e => e.Notes).HasMaxLength(500);

            // Relationships
            _ = entity.HasOne(ash => ash.ScheduledVehicle)
                  .WithMany(v => v.ScheduledActivities)
                  .HasForeignKey(ash => ash.ScheduledVehicleId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_ActivitySchedule_Vehicle");

            _ = entity.HasOne(ash => ash.ScheduledDriver)
                  .WithMany(d => d.ScheduledActivities)
                  .HasForeignKey(ash => ash.ScheduledDriverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_ActivitySchedule_Driver");

            // Indexes
            _ = entity.HasIndex(e => e.ScheduledDate).HasDatabaseName("IX_ActivitySchedule_Date");
            _ = entity.HasIndex(e => e.TripType).HasDatabaseName("IX_ActivitySchedule_TripType");
            _ = entity.HasIndex(e => e.ScheduledVehicleId).HasDatabaseName("IX_ActivitySchedule_VehicleId");
            _ = entity.HasIndex(e => e.ScheduledDriverId).HasDatabaseName("IX_ActivitySchedule_DriverId");
        });

        // Configure AIInsight entity for Grok analysis results
        _ = modelBuilder.Entity<AIInsight>(entity =>
        {
            _ = entity.ToTable("AIInsights");
            _ = entity.HasKey(e => e.InsightId);

            // Properties
            _ = entity.Property(e => e.InsightType).IsRequired().HasMaxLength(50);
            _ = entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Medium");
            _ = entity.Property(e => e.EntityReference).HasMaxLength(100);
            _ = entity.Property(e => e.InsightDetails).HasColumnType("nvarchar(max)");
            _ = entity.Property(e => e.Summary).IsRequired().HasMaxLength(500);
            _ = entity.Property(e => e.RecommendedActions).HasMaxLength(1000);
            _ = entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(4,3)");
            _ = entity.Property(e => e.Source).HasMaxLength(50).HasDefaultValue("Grok-4");
            _ = entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("New");
            _ = entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            _ = entity.Property(e => e.CreatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            _ = entity.Property(e => e.EstimatedSavings).HasColumnType("decimal(10,2)");
            _ = entity.Property(e => e.Tags).HasMaxLength(500);

            // Relationships
            _ = entity.HasOne(ai => ai.Vehicle)
                  .WithMany()
                  .HasForeignKey(ai => ai.VehicleId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_AIInsights_Vehicle");

            _ = entity.HasOne(ai => ai.Route)
                  .WithMany()
                  .HasForeignKey(ai => ai.RouteId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_AIInsights_Route");

            _ = entity.HasOne(ai => ai.Driver)
                  .WithMany()
                  .HasForeignKey(ai => ai.DriverId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_AIInsights_Driver");

            // Indexes for performance and querying
            _ = entity.HasIndex(e => e.InsightType).HasDatabaseName("IX_AIInsights_Type");
            _ = entity.HasIndex(e => e.Priority).HasDatabaseName("IX_AIInsights_Priority");
            _ = entity.HasIndex(e => e.Status).HasDatabaseName("IX_AIInsights_Status");
            _ = entity.HasIndex(e => e.CreatedDate).HasDatabaseName("IX_AIInsights_CreatedDate");
            _ = entity.HasIndex(e => e.ConfidenceScore).HasDatabaseName("IX_AIInsights_ConfidenceScore");
            _ = entity.HasIndex(e => new { e.InsightType, e.Status }).HasDatabaseName("IX_AIInsights_TypeStatus");
            _ = entity.HasIndex(e => new { e.VehicleId, e.InsightType }).HasDatabaseName("IX_AIInsights_VehicleType");
            _ = entity.HasIndex(e => new { e.RouteId, e.InsightType }).HasDatabaseName("IX_AIInsights_RouteType");
            _ = entity.HasIndex(e => new { e.DriverId, e.InsightType }).HasDatabaseName("IX_AIInsights_DriverType");
            _ = entity.HasIndex(e => e.ExpiryDate).HasDatabaseName("IX_AIInsights_ExpiryDate");
        });

        // REMOVED: Ticket entity configuration - deprecated module

        // Conditionally seed initial data
        // Always skip global seed data if using in-memory provider (for test isolation)
        var isInMemory = this.Database.ProviderName != null && this.Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        if (!isInMemory && !SkipGlobalSeedData)
        {
            SeedData(modelBuilder);
        }
        // If SkipGlobalSeedData is true or using in-memory provider, do NOT call SeedData; ensures no global seed data for in-memory tests
    }

    /// <summary>
    /// Configure global query filters for soft deletes
    /// </summary>
    private static void ConfigureGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // TODO: Re-implement soft delete filter when entities inherit from BaseEntity
        // Apply soft delete filter to all entities that inherit from BaseEntity
        /*
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType);
                var propertyMethodInfo = typeof(EF).GetMethod("Property")?.MakeGenericMethod(typeof(bool));
                var isDeletedProperty = Expression.Call(propertyMethodInfo!, parameter, Expression.Constant("IsDeleted"));
                var compareExpression = Expression.MakeBinary(ExpressionType.Equal, isDeletedProperty, Expression.Constant(false));
                var lambda = Expression.Lambda(compareExpression, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
        */
    }

    private static void ConfigureNullHandling(ModelBuilder modelBuilder)
    {
        // Configure specific entities with NULL-safe defaults to prevent SqlNullValueException
        _ = modelBuilder.Entity<Driver>(entity =>
        {
            _ = entity.Property(e => e.DriverName)
                .HasDefaultValue("Unknown Driver");

            _ = entity.Property(e => e.Status)
                .HasDefaultValue("Active");

            _ = entity.Property(e => e.DriversLicenceType)
                .HasDefaultValue("Standard");
        });

        _ = modelBuilder.Entity<Route>(entity =>
        {
            _ = entity.Property(e => e.RouteName)
                .HasDefaultValue("Route");
        });

        _ = modelBuilder.Entity<Bus>(entity =>
        {
            _ = entity.Property(e => e.Make)
                .HasDefaultValue("Unknown");

            _ = entity.Property(e => e.Model)
                .HasDefaultValue("Unknown");

            _ = entity.Property(e => e.Status)
                .HasDefaultValue("Active");
        });

        _ = modelBuilder.Entity<Activity>(entity =>
        {
            _ = entity.Property(e => e.Description)
                .HasDefaultValue("Activity");
        });

        // Add NULL handling for Schedule entity to prevent InvalidCastException
        _ = modelBuilder.Entity<Schedule>(entity =>
        {
            _ = entity.Property(e => e.Status)
                .HasDefaultValue("Scheduled");

            _ = entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure all string properties to handle NULL values gracefully
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string))
                {
                    // Convert NULL values to empty string for required string properties
                    if (!property.IsNullable)
                    {
                        property.SetDefaultValue("");
                    }
                }

                // Handle DateTime properties that might be NULL
                if (property.ClrType == typeof(DateTime) && property.IsNullable)
                {
                    // Add a value converter to handle invalid dates
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                        v => v.HasValue && v.Value != DateTime.MinValue ? v : null,
                        v => v ?? DateTime.MinValue));
                }
            }
        }
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Use static dates to avoid migration conflicts
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed sample buses
        _ = modelBuilder.Entity<Bus>().HasData(
            new Bus
            {
                BusId = 1,
                BusNumber = "001",
                Year = 2020,
                Make = "Blue Bird",
                Model = "Vision",
                SeatingCapacity = 72,
                VINNumber = "1BAANKCL7LF123456",
                LicenseNumber = "TX123456",
                Status = "Active",
                FuelCapacity = 60m, // Added post-refactor static seed for fuel analytics
                MilesPerGallon = 8.5m, // Added post-refactor static seed for efficiency metrics
                PurchaseDate = new DateTime(2020, 8, 15),
                PurchasePrice = 85000.00m,
                CreatedDate = seedDate
            },
            new Bus
            {
                BusId = 2,
                BusNumber = "002",
                Year = 2019,
                Make = "Thomas Built",
                Model = "Saf-T-Liner C2",
                SeatingCapacity = 66,
                VINNumber = "4DRBTAAN7KB654321",
                LicenseNumber = "TX654321",
                Status = "Active",
                FuelCapacity = 55m,
                MilesPerGallon = 8.2m,
                PurchaseDate = new DateTime(2019, 7, 10),
                PurchasePrice = 82000.00m,
                CreatedDate = seedDate
            }
        );

        // Seed sample drivers
        _ = modelBuilder.Entity<Driver>().HasData(
            new Driver
            {
                DriverId = 1,
                DriverName = "John Smith",
                DriverPhone = "555-012-3456",
                DriverEmail = "john.smith@school.edu",
                DriversLicenceType = "CDL",
                TrainingComplete = true,
                CreatedDate = seedDate
            },
            new Driver
            {
                DriverId = 2,
                DriverName = "Mary Johnson",
                DriverPhone = "555-045-6789",
                DriverEmail = "mary.johnson@school.edu",
                DriversLicenceType = "CDL",
                TrainingComplete = true,
                CreatedDate = seedDate
            }
        );
    }

    /// <summary>
    /// Override SaveChanges to apply audit fields with concurrency protection
    /// </summary>
    public override int SaveChanges()
    {
        try
        {
            ApplyAuditFields();
            return base.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Log the concurrency exception
            HandleConcurrencyException(ex);
            throw; // Re-throw after logging
        }
    }

    /// <summary>
    /// Override SaveChangesAsync to apply audit fields with concurrency protection
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Log the concurrency exception
            HandleConcurrencyException(ex);
            throw; // Re-throw after logging
        }
    }

    /// <summary>
    /// Handle database concurrency exceptions with detailed logging
    /// </summary>
    private static void HandleConcurrencyException(DbUpdateConcurrencyException ex)
    {
        // Log the detailed concurrency information to help with debugging
        var failedEntries = ex.Entries.ToList();
        foreach (var entry in failedEntries)
        {
            var proposedValues = entry.CurrentValues;
            var databaseValues = entry.GetDatabaseValues();

            var propNames = proposedValues.Properties.Select(p => p.Name).ToList();
            var conflictDetails = new System.Text.StringBuilder();
            _ = conflictDetails.AppendLine($"Concurrency conflict for entity: {entry.Entity.GetType().Name}");

            foreach (var propName in propNames)
            {
                var proposedValue = proposedValues[propName]?.ToString() ?? "null";
                var databaseValue = databaseValues?[propName]?.ToString() ?? "null";

                if (proposedValue != databaseValue)
                {
                    _ = conflictDetails.AppendLine($"Property: {propName}, Proposed: {proposedValue}, Database: {databaseValue}");
                }
            }

            // Output to debug - in a production app, use proper logging
            System.Diagnostics.Debug.WriteLine(conflictDetails.ToString());
        }
    }

    /// <summary>
    /// Apply audit fields to entities before saving
    /// TODO: Re-implement when entities inherit from BaseEntity
    /// </summary>
    private static void ApplyAuditFields()
    {
        // TODO: Re-implement audit fields when entities inherit from BaseEntity
        /*
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entity in entities)
        {
            var now = DateTime.UtcNow;

            if (entity.State == EntityState.Added)
            {
                entity.Entity.CreatedDate = now;
                entity.Entity.CreatedBy = _currentAuditUser;
            }

            if (entity.State == EntityState.Modified)
            {
                entity.Entity.UpdatedDate = now;
                entity.Entity.UpdatedBy = _currentAuditUser;
                // Prevent modification of CreatedDate and CreatedBy
                entity.Property(x => x.CreatedDate).IsModified = false;
                entity.Property(x => x.CreatedBy).IsModified = false;
            }

            // Call entity-specific OnSaving method
            entity.Entity.OnSaving();
        }
        */
    }

    private static void UseLocalDbFallback(DbContextOptionsBuilder optionsBuilder, ILogger logger)
    {
        const string fallback = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";
        logger.Information("Using LocalDB fallback connection");
        _ = optionsBuilder.UseSqlServer(fallback, sql =>
        {
            _ = sql.CommandTimeout(60);
            _ = sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), new[] { 40613, 40501, 40197, 10928, 10929, 10060, 10054, 10053 });
        });
    }

    // Centralized EF Core logging configuration (same logic as earlier implementation)
    private static void ConfigureEfLogging(DbContextOptionsBuilder optionsBuilder)
    {
        _ = optionsBuilder.LogTo(message =>
        {
            using (LogContext.PushProperty("DatabaseContext", "BusBuddyDbContext"))
            using (LogContext.PushProperty("SourceContext", "EntityFramework"))
            {
                var innerLogger = Log.ForContext("SourceContext", "BusBuddyDbContext");
                if (message.Contains("warn", StringComparison.OrdinalIgnoreCase) || message.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    innerLogger.Warning("EF Core: {Message}", message);
                }
                else if (message.Contains("error", StringComparison.OrdinalIgnoreCase) || message.Contains("exception", StringComparison.OrdinalIgnoreCase))
                {
                    innerLogger.Error("EF Core: {Message}", message);
                }
                else
                {
                    innerLogger.Information("EF Core: {Message}", message);
                }
            }
        });

        if (System.Diagnostics.Debugger.IsAttached)
        {
            _ = optionsBuilder.EnableSensitiveDataLogging();
            _ = optionsBuilder.EnableDetailedErrors();
        }
    }
}
}
