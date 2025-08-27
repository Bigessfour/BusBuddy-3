using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class WileyTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private IBusBuddyDbContextFactory _contextFactory = null!;
        private StudentService? _studentService;
        private BusService? _busService;
        private MemoryCache? _memoryCache;
        private BusCachingService? _busCaching_service;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;
            public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
            public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);
            public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
            public void Dispose() { }
        }

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"WileyTests_{Guid.NewGuid()}_{DateTime.Now.Ticks}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);
            _contextFactory = new TestDbContextFactory(_dbOptions);

            // Ensure database is created
            _dbContext.Database.EnsureCreated();

            // Seed test data
            SeedTestData();

            // Initialize additional services for this test
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _busCaching_service = new BusCachingService(_memoryCache);
            _busService = new BusService(_contextFactory, _busCaching_service);
            _studentService = new StudentService(_contextFactory);
        }

        [TearDown]
        public void TearDown()
        {
            _busCaching_service?.Dispose();
            _memoryCache?.Dispose();
            _dbContext?.Dispose();
        }

        public void Dispose()
        {
            TearDown();
            GC.SuppressFinalize(this);
        }

        private void SeedTestData()
        {
            // Add test routes
            if (!_dbContext.Routes.Any())
            {
                _dbContext.Routes.AddRange(
                    new Route
                    {
                        RouteId = 1,
                        RouteName = "Route A",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Test School",
                        Boundaries = "Test Area A"
                    },
                    new Route
                    {
                        RouteId = 2,
                        RouteName = "Route B",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Test School",
                        Boundaries = "Test Area B"
                    },
                    new Route
                    {
                        RouteId = 3,
                        RouteName = "East Route",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Wiley School District",
                        Boundaries = "east of 287"
                    }
                );
            }

            // Add test buses
            if (!_dbContext.Buses.Any())
            {
                _dbContext.Buses.AddRange(
                    new Bus
                    {
                        BusNumber = "17",
                        SeatingCapacity = 48,
                        Status = "Active",
                        Make = "Blue Bird",
                        Model = "Vision",
                        Year = 2020
                    },
                    new Bus
                    {
                        BusNumber = "EAST1",
                        SeatingCapacity = 65,
                        Status = "Active",
                        Make = "Thomas",
                        Model = "Saf-T-Liner",
                        Year = 2021
                    }
                );
            }

            _dbContext.SaveChanges();
        }
    }
}
