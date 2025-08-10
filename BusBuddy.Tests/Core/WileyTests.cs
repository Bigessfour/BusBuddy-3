using NUnit.Framework;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Data;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class WileyTests : IDisposable
{
    private BusBuddyDbContext? _context;
    private StudentService? _studentService;
    private BusService? _busService;
    private MemoryCache? _memoryCache;
    private BusCachingService? _busCachingService;

    [SetUp]
    public void Setup()
    {
        var contextFactory = new BusBuddyDbContextFactory();
        _context = contextFactory.CreateDbContext();
        _studentService = new StudentService(contextFactory);
    _memoryCache = new MemoryCache(new MemoryCacheOptions());
    _busCachingService = new BusCachingService(_memoryCache);
    _busService = new BusService(contextFactory, _busCachingService);

        // Ensure baseline data required by tests
        // Guarantee that "East Route" exists with RouteId = 1 for deterministic assertions
        // Docs: DbSet.Find (sync) — https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbset-1.find
        var eastById = _context.Routes.Find(1);
        if (eastById is null)
        {
            _context.Routes.Add(new Route
            {
                RouteId = 1,
                RouteName = "East Route",
                Date = System.DateTime.Today,
                IsActive = true,
                School = "Wiley School District",
                Boundaries = "east of 287"
            });
        }
        else
        {
            // Ensure properties are correct
            eastById.RouteName = "East Route";
            eastById.Date = System.DateTime.Today;
            eastById.IsActive = true;
            eastById.School = "Wiley School District";
            eastById.Boundaries = "east of 287";
        }
        if (!_context.Buses.Any(v => v.BusNumber == "17"))
        {
            _context.Buses.Add(new Bus { BusNumber = "17", SeatingCapacity = 48, Status = "Active", Make = "Blue Bird", Model = "Vision", Year = 2020 });
        }
        _context.SaveChanges();
    }

    [Test]
    public async Task TestAssignToEastRoute()
    {
        // Arrange: Seed a student with address east of 287
        Assert.That(_context, Is.Not.Null);
        Assert.That(_studentService, Is.Not.Null);
        Assert.That(_busService, Is.Not.Null);
        var student = new Student { StudentName = "Test East", HomeAddress = "123 East Hwy 287", Grade = "5", School = "Wiley School District" };
        var route = _context!.Routes.FirstOrDefault(r => r.RouteName == "East Route");
        Assert.That(route, Is.Not.Null, "East Route must exist");
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        // Act: Assign student to route
        var assignments = await _studentService!.AssignStudentsToRoutesAsync(_context, new[] { student }, new[] { route! }, _busService!);
        await _context.SaveChangesAsync();

        // Assert: Student assigned to East Route
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
        // Arrange: Get Bus #17 and ensure capacity
        var bus = _context!.Buses.FirstOrDefault(v => v.BusNumber == "17");
        Assert.That(bus, Is.Not.Null, "Bus #17 must exist");
        var assignedCount = await _busService!.GetAssignedStudentCountAsync(_context, bus!.VehicleId);

        // Act & Assert: Should be under capacity
        Assert.That(assignedCount, Is.LessThanOrEqualTo(bus.SeatingCapacity));
    }
    public void Dispose()
    {
        _busCachingService?.Dispose();
        _memoryCache?.Dispose();
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
