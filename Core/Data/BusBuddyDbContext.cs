using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace BusBuddy.Core.Data
{
    public class BusBuddyDbContext : DbContext
    {
        // ...existing code...

        /// <summary>
        /// Students table — see EF Core docs: https://learn.microsoft.com/en-us/ef/core/modeling/
        /// </summary>
        public DbSet<Student> Students { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Use SQLite database for development/testing
                optionsBuilder.UseSqlite($"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "BusBuddy.db")}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ...existing code...

            // Ensure Students table is mapped correctly
            modelBuilder.Entity<Student>(entity =>
            {
                entity.ToTable("Students"); // Explicit table name
                entity.HasKey(e => e.StudentId);   // Using StudentId as PK

                // Properties
                entity.Property(e => e.StudentName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StudentNumber).HasMaxLength(20);
            });

            // ...existing code...
        }
    // ...existing code...
    }
}
