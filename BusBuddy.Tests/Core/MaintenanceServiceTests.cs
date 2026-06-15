using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class MaintenanceServiceTests : IDisposable
    {
        private BusBuddyDbContext _context = null!;
        private IServiceProvider _serviceProvider = null!;
        private MaintenanceService _service;
        private bool _disposed;

        [SetUp]
        public void Setup()
        {
            // Simple in-memory setup (matches DataLayerTests pattern) - proves MaintenanceService "works"
            var services = new ServiceCollection();
            services.AddDbContext<BusBuddyDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
            services.AddLogging();
            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<BusBuddyDbContext>();
            _context.Database.EnsureCreated();

            // Use a simple factory wrapper over the context for the service (common test pattern)
            var factory = new InMemoryContextFactory(_context);
            _service = new MaintenanceService(factory);
        }

        [TearDown]
        public void TearDown()
        {
            Dispose();
        }

        [Test]
        public async Task GetAllMaintenanceRecordsAsync_ReturnsRecords()
        {
            // Arrange - proves the maintenance flow item (core "works")
            _context.MaintenanceRecords.Add(new Maintenance { VehicleId = 1, Description = "Oil change", Date = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            // Act
            var records = (await _service.GetAllMaintenanceRecordsAsync()).ToList();

            // Assert
            Assert.That(records, Is.Not.Empty);
            Assert.That(records[0].Description, Does.Contain("Oil"));
        }

        [Test]
        public async Task CreateMaintenanceRecordAsync_PersistsAndReturnsWithTimestamp()
        {
            // Arrange
            var record = new Maintenance { VehicleId = 1, Description = "Brake service", Date = DateTime.UtcNow };

            // Act
            var created = await _service.CreateMaintenanceRecordAsync(record);

            // Assert - proves create + timestamp + query
            Assert.That(created.MaintenanceId, Is.GreaterThan(0));
            Assert.That(created.CreatedDate, Is.Not.EqualTo(default(DateTime)));
            var all = await _service.GetAllMaintenanceRecordsAsync();
            Assert.That(all.Any(r => r.Description == "Brake service"));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
            _disposed = true;
        }
    }

    // Lightweight in-memory factory for service (avoids duplicate type conflict with RouteServiceTests)
    internal class InMemoryContextFactory : IBusBuddyDbContextFactory
    {
        private readonly BusBuddyDbContext _ctx;
        public InMemoryContextFactory(BusBuddyDbContext ctx) => _ctx = ctx;
        public BusBuddyDbContext CreateDbContext() => _ctx;
        public BusBuddyDbContext CreateWriteDbContext() => _ctx;
    }
}
