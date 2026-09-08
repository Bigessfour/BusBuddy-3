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
    // Roster intake is deliberately standalone: it never runs migrations, so importing a local
    // roster cannot race a pending schema change. See Documentation/STUDENT-ROSTER-INTAKE.md.
    if (command is "import-roster")
    {
        var rosterPath = args.Length > 1 ? args[1] : null;
        if (string.IsNullOrWhiteSpace(rosterPath))
        {
            Console.Error.WriteLine("Usage: dotnet run --project BusBuddy.DbPrep -- import-roster <path-to-roster.csv>");
            return 2;
        }

        Console.WriteLine($"Importing roster from {rosterPath} ...");
        var imported = await seed.ImportStudentsFromCsvAsync(rosterPath);
        Console.WriteLine($"Students added: {imported}");

        // Counts and flags only. Never echo names, addresses, or phone numbers to the console.
        await using var verify = factory.CreateDbContext();
        var total = await verify.Students.CountAsync();
        var specialNeeds = await verify.Students.CountAsync(s => s.RequiresSpecialNeedsBus);
        var needAide = await verify.Students.CountAsync(s => s.RequiresAide);
        var homePickup = await verify.Students.CountAsync(s => s.PickupStopId == null);
        var awaitingGeocode = await verify.Students.CountAsync(s => s.Latitude == null || s.Longitude == null);
        var unlinkedCampus = await verify.Students.CountAsync(s => s.DestinationId == null);

        Console.WriteLine();
        Console.WriteLine("=== Roster import verification (PII-free) ===");
        Console.WriteLine($"Students in database:        {total}");
        Console.WriteLine($"Special-needs riders:        {specialNeeds}");
        Console.WriteLine($"Aide required:               {needAide}");
        Console.WriteLine($"Home pickup (no catalog stop): {homePickup}");
        Console.WriteLine($"Awaiting geocode:            {awaitingGeocode}");
        Console.WriteLine($"Campus not linked to Destinations: {unlinkedCampus}");
        return 0;
    }

    if (command is "ensure-routes")
    {
        Console.WriteLine("Creating missing Routes rows for student AM/PM assignments...");
        var created = await seed.EnsureRoutesForStudentAssignmentsAsync();
        Console.WriteLine($"Routes created or spelling repaired: {created}");
        return 0;
    }

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
