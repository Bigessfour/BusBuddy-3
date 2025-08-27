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
    public class WileyTests : DatabaseTestBase
    {
        private StudentService? _studentService;
        private BusService? _busService;
        private MemoryCache? _memoryCache;
        private BusCachingService? _busCaching_service;

        [SetUp]
        public override void SetUp()
        {
            // Call base setup first
            base.SetUp();

            // Initialize additional services for this test
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _busCaching_service = new BusCachingService(_memoryCache);
            _busService = new BusService(ContextFactory, _busCaching_service);
            _studentService = new StudentService(ContextFactory);
        }

        protected override void SeedTestData()
        {
            // Call base seeding first
            base.SeedTestData();

            // Add any additional test data specific to Wiley tests if needed
            // The base class already adds the required routes and buses
        }

        [Test]
        public async Task TestAssignToEastRoute()
        {
            Assert.That(Context, Is.Not.Null);
            Assert.That(_studentService, Is.Not.Null);
            Assert.That(_busService, Is.Not.Null);

            var student = new Student { StudentName = "Test East", HomeAddress = "123 East Hwy 287", Grade = "5", School = "Wiley School District" };
            var route = Context.Routes.FirstOrDefault(r => r.RouteName == "East Route");
            Assert.That(route, Is.Not.Null, "East Route must exist");

            await Context.Students.AddAsync(student);
            await Context.SaveChangesAsync();

            var assignments = await _studentService!.AssignStudentsToRoutesAsync(Context, new[] { student }, new[] { route! }, _busService!);
            await Context.SaveChangesAsync();

            var updatedStudent = Context.Students.FirstOrDefault(s => s.StudentName == "Test East");
            Assert.That(updatedStudent, Is.Not.Null);
            var assignedRouteId = Context.RouteAssignments.FirstOrDefault(ra => ra.RouteAssignmentId == updatedStudent!.RouteAssignmentId)?.RouteId;
            Assert.That(assignedRouteId, Is.EqualTo(route!.RouteId));
        }

        [Test]
        public async Task TestCapacityCheck()
        {
            Assert.That(Context, Is.Not.Null);
            Assert.That(_busService, Is.Not.Null);

            var bus = Context.Buses.FirstOrDefault(v => v.BusNumber == "17");
            Assert.That(bus, Is.Not.Null, "Bus #17 must exist");
            var assignedCount = await _busService!.GetAssignedStudentCountAsync(Context, bus!.BusId);

            Assert.That(assignedCount, Is.LessThanOrEqualTo(bus.SeatingCapacity));
        }

        [TearDown]
        public override void TearDown()
        {
            _busCaching_service?.Dispose();
            _memoryCache?.Dispose();
            base.TearDown();
        }
    }
}
