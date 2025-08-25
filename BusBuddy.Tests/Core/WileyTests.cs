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
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public class WileyTests : IDisposable
    {
    private BusBuddyDbContext? _context;
    private IBusBuddyDbContextFactory? _contextFactory;
        private string? _oldBusBuddyConnection;
        private string? _oldAzureSqlUser;
        private string? _oldAzureSqlPassword;
    private StudentService? _studentService;
    private BusService? _busService;
        private MemoryCache? _memoryCache;
        private BusCachingService? _busCaching_service;
        private SqliteConnection? _sqliteConnection;

        [SetUp]
        public void Setup()
        {
            var connString = "Data Source=file:memdb_WileyTests?mode=memory&cache=shared";
            _sqliteConnection = new SqliteConnection(connString);
            _sqliteConnection.Open();

            var inMemorySettings = new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Local",
                ["ConnectionStrings:DefaultConnection"] = connString,
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Clear environment overrides so tests use the in-memory Sqlite connection configured above
            _oldBusBuddyConnection = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            _oldAzureSqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            _oldAzureSqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_USER", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", null);

            _contextFactory = new BusBuddyDbContextFactory(serviceProvider);
            _context = _contextFactory.CreateDbContext();
            _context.Database.EnsureCreated();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _busCaching_service = new BusCachingService(_memoryCache);
            _busService = new BusService(_contextFactory, _busCaching_service);
            _studentService = new StudentService(_contextFactory);

            var eastById = _context.Routes.FirstOrDefault(r => r.RouteId == 1);
            if (eastById is null)
            {
                _context.Routes.Add(new Route
                {
                    RouteId = 1,
                    RouteName = "East Route",
                    Date = DateTime.Today,
                    IsActive = true,
                    School = "Wiley School District",
                    Boundaries = "east of 287"
                });
            }
            else
            {
                eastById.RouteName = "East Route";
                eastById.Date = DateTime.Today;
                eastById.IsActive = true;
                eastById.School = "Wiley School District";
                eastById.Boundaries = "east of 287";
            }

            if (!_context.Buses.Any(v => v.BusNumber == "17"))
            {
                _context.Buses.Add(new Bus { BusNumber = "17", SeatingCapacity = 48, Status = "Active", Make = "Blue Bird", Model = "Vision", Year = 2020 });
            }

            // Ensure there's a bus that will be matched by route.RouteName in AssignStudentsToRoutesAsync
            // StudentService looks for a bus where Make == route.RouteName (or BusNumber matches),
            // so add a test bus with Make equal to the route name to allow assignment in tests.
            if (!_context.Buses.Any(v => v.Make == "East Route"))
            {
                _context.Buses.Add(new Bus { BusNumber = "EAST1", SeatingCapacity = 48, Status = "Active", Make = "East Route", Model = "RouteBus", Year = 2020 });
            }

            _context.SaveChanges();
        }

        [Test]
        public async Task TestAssignToEastRoute()
        {
            Assert.That(_context, Is.Not.Null);
            Assert.That(_studentService, Is.Not.Null);
            Assert.That(_busService, Is.Not.Null);

            var student = new Student { StudentName = "Test East", HomeAddress = "123 East Hwy 287", Grade = "5", School = "Wiley School District" };
            var route = _context!.Routes.FirstOrDefault(r => r.RouteName == "East Route");
            Assert.That(route, Is.Not.Null, "East Route must exist");

            await _context.Students.AddAsync(student);
            await _context.SaveChangesAsync();

            var assignments = await _studentService!.AssignStudentsToRoutesAsync(_context, new[] { student }, new[] { route! }, _busService!);
            await _context.SaveChangesAsync();

            var updatedStudent = _context.Students.FirstOrDefault(s => s.StudentName == "Test East");
            Assert.That(updatedStudent, Is.Not.Null);
            var assignedRouteId = _context.RouteAssignments.FirstOrDefault(ra => ra.RouteAssignmentId == updatedStudent!.RouteAssignmentId)?.RouteId;
            Assert.That(assignedRouteId, Is.EqualTo(route!.RouteId));
        }

        [Test]
        public async Task TestCapacityCheck()
        {
            Assert.That(_context, Is.Not.Null);
            Assert.That(_busService, Is.Not.Null);

            var bus = _context!.Buses.FirstOrDefault(v => v.BusNumber == "17");
            Assert.That(bus, Is.Not.Null, "Bus #17 must exist");
            var assignedCount = await _busService!.GetAssignedStudentCountAsync(_context, bus!.VehicleId);

            Assert.That(assignedCount, Is.LessThanOrEqualTo(bus.SeatingCapacity));
        }

        public void Dispose()
        {
            _busCaching_service?.Dispose();
            _memoryCache?.Dispose();
            _context?.Dispose();
            _sqliteConnection?.Dispose();
            // Restore environment variables
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", _oldBusBuddyConnection);
            Environment.SetEnvironmentVariable("AZURE_SQL_USER", _oldAzureSqlUser);
            Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", _oldAzureSqlPassword);
            GC.SuppressFinalize(this);
        }
    }
}