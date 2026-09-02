using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
var connection = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
if (string.IsNullOrWhiteSpace(connection))
{
    connection = "Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev;Include Error Detail=true";
    Console.WriteLine("BUSBUDDY_CONNECTION not set; using local Docker Postgres default.");
}

Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", connection);

var factory = new BusBuddyDbContextFactory();
var seed = new SeedDataService(factory);

try
{
    if (command is "migrate" or "all")
    {
        await using var ctx = factory.CreateWriteDbContext();
        Console.WriteLine("Applying EF migrations...");
        await ctx.Database.MigrateAsync();
        Console.WriteLine("Migrations applied.");
    }

    if (command is "seed" or "all" or "sn-prep")
    {
        Console.WriteLine("Running special-needs transport prep seed...");
        var summary = await seed.SeedSpecialNeedsTransportPrepAsync();
        foreach (var message in summary.Messages)
        {
            Console.WriteLine($"  - {message}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Special Needs Transport Prep ===");
        Console.WriteLine($"School destination id: {summary.SchoolDestinationId}");
        Console.WriteLine($"Route: {summary.SpecialNeedsRouteName} (id {summary.SpecialNeedsRouteId})");
        Console.WriteLine($"Driver id: {summary.SpecialNeedsDriverId}");
        Console.WriteLine($"Bus id: {summary.SpecialNeedsBusId}");
        Console.WriteLine($"SN students prepared: {summary.SpecialNeedsStudentsPrepared}");
        Console.WriteLine($"Regular students prepared: {summary.RegularStudentsPrepared}");
    }

    if (command is "full-seed")
    {
        await seed.SeedAllAsync();
        Console.WriteLine("Full development seed completed.");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Database prep failed: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}
