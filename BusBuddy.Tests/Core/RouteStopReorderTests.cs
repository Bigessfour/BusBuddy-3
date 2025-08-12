using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class RouteStopReorderTests : IDisposable
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);
        public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
        public void Dispose() { }
    }

    private BusBuddyDbContext _context = null!; // primary working context for seeding/verification
    private TestDbContextFactory _factory = null!;
    private RouteService _routeService = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"RouteReorder_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
    _context = new BusBuddyDbContext(options);
    _factory = new TestDbContextFactory(options);
    _routeService = new RouteService(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task ReorderRouteStops_ReordersAndPersistsStopOrder()
    {
        var route = new Route { RouteName = "Test Route", Date = DateTime.Today, School = "Default", IsActive = true };
        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        var stops = new List<RouteStop>
        {
            new() { RouteId = route.RouteId, StopOrder = 1, StopName = "A" },
            new() { RouteId = route.RouteId, StopOrder = 2, StopName = "B" },
            new() { RouteId = route.RouteId, StopOrder = 3, StopName = "C" }
        };
        _context.RouteStops.AddRange(stops);
        await _context.SaveChangesAsync();

        var reversedIds = stops.OrderByDescending(s => s.StopOrder).Select(s => s.RouteStopId).ToList();
        var result = await _routeService.ReorderRouteStopsAsync(route.RouteId, reversedIds);

        Assert.That(result.IsSuccess, Is.True, result.Error);

    // Use a fresh verification context to avoid any tracked entity overlap
    await using var verifyCtx = _factory.CreateDbContext();
    var reloaded = await verifyCtx.RouteStops.Where(rs => rs.RouteId == route.RouteId)
            .OrderBy(rs => rs.StopOrder)
            .Select(rs => new { rs.RouteStopId, rs.StopOrder })
            .ToListAsync();

    // Diagnostics: capture pre and post ordering for debugging
    TestContext.WriteLine("Original IDs (initial order 1..n): " + string.Join(",", stops.Select(s => s.RouteStopId)));
    TestContext.WriteLine("Reversed target IDs: " + string.Join(",", reversedIds));
    TestContext.WriteLine("Reloaded by StopOrder: " + string.Join(",", reloaded.Select(r => $"{r.StopOrder}:{r.RouteStopId}")));

        Assert.That(reloaded, Has.Count.EqualTo(3));
        // Validate each ID got the expected StopOrder per its position in reversedIds
        var orderLookup = reloaded.ToDictionary(r => r.RouteStopId, r => r.StopOrder);
        for (int i = 0; i < reversedIds.Count; i++)
        {
            var id = reversedIds[i];
            Assert.That(orderLookup.ContainsKey(id), $"Missing stop id {id} after reorder");
            Assert.That(orderLookup[id], Is.EqualTo(i + 1), $"Stop {id} expected order {i + 1} but was {orderLookup[id]}");
        }
    }
}
