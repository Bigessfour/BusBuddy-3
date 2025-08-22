# 🗄️ Database Schema - Entity Framework Models & Migrations

**Part of BusBuddy Copilot Reference Hub**  
**Last Updated**: August 3, 2025  
**Purpose**: Provide GitHub Copilot with Entity Framework schema patterns for BusBuddy database design

---

## 🏗️ **Core Entity Models**

### **Student Entity**

```csharp
// BusBuddy.Core/Models/Student.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Students")]
public class Student
{
    [Key]
    public int StudentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Range(0, 12)]
    public int Grade { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(10)]
    public string ZipCode { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? EmergencyContact { get; set; }

    [MaxLength(100)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(50)]
    public string? MedicalNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation Properties
    [ForeignKey("Route")]
    public int? RouteId { get; set; }
    public virtual Route? Route { get; set; }

    public virtual ICollection<StudentNote> Notes { get; set; } = new List<StudentNote>();
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    // Computed Properties
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [NotMapped]
    public int Age => DateTime.Today.Year - DateOfBirth.Year -
                     (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);

    [NotMapped]
    public string GradeDisplay => Grade switch
    {
        0 => "Pre-K",
        13 => "Kindergarten",
        _ => $"Grade {Grade}"
    };
}
```

### **Route Entity**

```csharp
// BusBuddy.Core/Models/Route.cs
[Table("Routes")]
public class Route
{
    [Key]
    public int RouteId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RouteName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal EstimatedDistance { get; set; } // in miles

    public int EstimatedDuration { get; set; } // in minutes

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation Properties
    [ForeignKey("Bus")]
    public int BusId { get; set; }
    [Required]
    public virtual Bus Bus { get; set; } = null!;

    [ForeignKey("Driver")]
    public int DriverId { get; set; }
    [Required]
    public virtual Driver Driver { get; set; } = null!;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
    public virtual ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
    public virtual ICollection<RouteSchedule> Schedules { get; set; } = new List<RouteSchedule>();

    // Computed Properties
    [NotMapped]
    public int StudentCount => Students?.Count ?? 0;

    [NotMapped]
    public bool IsAtCapacity => Students?.Count >= Bus?.Capacity;

    [NotMapped]
    public double UtilizationRate => Bus?.Capacity > 0 ? (double)(Students?.Count ?? 0) / Bus.Capacity : 0;

    [NotMapped]
    public string TimeRange => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
}
```

### **Bus Entity**

```csharp
// BusBuddy.Core/Models/Bus.cs
[Table("Buses")]
public class Bus
{
    [Key]
    public int BusId { get; set; }

    [Required]
    [MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Make { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    [Required]
    [Range(1990, 2050)]
    public int Year { get; set; }

    [Required]
    [Range(1, 100)]
    public int Capacity { get; set; }

    [Range(0, int.MaxValue)]
    public int Mileage { get; set; }

    [MaxLength(17)]
    public string? VinNumber { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PurchasePrice { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }

    public BusStatus Status { get; set; } = BusStatus.Active;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation Properties
    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();
    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public virtual ICollection<FuelRecord> FuelRecords { get; set; } = new List<FuelRecord>();

    // Computed Properties
    [NotMapped]
    public string DisplayName => $"{LicensePlate} ({Make} {Model})";

    [NotMapped]
    public int Age => DateTime.Now.Year - Year;

    [NotMapped]
    public bool InspectionDue => NextInspectionDate.HasValue && NextInspectionDate.Value <= DateTime.Today.AddDays(30);

    [NotMapped]
    public decimal AverageMpg => FuelRecords?.Any() == true ?
        (decimal)FuelRecords.Average(f => f.MilesPerGallon) : 0;
}

public enum BusStatus
{
    Active,
    Maintenance,
    OutOfService,
    Retired
}
```

### **Driver Entity**

```csharp
// BusBuddy.Core/Models/Driver.cs
[Table("Drivers")]
public class Driver
{
    [Key]
    public int DriverId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Required]
    public DateTime LicenseExpiry { get; set; }

    [Phone]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    public DateTime? HireDate { get; set; }
    public DateTime? LastPhysicalDate { get; set; }
    public DateTime? NextPhysicalDate { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? HourlyRate { get; set; }

    public DriverStatus Status { get; set; } = DriverStatus.Active;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation Properties
    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();
    public virtual ICollection<DriverNote> Notes { get; set; } = new List<DriverNote>();

    // Computed Properties
    [NotMapped]
    public bool LicenseExpiringSoon => LicenseExpiry <= DateTime.Today.AddDays(90);

    [NotMapped]
    public bool PhysicalDue => NextPhysicalDate.HasValue && NextPhysicalDate.Value <= DateTime.Today.AddDays(30);

    [NotMapped]
    public int YearsOfService => HireDate.HasValue ? DateTime.Today.Year - HireDate.Value.Year : 0;
}

public enum DriverStatus
{
    Active,
    OnLeave,
    Suspended,
    Inactive
}
```

### **Supporting Entities**

```csharp
// BusBuddy.Core/Models/RouteStop.cs
[Table("RouteStops")]
public class RouteStop
{
    [Key]
    public int RouteStopId { get; set; }

    [ForeignKey("Route")]
    public int RouteId { get; set; }
    public virtual Route Route { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? StopName { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Longitude { get; set; }

    [Required]
    public int StopOrder { get; set; }

    [Required]
    public TimeSpan ScheduledTime { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}

// BusBuddy.Core/Models/StudentNote.cs
[Table("StudentNotes")]
public class StudentNote
{
    [Key]
    public int StudentNoteId { get; set; }

    [ForeignKey("Student")]
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Note { get; set; } = string.Empty;

    public NoteType NoteType { get; set; } = NoteType.General;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}

public enum NoteType
{
    General,
    Medical,
    Behavioral,
    Transportation,
    Emergency
}

// BusBuddy.Core/Models/MaintenanceRecord.cs
[Table("MaintenanceRecords")]
public class MaintenanceRecord
{
    [Key]
    public int MaintenanceRecordId { get; set; }

    [ForeignKey("Bus")]
    public int BusId { get; set; }
    public virtual Bus Bus { get; set; } = null!;

    [Required]
    public DateTime ServiceDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string ServiceType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal Cost { get; set; }

    [MaxLength(100)]
    public string? ServiceProvider { get; set; }

    public int MileageAtService { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

// BusBuddy.Core/Models/FuelRecord.cs
[Table("FuelRecords")]
public class FuelRecord
{
    [Key]
    public int FuelRecordId { get; set; }

    [ForeignKey("Bus")]
    public int BusId { get; set; }
    public virtual Bus Bus { get; set; } = null!;

    [Required]
    public DateTime FuelDate { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal Gallons { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal PricePerGallon { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal TotalCost { get; set; }

    public int Odometer { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal MilesPerGallon { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
```

---

## 🗄️ **DbContext Configuration**

### **BusBuddyContext Implementation**

```csharp
// BusBuddy.Core/BusBuddyDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class BusBuddyContext : DbContext
{
    private readonly IConfiguration? _configuration;

    public BusBuddyContext(DbContextOptions<BusBuddyContext> options) : base(options)
    {
    }

    public BusBuddyContext(DbContextOptions<BusBuddyContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    // DbSet Properties
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<Route> Routes { get; set; } = null!;
    public DbSet<Bus> Buses { get; set; } = null!;
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<RouteStop> RouteStops { get; set; } = null!;
    public DbSet<StudentNote> StudentNotes { get; set; } = null!;
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; } = null!;
    public DbSet<FuelRecord> FuelRecords { get; set; } = null!;
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; } = null!;
    public DbSet<RouteSchedule> RouteSchedules { get; set; } = null!;
    public DbSet<DriverNote> DriverNotes { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && _configuration != null)
        {
            var provider = _configuration["DatabaseProvider"] ?? "LocalDB";
            var connectionString = provider switch
            {
                "Azure" => _configuration.GetConnectionString("AzureConnection"),
                "LocalDB" => _configuration.GetConnectionString("DefaultConnection"),
                _ => _configuration.GetConnectionString("LocalConnection")
            };

            if (!string.IsNullOrEmpty(connectionString))
            {
                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.CommandTimeout(30);
                    options.EnableRetryOnFailure(3);
                });
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entity relationships and constraints
        ConfigureStudentEntity(modelBuilder);
        ConfigureRouteEntity(modelBuilder);
        ConfigureBusEntity(modelBuilder);
        ConfigureDriverEntity(modelBuilder);
        ConfigureRouteStopEntity(modelBuilder);

        // Seed initial data
        SeedInitialData(modelBuilder);
    }

    private void ConfigureStudentEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            // Indexes for performance
            entity.HasIndex(e => e.LastName)
                  .HasDatabaseName("IX_Students_LastName");

            entity.HasIndex(e => e.Grade)
                  .HasDatabaseName("IX_Students_Grade");

            entity.HasIndex(e => e.RouteId)
                  .HasDatabaseName("IX_Students_RouteId");

            entity.HasIndex(e => new { e.FirstName, e.LastName, e.DateOfBirth })
                  .IsUnique()
                  .HasDatabaseName("UK_Students_Name_DOB");

            // Configure relationships
            entity.HasOne(s => s.Route)
                  .WithMany(r => r.Students)
                  .HasForeignKey(s => s.RouteId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(s => s.Notes)
                  .WithOne(n => n.Student)
                  .HasForeignKey(n => n.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Default values
            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private void ConfigureRouteEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Route>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.RouteName)
                  .IsUnique()
                  .HasDatabaseName("UK_Routes_RouteName");

            entity.HasIndex(e => e.BusId)
                  .HasDatabaseName("IX_Routes_BusId");

            entity.HasIndex(e => e.DriverId)
                  .HasDatabaseName("IX_Routes_DriverId");

            // Configure relationships
            entity.HasOne(r => r.Bus)
                  .WithMany(b => b.Routes)
                  .HasForeignKey(r => r.BusId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Driver)
                  .WithMany(d => d.Routes)
                  .HasForeignKey(r => r.DriverId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Stops)
                  .WithOne(s => s.Route)
                  .HasForeignKey(s => s.RouteId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Default values
            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private void ConfigureBusEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bus>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.LicensePlate)
                  .IsUnique()
                  .HasDatabaseName("UK_Buses_LicensePlate");

            entity.HasIndex(e => e.VinNumber)
                  .IsUnique()
                  .HasDatabaseName("UK_Buses_VinNumber");

            // Configure relationships
            entity.HasMany(b => b.MaintenanceRecords)
                  .WithOne(m => m.Bus)
                  .HasForeignKey(m => m.BusId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(b => b.FuelRecords)
                  .WithOne(f => f.Bus)
                  .HasForeignKey(f => f.BusId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Default values
            entity.Property(e => e.Status)
                  .HasDefaultValue(BusStatus.Active);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private void ConfigureDriverEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Driver>(entity =>
        {
            // Indexes
            entity.HasIndex(e => e.LicenseNumber)
                  .IsUnique()
                  .HasDatabaseName("UK_Drivers_LicenseNumber");

            entity.HasIndex(e => e.FullName)
                  .HasDatabaseName("IX_Drivers_FullName");

            // Configure relationships
            entity.HasMany(d => d.Notes)
                  .WithOne(n => n.Driver)
                  .HasForeignKey(n => n.DriverId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Default values
            entity.Property(e => e.Status)
                  .HasDefaultValue(DriverStatus.Active);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedDate)
                  .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private void ConfigureRouteStopEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteStop>(entity =>
        {
            // Indexes
            entity.HasIndex(e => new { e.RouteId, e.StopOrder })
                  .IsUnique()
                  .HasDatabaseName("UK_RouteStops_Route_Order");

            // Default values
            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);
        });
    }

    private void SeedInitialData(ModelBuilder modelBuilder)
    {
        // Seed sample buses
        modelBuilder.Entity<Bus>().HasData(
            new Bus
            {
                BusId = 1,
                LicensePlate = "BUS001",
                Make = "Blue Bird",
                Model = "Vision",
                Year = 2020,
                Capacity = 48,
                Mileage = 25000,
                Status = BusStatus.Active,
                CreatedDate = DateTime.UtcNow
            },
            new Bus
            {
                BusId = 2,
                LicensePlate = "BUS002",
                Make = "Thomas Built",
                Model = "Saf-T-Liner C2",
                Year = 2019,
                Capacity = 54,
                Mileage = 32000,
                Status = BusStatus.Active,
                CreatedDate = DateTime.UtcNow
            }
        );

        // Seed sample drivers
        modelBuilder.Entity<Driver>().HasData(
            new Driver
            {
                DriverId = 1,
                FullName = "John Smith",
                LicenseNumber = "DL123456789",
                LicenseExpiry = DateTime.Today.AddYears(2),
                Phone = "555-0101",
                Email = "john.smith@busbuddy.com",
                HireDate = DateTime.Today.AddYears(-3),
                Status = DriverStatus.Active,
                CreatedDate = DateTime.UtcNow
            },
            new Driver
            {
                DriverId = 2,
                FullName = "Sarah Johnson",
                LicenseNumber = "DL987654321",
                LicenseExpiry = DateTime.Today.AddYears(1),
                Phone = "555-0102",
                Email = "sarah.johnson@busbuddy.com",
                HireDate = DateTime.Today.AddYears(-2),
                Status = DriverStatus.Active,
                CreatedDate = DateTime.UtcNow
            }
        );

        // Seed sample routes
        modelBuilder.Entity<Route>().HasData(
            new Route
            {
                RouteId = 1,
                RouteName = "Elementary East",
                Description = "Eastern elementary schools route",
                StartTime = new TimeSpan(7, 30, 0),
                EndTime = new TimeSpan(8, 30, 0),
                EstimatedDistance = 15.5m,
                EstimatedDuration = 45,
                BusId = 1,
                DriverId = 1,
                CreatedDate = DateTime.UtcNow
            },
            new Route
            {
                RouteId = 2,
                RouteName = "Middle School Central",
                Description = "Central middle school route",
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(9, 0, 0),
                EstimatedDistance = 12.3m,
                EstimatedDuration = 40,
                BusId = 2,
                DriverId = 2,
                CreatedDate = DateTime.UtcNow
            }
        );
    }

    // Override SaveChanges to automatically set ModifiedDate
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            if (entry.Entity is Student student)
                student.ModifiedDate = DateTime.UtcNow;
            else if (entry.Entity is Route route)
                route.ModifiedDate = DateTime.UtcNow;
            else if (entry.Entity is Bus bus)
                bus.ModifiedDate = DateTime.UtcNow;
            else if (entry.Entity is Driver driver)
                driver.ModifiedDate = DateTime.UtcNow;
        }
    }
}

// Base entity interface for common properties
public interface BaseEntity
{
    DateTime CreatedDate { get; set; }
    DateTime? ModifiedDate { get; set; }
    bool IsActive { get; set; }
}
```

---

## 🔄 **Migration Patterns**

### **Initial Migration Creation**

```powershell
# Create initial migration
dotnet ef migrations add InitialCreate --project BusBuddy.Core --startup-project BusBuddy.WPF

# Update database
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF

# Generate SQL script
dotnet ef migrations script --project BusBuddy.Core --startup-project BusBuddy.WPF --output InitialCreate.sql
```

### **Sample Migration File**

```csharp
// BusBuddy.Core/Migrations/20250803000001_InitialCreate.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buses",
                columns: table => new
                {
                    BusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicensePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Make = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Mileage = table.Column<int>(type: "int", nullable: false),
                    VinNumber = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextInspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buses", x => x.BusId);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LicenseExpiry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPhysicalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextPhysicalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverId);
                });

            // Additional table creation code...

            migrationBuilder.CreateIndex(
                name: "UK_Buses_LicensePlate",
                table: "Buses",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_Drivers_LicenseNumber",
                table: "Drivers",
                column: "LicenseNumber",
                unique: true);

            // Seed data
            migrationBuilder.InsertData(
                table: "Buses",
                columns: new[] { "BusId", "LicensePlate", "Make", "Model", "Year", "Capacity", "Mileage", "CreatedDate" },
                values: new object[,]
                {
                    { 1, "BUS001", "Blue Bird", "Vision", 2020, 48, 25000, new DateTime(2025, 8, 3, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "BUS002", "Thomas Built", "Saf-T-Liner C2", 2019, 54, 32000, new DateTime(2025, 8, 3, 12, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Students");
            migrationBuilder.DropTable(name: "RouteStops");
            migrationBuilder.DropTable(name: "Routes");
            migrationBuilder.DropTable(name: "Buses");
            migrationBuilder.DropTable(name: "Drivers");
            // Additional cleanup...
        }
    }
}
```

---

## 🎯 **Database Service Patterns**

### **Generic Repository Pattern**

```csharp
// BusBuddy.Core/Data/IRepository.cs
public interface IRepository<T> where T : class
{
    Task<List<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    IQueryable<T> Query();
}

// BusBuddy.Core/Data/Repository.cs
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly BusBuddyContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger<Repository<T>> _logger;

    public Repository(BusBuddyContext context, ILogger<Repository<T>> logger)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
    }

    public virtual async Task<List<T>> GetAllAsync()
    {
        try
        {
            return await _dbSet.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        try
        {
            return await _dbSet.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        try
        {
            var entry = await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entry.Entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        try
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public virtual async Task DeleteAsync(int id)
    {
        try
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting {EntityType} with ID {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual async Task<bool> ExistsAsync(int id)
    {
        try
        {
            return await _dbSet.FindAsync(id) != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of {EntityType} with ID {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }
}
```

### **Specialized Student Repository**

```csharp
// BusBuddy.Core/Data/IStudentRepository.cs
public interface IStudentRepository : IRepository<Student>
{
    Task<List<Student>> GetStudentsByGradeAsync(int grade);
    Task<List<Student>> GetStudentsByRouteAsync(int routeId);
    Task<List<Student>> GetUnassignedStudentsAsync();
    Task<List<Student>> SearchStudentsAsync(string searchTerm);
    Task<Student?> GetStudentWithNotesAsync(int studentId);
    Task<bool> StudentExistsAsync(string firstName, string lastName, DateTime dateOfBirth);
}

// BusBuddy.Core/Data/StudentRepository.cs
public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(BusBuddyContext context, ILogger<StudentRepository> logger)
        : base(context, logger)
    {
    }

    public override async Task<List<Student>> GetAllAsync()
    {
        return await _dbSet
            .Include(s => s.Route)
            .Include(s => s.Notes)
            .Where(s => s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public override async Task<Student?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(s => s.Route)
            .Include(s => s.Notes)
            .FirstOrDefaultAsync(s => s.StudentId == id && s.IsActive);
    }

    public async Task<List<Student>> GetStudentsByGradeAsync(int grade)
    {
        return await _dbSet
            .Include(s => s.Route)
            .Where(s => s.Grade == grade && s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<List<Student>> GetStudentsByRouteAsync(int routeId)
    {
        return await _dbSet
            .Include(s => s.Route)
            .Where(s => s.RouteId == routeId && s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<List<Student>> GetUnassignedStudentsAsync()
    {
        return await _dbSet
            .Where(s => s.RouteId == null && s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<List<Student>> SearchStudentsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync();

        var lowerSearchTerm = searchTerm.ToLower();

        return await _dbSet
            .Include(s => s.Route)
            .Where(s => s.IsActive && (
                s.FirstName.ToLower().Contains(lowerSearchTerm) ||
                s.LastName.ToLower().Contains(lowerSearchTerm) ||
                s.Address.ToLower().Contains(lowerSearchTerm) ||
                s.StudentId.ToString().Contains(searchTerm)))
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<Student?> GetStudentWithNotesAsync(int studentId)
    {
        return await _dbSet
            .Include(s => s.Route)
            .Include(s => s.Notes)
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.IsActive);
    }

    public async Task<bool> StudentExistsAsync(string firstName, string lastName, DateTime dateOfBirth)
    {
        return await _dbSet
            .AnyAsync(s => s.FirstName.ToLower() == firstName.ToLower() &&
                          s.LastName.ToLower() == lastName.ToLower() &&
                          s.DateOfBirth.Date == dateOfBirth.Date &&
                          s.IsActive);
    }
}
```

---

## 🧪 **Testing Database Patterns**

### **In-Memory Database Testing**

```csharp
// BusBuddy.Tests/Core/DatabaseTests.cs
[TestFixture]
[Category("Integration")]
public class DatabaseTests
{
    private BusBuddyContext _context;
    private DbContextOptions<BusBuddyContext> _options;

    [SetUp]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<BusBuddyContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BusBuddyContext(_options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Student_CanBeAddedAndRetrieved()
    {
        // Arrange
        var student = new Student
        {
            FirstName = "John",
            LastName = "Doe",
            Grade = 5,
            DateOfBirth = DateTime.Today.AddYears(-10),
            Address = "123 Main St"
        };

        // Act
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var retrievedStudent = await _context.Students
            .FirstOrDefaultAsync(s => s.FirstName == "John");

        // Assert
        Assert.That(retrievedStudent, Is.Not.Null);
        Assert.That(retrievedStudent.FullName, Is.EqualTo("John Doe"));
        Assert.That(retrievedStudent.Age, Is.EqualTo(10));
    }

    [Test]
    public async Task Route_WithStudents_MaintainsRelationship()
    {
        // Arrange
        var bus = new Bus { LicensePlate = "TEST123", Make = "Test", Model = "Bus", Year = 2020, Capacity = 50 };
        var driver = new Driver { FullName = "Test Driver", LicenseNumber = "DL123", LicenseExpiry = DateTime.Today.AddYears(1), Phone = "555-1234" };

        _context.Buses.Add(bus);
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();

        var route = new Route
        {
            RouteName = "Test Route",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(9),
            BusId = bus.BusId,
            DriverId = driver.DriverId
        };

        var student = new Student
        {
            FirstName = "Jane",
            LastName = "Smith",
            Grade = 3,
            DateOfBirth = DateTime.Today.AddYears(-8),
            Route = route
        };

        // Act
        _context.Routes.Add(route);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        var retrievedRoute = await _context.Routes
            .Include(r => r.Students)
            .Include(r => r.Bus)
            .Include(r => r.Driver)
            .FirstOrDefaultAsync();

        // Assert
        Assert.That(retrievedRoute, Is.Not.Null);
        Assert.That(retrievedRoute.Students.Count, Is.EqualTo(1));
        Assert.That(retrievedRoute.StudentCount, Is.EqualTo(1));
        Assert.That(retrievedRoute.UtilizationRate, Is.EqualTo(0.02)); // 1/50
    }
}
```

---

## 📋 **Quick Reference**

### **Key Entity Framework Features**

- **Code First Migrations**: Database schema managed through code
- **Fluent API Configuration**: Advanced entity relationships and constraints
- **Navigation Properties**: Automatic loading of related entities
- **Indexes and Constraints**: Performance optimization and data integrity
- **Seed Data**: Initial data population for development and testing

### **Migration Commands**

```powershell
# Create new migration
dotnet ef migrations add [MigrationName] --project BusBuddy.Core --startup-project BusBuddy.WPF

# Update database
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF

# Generate SQL script
dotnet ef migrations script --project BusBuddy.Core --startup-project BusBuddy.WPF

# Remove last migration
dotnet ef migrations remove --project BusBuddy.Core --startup-project BusBuddy.WPF

# Load database schema reference
bb-copilot-ref Database-Schema
```

### **Connection String Patterns**

- **LocalDB**: `Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True`
- **Azure SQL**: `Server=tcp:[server].database.windows.net,1433;Initial Catalog=[database];User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD}`
- **SQL Server**: `Server=[server];Database=BusBuddy;Trusted_Connection=true`

---

**📋 Note**: This reference provides GitHub Copilot with comprehensive Entity Framework patterns and database schema design for BusBuddy. Use `bb-copilot-ref Database-Schema` to load these patterns before implementing database features.
