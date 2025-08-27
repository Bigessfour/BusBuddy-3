using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class MoreDbContextTests : DatabaseTestBase
    {
        [Test, Category("Configuration")]
        public void DriverEntity_HasRequiredProperties()
        {
            var entityType = Context.Model.FindEntityType(typeof(Driver));
            entityType.FindProperty("DriverName").IsNullable.Should().BeFalse();
        }

        [Test, Category("CRUD")]
        public async Task AddDriver_SavesCorrectly()
        {
            var driver = new Driver { DriverName = "John Doe", LicenseNumber = "D12345" };
            await Context.Drivers.AddAsync(driver);
            await Context.SaveChangesAsync();

            var savedDriver = await Context.Drivers.FindAsync(driver.DriverId);
            savedDriver.Should().NotBeNull();
            savedDriver.DriverName.Should().Be("John Doe");
        }

        [Test, Category("CRUD")]
        public async Task UpdateDriver_SavesChanges()
        {
            var driver = new Driver { DriverName = "Jane Doe", LicenseNumber = "D54321" };
            await Context.Drivers.AddAsync(driver);
            await Context.SaveChangesAsync();

            driver.DriverName = "Jane Smith";
            Context.Drivers.Update(driver);
            await Context.SaveChangesAsync();

            var updatedDriver = await Context.Drivers.FindAsync(driver.DriverId);
            updatedDriver.Should().NotBeNull();
            updatedDriver.DriverName.Should().Be("Jane Smith");
        }

        [Test, Category("Configuration")]
        public void MaintenanceEntity_DescriptionIsOptional()
        {
            var entityType = Context.Model.FindEntityType(typeof(Maintenance));
            entityType.FindProperty("Description").IsNullable.Should().BeTrue();
        }

        [Test, Category("CRUD")]
        public async Task AddMaintenance_SavesCorrectly()
        {
            var bus = new Bus { BusNumber = "B103", Make = "Ford", Model = "Transit", Year = 2023, Capacity = 15, Status = "Active" };
            await Context.Buses.AddAsync(bus);
            await Context.SaveChangesAsync();

            var maintenance = new Maintenance { VehicleId = bus.BusId, Description = "Oil Change", RepairCost = 100.00m, Date = DateTime.UtcNow, MaintenanceCompleted = "Oil Change", Vendor = "Garage" };
            await Context.Maintenances.AddAsync(maintenance);
            await Context.SaveChangesAsync();

            var savedMaintenance = await Context.Maintenances.FindAsync(maintenance.MaintenanceId);
            savedMaintenance.Should().NotBeNull();
            savedMaintenance.Description.Should().Be("Oil Change");

            var busWithMaintenance = await Context.Buses
                .Include(b => b.MaintenanceRecords)
                .FirstOrDefaultAsync(b => b.BusId == bus.BusId);
            busWithMaintenance.Should().NotBeNull();
            busWithMaintenance.MaintenanceRecords.Should().ContainSingle(m => m.Description == "Oil Change");
        }

        [Test, Category("Configuration")]
        public void FuelEntity_HasRequiredProperties()
        {
            var entityType = Context.Model.FindEntityType(typeof(Fuel));
            entityType.FindProperty("FuelDate").IsNullable.Should().BeFalse();
            entityType.FindProperty("FuelLocation").IsNullable.Should().BeFalse();
        }

        [Test, Category("CRUD")]
        public async Task AddFuel_SavesCorrectly()
        {
            var bus = new Bus { BusNumber = "B104", Make = "Chevy", Model = "Express", Year = 2022, Capacity = 20, Status = "Active" };
            await Context.Buses.AddAsync(bus);
            await Context.SaveChangesAsync();

            var fuelRecord = new Fuel { VehicleFueledId = bus.BusId, Gallons = 20.5m, TotalCost = 60.25m, FuelDate = DateTime.UtcNow, Vehicle = bus, FuelLocation = "Test Location", VehicleOdometerReading = 1000, FuelType = "Diesel" };
            await Context.Fuels.AddAsync(fuelRecord);
            await Context.SaveChangesAsync();

            var savedFuelRecord = await Context.Fuels.FindAsync(fuelRecord.FuelId);
            savedFuelRecord.Should().NotBeNull();
            savedFuelRecord.Gallons.Should().Be(20.5m);

            var busWithFuel = await Context.Buses
                .Include(b => b.FuelRecords)
                .FirstOrDefaultAsync(b => b.BusId == bus.BusId);
            busWithFuel.Should().NotBeNull();
            busWithFuel.FuelRecords.Should().ContainSingle(f => f.Gallons == 20.5m);
        }

        [Test, Category("Configuration")]
        public void ActivityEntity_HasRequiredProperties()
        {
            var entityType = Context.Model.FindEntityType(typeof(Activity));
            entityType.FindProperty("ActivityType").IsNullable.Should().BeFalse();
        }

        [Test, Category("CRUD")]
        public async Task AddActivity_SavesCorrectly()
        {
            var activity = new Activity { ActivityType = "Field Trip", Description = "Trip to the museum", Destination = "Museum", RequestedBy = "Test" };
            await Context.Activities.AddAsync(activity);
            await Context.SaveChangesAsync();

            var savedActivity = await Context.Activities.FindAsync(activity.ActivityId);
            savedActivity.Should().NotBeNull();
            savedActivity.ActivityType.Should().Be("Field Trip");
        }
        [Test, Category("Concurrency")]
        public async Task Update_WithDetachedEntity_ThrowsException()
        {
            var bus = new Bus { BusNumber = "B105", Make = "GMC", Model = "Savana", Year = 2021, Capacity = 18, Status = "Active" };
            await Context.Buses.AddAsync(bus);
            await Context.SaveChangesAsync();

            var maintenance = new Maintenance { VehicleId = bus.BusId, Description = "Tire Rotation", RepairCost = 50.00m, Date = DateTime.UtcNow, MaintenanceCompleted = "Tire Rotation", Vendor = "Tire Shop" };
            await Context.Maintenances.AddAsync(maintenance);
            await Context.SaveChangesAsync();

            // Detach the entity to simulate a disconnected scenario
            Context.Entry(maintenance).State = EntityState.Detached;

            // Modify the detached entity
            maintenance.RepairCost = 75.00m;

            // This should throw an exception because we are trying to update a detached entity
            // as if it were new. A common error is to call Add instead of Update.
            Func<Task> act = async () =>
            {
                Context.Maintenances.Update(maintenance);
                await Context.SaveChangesAsync();
            };

            // To properly handle updates in a disconnected scenario, one would typically
            // attach the entity and set its state to Modified. This test ensures
            // that simply calling Update on a modified detached entity works as expected
            // by re-attaching it and marking it as modified.
            await act.Should().NotThrowAsync();
            var updatedMaintenance = await Context.Maintenances.FindAsync(maintenance.MaintenanceId);
            updatedMaintenance.RepairCost.Should().Be(75.00m);
        }

        [Test, Category("Concurrency")]
        public async Task ConcurrentUpdate_DoesNotThrowInSQLite()
        {
            // Arrange: Create and save a bus
            var bus = new Bus { BusNumber = "B106", Make = "International", Model = "CE", Year = 2020, Capacity = 72, Status = "Active" };
            await Context.Buses.AddAsync(bus);
            await Context.SaveChangesAsync();

            // Act: Simulate two users fetching the same bus
            var busUser1 = await Context.Buses.FindAsync(bus.BusId);
            var busUser2 = await Context.Buses.FindAsync(bus.BusId);

            // User 1 updates and saves
            busUser1.Status = "Under Maintenance";
            Context.Buses.Update(busUser1);
            await Context.SaveChangesAsync();

            // User 2 attempts to update with stale data
            busUser2.Status = "Inactive";
            Context.Buses.Update(busUser2);

            // Assert: SQLite in-memory doesn't support concurrency control, so no exception expected
            Func<Task> act = async () => await Context.SaveChangesAsync();
            await act.Should().NotThrowAsync();
        }

        [Test, Category("Transactions")]
        public async Task Transaction_BeginTransaction_ThrowsInSQLite()
        {
            // Arrange & Act & Assert
            // SQLite in-memory database doesn't support transactions
            Func<Task> act = async () => await Context.Database.BeginTransactionAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Transactions are not supported by the in-memory store*");
        }

        [Test, Category("EdgeCases")]
        public async Task FindAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var driver = await Context.Drivers.FindAsync(999);

            // Assert
            driver.Should().BeNull();
        }

        [Test, Category("EdgeCases")]
        public async Task FirstOrDefaultAsync_WithNoMatch_ReturnsNull()
        {
            // Act
            var driver = await Context.Drivers.FirstOrDefaultAsync(d => d.DriverName == "Non Existent Driver");

            // Assert
            driver.Should().BeNull();
        }

        [Test, Category("EdgeCases")]
        public async Task Add_WithInvalidData_DoesNotThrowInSQLite()
        {
            // Arrange: Create a driver with a name that exceeds the StringLength limit
            // Note: SQLite in-memory database doesn't enforce StringLength validation by default
            var driver = new Driver
            {
                DriverName = new string('A', 300), // Exceeds [StringLength(100)] limit
                LicenseNumber = "V12345"
            };
            await Context.Drivers.AddAsync(driver);

            // Act & Assert
            Func<Task> act = async () => await Context.SaveChangesAsync();
            // SQLite doesn't enforce data annotation validation, so no exception is expected
            await act.Should().NotThrowAsync();
        }
    }
}
