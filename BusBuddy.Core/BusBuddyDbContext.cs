
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core
{
    public class BusBuddyDbContext : DbContext
    {
        public BusBuddyDbContext(DbContextOptions<BusBuddyDbContext> options) : base(options) { }

        // Table: Activity
        public DbSet<Activity> Activities { get; set; }

        // Table: ActivitySchedule
        public DbSet<ActivitySchedule> ActivitySchedules { get; set; }

        // Table: Driver
        public DbSet<Driver> Drivers { get; set; }

        // Table: Fuel
        public DbSet<Fuel> Fuels { get; set; }

        // Table: Maintenance
        public DbSet<Maintenance> Maintenances { get; set; }

        // Table: Route
        public DbSet<Route> Routes { get; set; }

        // Table: SchoolCalendar
        public DbSet<SchoolCalendar> SchoolCalendars { get; set; }

        // Table: TimeCard
        public DbSet<TimeCard> TimeCards { get; set; }

        // Table: Vehicle
        public DbSet<Vehicle> Vehicles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Activity
            modelBuilder.Entity<Activity>().HasKey(a => a.Id);
            modelBuilder.Entity<Activity>()
                .HasOne(a => a.Vehicle)
                .WithMany(v => v.Activities)
                .HasForeignKey(a => a.VehicleId);
            modelBuilder.Entity<Activity>()
                .HasOne(a => a.Driver)
                .WithMany(d => d.Activities)
                .HasForeignKey(a => a.DriverId);

            // ActivitySchedule
            modelBuilder.Entity<ActivitySchedule>().HasKey(a => a.Id);
            modelBuilder.Entity<ActivitySchedule>()
                .HasOne(a => a.Activity)
                .WithMany(a => a.Schedules)
                .HasForeignKey(a => a.ActivityId);

            // Driver
            modelBuilder.Entity<Driver>().HasKey(d => d.Id);
            modelBuilder.Entity<Driver>().Property(d => d.LicenseNumber).HasMaxLength(20);

            // Fuel
            modelBuilder.Entity<Fuel>().HasKey(f => f.Id);
            modelBuilder.Entity<Fuel>()
                .HasOne(f => f.Vehicle)
                .WithMany(v => v.Fuels)
                .HasForeignKey(f => f.VehicleId);

            // Maintenance
            modelBuilder.Entity<Maintenance>().HasKey(m => m.Id);
            modelBuilder.Entity<Maintenance>()
                .HasOne(m => m.Vehicle)
                .WithMany(v => v.Maintenances)
                .HasForeignKey(m => m.VehicleId);

            // Route
            modelBuilder.Entity<Route>().HasKey(r => r.Id);
            modelBuilder.Entity<Route>().Property(r => r.RouteName).HasMaxLength(100);

            // SchoolCalendar
            modelBuilder.Entity<SchoolCalendar>().HasKey(s => s.Id);

            // TimeCard
            modelBuilder.Entity<TimeCard>().HasKey(t => t.Id);
            modelBuilder.Entity<TimeCard>()
                .HasOne(t => t.Driver)
                .WithMany(d => d.TimeCards)
                .HasForeignKey(t => t.DriverId);

            // Vehicle
            modelBuilder.Entity<Vehicle>().HasKey(v => v.Id);
            modelBuilder.Entity<Vehicle>().Property(v => v.VehicleNumber).HasMaxLength(10);
        }
    }

    public class Activity
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? BusNumber { get; set; } // e.g., "Bus #17"
        public string? Destination { get; set; }
        public TimeSpan LeaveTime { get; set; }
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }
        public double HoursDriven { get; set; }
        public int StudentsDriven { get; set; }
        public List<ActivitySchedule> Schedules { get; set; } = new();
    }

    public class ActivitySchedule
    {
        public int Id { get; set; }
        public int ActivityId { get; set; }
        public Activity? Activity { get; set; }
        public DateTime ScheduledDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class Driver
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? LicenseNumber { get; set; }
        public DateTime LicenseExpiration { get; set; }
        public List<Activity> Activities { get; set; } = new();
        public List<TimeCard> TimeCards { get; set; } = new();
    }

    public class Fuel
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }
        public DateTime FuelDate { get; set; }
        public double Gallons { get; set; }
        public decimal Cost { get; set; }
    }

    public class Maintenance
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public string? Description { get; set; }
        public decimal Cost { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class Route
    {
        public int Id { get; set; }
        public string? RouteName { get; set; } // e.g., "Truck Plaza"
        public string? AMVehicle { get; set; }
        public int? AMDriverId { get; set; }
        public Driver? AMDriver { get; set; }
        public string? PMVehicle { get; set; }
        public int? PMDriverId { get; set; }
        public Driver? PMDriver { get; set; }
        public int AMRiders { get; set; }
        public int PMRiders { get; set; }
    }

    public class SchoolCalendar
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? EventType { get; set; } // e.g., "School Day", "Holiday"
        public bool IsSchoolDay { get; set; }
    }

    public class TimeCard
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public Driver Driver { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan HoursWorked { get; set; }
    }

    public class Vehicle
    {
        public int Id { get; set; }
        public string? VehicleNumber { get; set; } // e.g., "Bus #17"
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int Year { get; set; }
        public int Capacity { get; set; } // e.g., 14 or 65
        public List<Activity> Activities { get; set; } = new();
        public List<Fuel> Fuels { get; set; } = new();
        public List<Maintenance> Maintenances { get; set; } = new();
    }
}
