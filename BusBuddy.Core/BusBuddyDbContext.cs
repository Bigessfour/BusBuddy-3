// This file is obsolete. Use BusBuddy.Core.Data.BusBuddyDbContext instead.
// Remove all usages and references to this file in the solution.

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

using BusBuddy.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core
{
    public class BusBuddyDbContext : DbContext
    {
        public BusBuddyDbContext(DbContextOptions<BusBuddyDbContext> options) : base(options) { }

        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivitySchedule> ActivitySchedules { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Fuel> Fuels { get; set; }
        public DbSet<Maintenance> Maintenances { get; set; }
        public DbSet<Route> Routes { get; set; }
        public DbSet<SchoolCalendar> SchoolCalendars { get; set; }
        public DbSet<Family> Families { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Bus> Buses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Activity
            modelBuilder.Entity<Activity>().HasKey(a => a.ActivityId);
            modelBuilder.Entity<Activity>().Property(a => a.Destination).HasMaxLength(200);

            // ActivitySchedule
            modelBuilder.Entity<ActivitySchedule>().HasKey(a => a.ActivityScheduleId);
            // No navigation property to Activity in canonical model

            // Driver
            modelBuilder.Entity<Driver>().HasKey(d => d.DriverId);
            modelBuilder.Entity<Driver>().Property(d => d.LicenseNumber).HasMaxLength(20);

            // Fuel
            modelBuilder.Entity<Fuel>().HasKey(f => f.FuelId);
            // No navigation property to Bus in canonical model (use Vehicle if present)

            // Maintenance
            modelBuilder.Entity<Maintenance>().HasKey(m => m.MaintenanceId);
            // No navigation property to Bus in canonical model (use Vehicle if present)

            // Route
            modelBuilder.Entity<Route>().HasKey(r => r.RouteId);
            modelBuilder.Entity<Route>().Property(r => r.RouteName).HasMaxLength(100);

            // SchoolCalendar
            modelBuilder.Entity<SchoolCalendar>().HasKey(s => s.CalendarId);


        }
    }

    // ...existing code...
}
