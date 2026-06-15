using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class MaintenanceServiceTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _options = null!;
        private MaintenanceService _service = null!;
        private bool _disposed;

        [SetUp]
        public void Setup()
        {
            _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var seed = new BusBuddyDbContext(_options);
            seed.Buses.Add(new Bus
            {
                BusId = 1,
                BusNumber = "001",
                Year = 2020,
                Make = "Test",
                Model = "Bus",
                SeatingCapacity = 50,
                Status = "Active"
            });
            seed.Database.EnsureCreated();
            seed.SaveChanges();

            _service = new MaintenanceService(new InMemoryContextFactory(_options));
        }

        [TearDown]
        public void TearDown()
        {
            Dispose();
        }

        [Test]
        public async Task GetAllMaintenanceRecordsAsync_ReturnsRecords()
        {
            await _service.CreateMaintenanceRecordAsync(new Maintenance
            {
                VehicleId = 1,
                Description = "Oil change",
                Date = DateTime.UtcNow,
                MaintenanceCompleted = "Oil change",
                Vendor = "Test Vendor",
                OdometerReading = 10000,
                RepairCost = 50m
            });

            var records = (await _service.GetAllMaintenanceRecordsAsync()).ToList();

            Assert.That(records, Is.Not.Empty);
            Assert.That(records[0].Description, Does.Contain("Oil"));
        }

        [Test]
        public async Task CreateMaintenanceRecordAsync_PersistsAndReturnsWithTimestamp()
        {
            var record = new Maintenance
            {
                VehicleId = 1,
                Description = "Brake service",
                Date = DateTime.UtcNow,
                MaintenanceCompleted = "Brake service",
                Vendor = "Test Vendor",
                OdometerReading = 12000,
                RepairCost = 150m
            };

            var created = await _service.CreateMaintenanceRecordAsync(record);

            Assert.That(created.MaintenanceId, Is.GreaterThan(0));
            Assert.That(created.CreatedDate, Is.Not.EqualTo(default(DateTime)));
            var all = await _service.GetAllMaintenanceRecordsAsync();
            Assert.That(all.Any(r => r.Description == "Brake service"));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    internal class InMemoryContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public InMemoryContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);
        public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
    }
}
