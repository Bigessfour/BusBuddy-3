using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Configurations;

/// <summary>
/// HasData rows for relational providers. Tests set <see cref="BusBuddyDbContext.SkipGlobalSeedData"/>.
/// </summary>
internal static class ModelHasData
{
    internal static void Apply(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Bus>().HasData(
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
                PurchaseDate = new DateTime(2019, 7, 10),
                PurchasePrice = 82000.00m,
                CreatedDate = seedDate
            });

        modelBuilder.Entity<Driver>().HasData(
            new Driver
            {
                DriverId = 1,
                DriverName = "John Smith",
                DriverPhone = "555-0123",
                DriverEmail = "john.smith@school.edu",
                DriversLicenceType = "CDL",
                TrainingComplete = true,
                CreatedDate = seedDate
            },
            new Driver
            {
                DriverId = 2,
                DriverName = "Mary Johnson",
                DriverPhone = "555-0456",
                DriverEmail = "mary.johnson@school.edu",
                DriversLicenceType = "CDL",
                TrainingComplete = true,
                CreatedDate = seedDate
            });
    }
}
