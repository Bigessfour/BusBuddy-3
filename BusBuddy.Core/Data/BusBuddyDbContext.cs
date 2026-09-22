using BusBuddy.Core.Data.Configurations;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

namespace BusBuddy.Core.Data;

/// <summary>
/// Clerk persist context. Entity mapping lives under <c>Data/Configurations</c> by aggregate.
/// Connection selection lives on <see cref="BusBuddyDbContextFactory"/> (and the parameterless-ctor fallback).
/// </summary>
public class BusBuddyDbContext : DbContext
{
    public static void SeedTestData(BusBuddyDbContext context, Action<BusBuddyDbContext> seedAction)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(seedAction);
        seedAction(context);
        context.SaveChanges();
    }

    /// <summary>Skip HasData rows (in-memory tests).</summary>
    public static bool SkipGlobalSeedData { get; set; }

    private string _currentAuditUser = "System";

    public BusBuddyDbContext() { }

    public BusBuddyDbContext(DbContextOptions<BusBuddyDbContext> options) : base(options)
    {
        ChangeTracker.LazyLoadingEnabled = false;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public void SetAuditUser(string userName) => _currentAuditUser = userName ?? "System";

    public string GetCurrentAuditUser() => _currentAuditUser;

    public virtual DbSet<Bus> Buses { get; set; } = null!;
    public virtual DbSet<ActivityLog> ActivityLogs { get; set; } = null!;
    public virtual DbSet<Driver> Drivers { get; set; } = null!;
    public virtual DbSet<Route> Routes { get; set; } = null!;
    public virtual DbSet<Fuel> FuelRecords { get; set; } = null!;
    public virtual DbSet<Maintenance> MaintenanceRecords { get; set; } = null!;
    public virtual DbSet<Student> Students { get; set; } = null!;
    public virtual DbSet<StudentDeletionLog> StudentDeletionLogs { get; set; } = null!;
    public virtual DbSet<Family> Families { get; set; } = null!;
    public virtual DbSet<Guardian> Guardians { get; set; } = null!;
    public virtual DbSet<Schedule> Schedules { get; set; } = null!;
    public virtual DbSet<StudentSchedule> StudentSchedules { get; set; } = null!;
    public virtual DbSet<TripEvent> TripEvents { get; set; } = null!;
    public virtual DbSet<RouteStop> RouteStops { get; set; } = null!;
    public virtual DbSet<RouteRiderException> RouteRiderExceptions { get; set; } = null!;
    public virtual DbSet<SchoolCalendar> SchoolCalendar { get; set; } = null!;
    public virtual DbSet<Destination> Destinations { get; set; } = null!;
    public virtual DbSet<PickupStop> PickupStops { get; set; } = null!;
    public virtual DbSet<StudentSchoolTransfer> StudentSchoolTransfers { get; set; } = null!;
    public virtual DbSet<DriverTrainingRecord> DriverTrainingRecords { get; set; } = null!;
    public virtual DbSet<RouteAssignment> RouteAssignments { get; set; } = null!;
    public virtual DbSet<AIInsight> AIInsights { get; set; } = null!;

    public virtual DbSet<Fuel> Fuels => FuelRecords;
    public virtual DbSet<Maintenance> Maintenances => MaintenanceRecords;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));
        if (!optionsBuilder.IsConfigured)
        {
            UnconfiguredDbContextOptions.Apply(optionsBuilder);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        UtcDateTimeConvention.Apply(modelBuilder);
        EntityConfigurations.Apply(modelBuilder, Database.ProviderName);

        var isInMemory = Database.ProviderName != null &&
                         Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        if (!isInMemory && !SkipGlobalSeedData)
        {
            ModelHasData.Apply(modelBuilder);
        }
    }

    public override int SaveChanges()
    {
        try
        {
            ApplyStudentPersistenceNormalization();
            return base.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            HandleConcurrencyException(ex);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyStudentPersistenceNormalization();
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            HandleConcurrencyException(ex);
            throw;
        }
    }

    private static void HandleConcurrencyException(DbUpdateConcurrencyException ex)
    {
        foreach (var entry in ex.Entries)
        {
            var proposedValues = entry.CurrentValues;
            var databaseValues = entry.GetDatabaseValues();
            var conflictDetails = new System.Text.StringBuilder();
            conflictDetails.AppendLine($"Concurrency conflict for entity: {entry.Entity.GetType().Name}");
            foreach (var propName in proposedValues.Properties.Select(p => p.Name))
            {
                var proposedValue = proposedValues[propName]?.ToString() ?? "null";
                var databaseValue = databaseValues?[propName]?.ToString() ?? "null";
                if (proposedValue != databaseValue)
                {
                    conflictDetails.AppendLine($"Property: {propName}, Proposed: {proposedValue}, Database: {databaseValue}");
                }
            }

            Log.ForContext<BusBuddyDbContext>().Warning("{Conflict}", conflictDetails.ToString());
        }
    }

    private void ApplyStudentPersistenceNormalization()
    {
        foreach (var entry in ChangeTracker.Entries<Student>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            StudentRecordNormalizer.NormalizeForPersistence(entry.Entity);
        }
    }
}
