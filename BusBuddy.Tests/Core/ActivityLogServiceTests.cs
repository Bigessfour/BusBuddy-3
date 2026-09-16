using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class ActivityLogServiceTests
{
    [Test]
    public async Task GetLogsAndLog_CanRunInParallelOnSeparateContexts()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"activity-parallel-{Guid.NewGuid():N}")
            .Options;
        var factory = new TestDbContextFactory(options);
        await using (var seed = factory.CreateWriteDbContext())
        {
            seed.ActivityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Action = "Seed",
                User = "tester"
            });
            await seed.SaveChangesAsync();
        }

        var service = new ActivityLogService(factory);
        await Task.WhenAll(
            service.GetLogsAsync(50),
            service.GetLogsAsync(50),
            service.LogAsync("Create", "clerk", "parallel"));

        var logs = (await service.GetLogsAsync(50)).ToList();
        Assert.That(logs.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(logs.Any(l => l.Action == "Create"), Is.True);
    }

    [Test]
    public async Task GetLogsByActionAsync_MatchesWithoutStringComparisonOverload()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"activity-action-{Guid.NewGuid():N}")
            .Options;
        var factory = new TestDbContextFactory(options);
        var service = new ActivityLogService(factory);
        await service.LogAsync("DeleteRoute", "clerk");
        await service.LogAsync("CreateStudent", "clerk");

        var deleted = (await service.GetLogsByActionAsync("delete")).ToList();
        Assert.That(deleted, Has.Count.EqualTo(1));
        Assert.That(deleted[0].Action, Is.EqualTo("DeleteRoute"));
    }
}
