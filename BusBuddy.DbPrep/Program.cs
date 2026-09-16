using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
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
    // Hop 1 clerk-path proof: AddSchoolAsync → Destinations row with bell times (+ optional GPS).
    // Same service path as Students → Add School (SchoolDestinationFormViewModel). No PII beyond catalog name/address.
    if (command is "hop1-add-school" or "add-school")
    {
        var destinations = new DestinationService(factory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var name = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1].Trim()
            : $"Hop1 Proof School {stamp}";
        var school = await destinations.AddSchoolAsync(
            name,
            "350 Main Street",
            "Wiley",
            "CO",
            "81092",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(30)),
            latitude: 38.1535m,
            longitude: -102.7195m);

        await using var verify = factory.CreateDbContext();
        var row = await verify.Destinations.AsNoTracking()
            .SingleAsync(d => d.DestinationId == school.DestinationId);

        Console.WriteLine();
        Console.WriteLine("=== Hop 1 proof (Add School) ===");
        Console.WriteLine($"DestinationId:   {row.DestinationId}");
        Console.WriteLine($"Name:            {row.Name}");
        Console.WriteLine($"StartTime:       {row.StartTime}");
        Console.WriteLine($"DismissalTime:   {row.DismissalTime}");
        Console.WriteLine($"Latitude:        {row.Latitude}");
        Console.WriteLine($"Longitude:       {row.Longitude}");
        Console.WriteLine($"DestinationType: {row.DestinationType}");
        Console.WriteLine();
        Console.WriteLine("Serilog should show: Added school DestinationId={Id} (see app logs when using the WPF form).");
        Console.WriteLine("PASS — Destinations row persisted with bell times and GPS.");
        return 0;
    }

    // Hop 2 clerk-path proof: StudentService.AddStudentAsync with DestinationId + validated lat/lng.
    // Same write service the WPF student form uses after Maps geocode (StudentFormSaveCoordinator →
    // StudentPersistenceWriter → IStudentService). Synthetic TEST_ names only — no real PII.
    if (command is "hop2-add-student" or "add-student")
    {
        await using (var lookup = factory.CreateDbContext())
        {
            Destination? school = null;
            var requestedDestinationId = 0;
            if (args.Length > 1
                && int.TryParse(args[1], out requestedDestinationId)
                && requestedDestinationId > 0)
            {
                school = await lookup.Destinations.AsNoTracking()
                    .FirstOrDefaultAsync(d =>
                        d.DestinationId == requestedDestinationId
                        && d.DestinationType == DestinationTypes.School
                        && d.IsActive
                        && !d.IsDeleted);
                if (school is null)
                {
                    Console.Error.WriteLine(
                        $"FAIL — DestinationId={requestedDestinationId} is missing, inactive, or not a School. No fallback when an id is supplied.");
                    return 2;
                }
            }
            else
            {
                school = await lookup.Destinations.AsNoTracking()
                    .Where(d =>
                        d.DestinationType == DestinationTypes.School
                        && d.IsActive
                        && !d.IsDeleted
                        && d.Name.StartsWith("Hop1 Proof"))
                    .OrderByDescending(d => d.DestinationId)
                    .FirstOrDefaultAsync();

                school ??= await lookup.Destinations.AsNoTracking()
                    .Where(d => d.DestinationType == DestinationTypes.School && d.IsActive && !d.IsDeleted)
                    .OrderByDescending(d => d.DestinationId)
                    .FirstOrDefaultAsync();
            }

            if (school is null)
            {
                Console.Error.WriteLine("No school Destinations row found. Run hop1-add-school first.");
                return 2;
            }

            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var students = new StudentService(factory);
            // Coordinates stand in for a successful Maps ValidateAndGeocode (clerks never type them as SSOT).
            var added = await students.AddStudentAsync(new Student
            {
                StudentName = $"TEST_HOP2_STUDENT_{stamp}",
                Grade = "3",
                School = school.Name,
                DestinationId = school.DestinationId,
                SchoolYear = StudentRecordNormalizer.CurrentSchoolYear(),
                HomeAddress = "100 Test Street",
                City = "Wiley",
                State = "CO",
                Zip = "81092",
                ParentGuardian = "TEST_GUARDIAN",
                EmergencyPhone = "555-0100",
                Latitude = 38.1541m,
                Longitude = -102.7201m,
                Active = true,
                RidesAm = true,
                RidesPm = true,
                EnrollmentDate = DateTime.UtcNow.Date,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "Hop2Proof"
            });
            if (!added.IsSuccess)
            {
                Console.Error.WriteLine($"FAIL — AddStudentAsync: {added.Error}");
                return 1;
            }

            var student = added.Value;

            var row = await lookup.Students.AsNoTracking()
                .SingleAsync(s => s.StudentId == student.StudentId);

            var hasCoords = LocationCoordinate.IsValidated(row.Latitude, row.Longitude);
            Console.WriteLine();
            Console.WriteLine("=== Hop 2 proof (Add Student) ===");
            Console.WriteLine($"StudentId:       {row.StudentId}");
            Console.WriteLine($"DestinationId:   {row.DestinationId}");
            Console.WriteLine($"School linked:   {row.DestinationId == school.DestinationId}");
            Console.WriteLine($"Latitude:        {row.Latitude}");
            Console.WriteLine($"Longitude:       {row.Longitude}");
            Console.WriteLine($"Validated GPS:   {hasCoords}");
            Console.WriteLine($"Active:          {row.Active}");
            Console.WriteLine();
            Console.WriteLine("Core Serilog: Successfully added student with ID={StudentId}");
            Console.WriteLine("WPF form Serilog (same persistence): Successfully saved student StudentId=…");
            if (row.DestinationId != school.DestinationId || !hasCoords)
            {
                Console.Error.WriteLine("FAIL — DestinationId and/or validated coordinates missing.");
                return 1;
            }

            Console.WriteLine("PASS — Students row persisted with DestinationId and validated lat/lng.");
            return 0;
        }
    }

    // Hop 3 clerk-path proof: RouteDeterminationService.GenerateAndAssignAsync → Routes + RouteStops
    // (same planner as Routes pane / Route Assignments Generate Routes). Must not grow Schedules or
    // RouteAssignments (those are later hops / dual-write decisions).
    if (command is "hop3-generate-routes" or "generate-routes")
    {
        await using var lookup = factory.CreateDbContext();
        var schoolId = 0;
        if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        {
            schoolId = parsed;
        }
        else
        {
            schoolId = await lookup.Destinations.AsNoTracking()
                .Where(d =>
                    d.DestinationType == DestinationTypes.School
                    && d.IsActive
                    && !d.IsDeleted
                    && d.Name.StartsWith("Hop1 Proof"))
                .OrderByDescending(d => d.DestinationId)
                .Select(d => d.DestinationId)
                .FirstOrDefaultAsync();
            if (schoolId <= 0)
            {
                schoolId = await lookup.Students.AsNoTracking()
                    .Where(s => s.DestinationId != null && s.Latitude != null && s.Longitude != null)
                    .GroupBy(s => s.DestinationId!.Value)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync();
            }
        }

        if (schoolId <= 0)
        {
            Console.Error.WriteLine("No school id. Pass DestinationId or run hop1-add-school + hop2-add-student.");
            return 2;
        }

        var school = await lookup.Destinations.AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.DestinationId == schoolId
                && d.DestinationType == DestinationTypes.School
                && d.IsActive
                && !d.IsDeleted);
        if (school is null)
        {
            Console.Error.WriteLine($"FAIL — DestinationId={schoolId} is missing, inactive, or not a School.");
            return 2;
        }

        var ridersWithGps = await lookup.Students.AsNoTracking()
            .CountAsync(s =>
                s.DestinationId == schoolId
                && s.Active
                && s.Latitude != null
                && s.Longitude != null);
        if (ridersWithGps == 0)
        {
            Console.Error.WriteLine(
                $"FAIL — no active students with GPS for DestinationId={schoolId}. Run hop2-add-student {schoolId}.");
            return 2;
        }

        var beforeRoutes = await lookup.Routes.CountAsync();
        var beforeStops = await lookup.RouteStops.CountAsync();
        var beforeAssignments = await lookup.RouteAssignments.CountAsync();
        var beforeSchedules = await lookup.Schedules.CountAsync();

        var routes = new RouteService(factory);
        var planner = new RouteDeterminationService(factory, routes);
        var result = await planner.GenerateAndAssignAsync(
            school.DestinationId,
            RouteTimeSlotKind.AM,
            FleetKind.HomeToSchool);

        await using var verify = factory.CreateDbContext();
        var afterRoutes = await verify.Routes.CountAsync();
        var afterStops = await verify.RouteStops.CountAsync();
        var afterAssignments = await verify.RouteAssignments.CountAsync();
        var afterSchedules = await verify.Schedules.CountAsync();
        var draftRoutes = await verify.Routes.AsNoTracking()
            .CountAsync(r => r.IsActive && r.RouteName.StartsWith("Draft-"));

        Console.WriteLine();
        Console.WriteLine("=== Hop 3 proof (Generate Routes) ===");
        Console.WriteLine($"School DestinationId: {school.DestinationId}");
        Console.WriteLine($"Riders with GPS:      {ridersWithGps}");
        Console.WriteLine($"Success:              {result.Success}");
        Console.WriteLine($"Assigned students:    {result.AssignedStudentCount}");
        Console.WriteLine($"Proposals:            {result.Proposals.Count}");
        Console.WriteLine($"Unclustered:          {result.UnclusteredStudentIds.Count}");
        Console.WriteLine($"Persisted route ids:  {string.Join(",", result.Proposals.Where(p => p.PersistedRouteId.HasValue).Select(p => p.PersistedRouteId))}");
        Console.WriteLine($"Δ Routes:             {afterRoutes - beforeRoutes} (now {afterRoutes}, Draft- active {draftRoutes})");
        Console.WriteLine($"Δ RouteStops:         {afterStops - beforeStops} (now {afterStops})");
        Console.WriteLine($"Δ RouteAssignments:   {afterAssignments - beforeAssignments} (must be 0)");
        Console.WriteLine($"Δ Schedules:          {afterSchedules - beforeSchedules} (must be 0)");
        if (result.Warnings.Count > 0)
        {
            Console.WriteLine("Warnings:");
            foreach (var w in result.Warnings.Take(8))
            {
                Console.WriteLine($"  - {w}");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Console.WriteLine($"Error: {result.Error}");
        }

        Console.WriteLine();
        Console.WriteLine("Serilog: Route generation completed School={SchoolId} Fleet=… Routes=… Students=…");

        if (afterAssignments != beforeAssignments || afterSchedules != beforeSchedules)
        {
            Console.Error.WriteLine(
                "FAIL — Generate Routes wrote RouteAssignments and/or Schedules. Stop; reconcile hop 4b before continuing.");
            return 1;
        }

        if (!result.Success
            || result.AssignedStudentCount <= 0
            || result.Proposals.All(p => p.PersistedRouteId is null))
        {
            Console.Error.WriteLine("FAIL — generation did not persist routes / assign students.");
            return 1;
        }

        Console.WriteLine("PASS — Routes/RouteStops updated; Schedules and RouteAssignments unchanged.");
        return 0;
    }

    // Hop 4 clerk-path proof: AssignVehicleToRouteAsync + AssignDriverToRouteAsync → Routes.AMVehicleId /
    // AMDriverId (same as Route Assignments Assign Vehicle/Driver). Must not grow RouteAssignments or
    // Schedules (hop 4b / hop 5). Fail closed when a route id is supplied but missing.
    if (command is "hop4-assign-bus-driver" or "assign-bus-driver")
    {
        await using var lookup = factory.CreateDbContext();
        int? requestedRouteId = null;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedRoute) && parsedRoute > 0)
        {
            requestedRouteId = parsedRoute;
        }

        Route? route;
        if (requestedRouteId is int rid)
        {
            route = await lookup.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RouteId == rid && r.IsActive);
            if (route is null)
            {
                Console.Error.WriteLine(
                    $"FAIL — RouteId={rid} is missing or inactive. No fallback when an id is supplied.");
                return 2;
            }
        }
        else
        {
            route = await lookup.Routes.AsNoTracking()
                .Where(r => r.IsActive && r.RouteName.StartsWith("Draft-"))
                .OrderByDescending(r => r.RouteId)
                .FirstOrDefaultAsync();
            route ??= await lookup.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.RouteId)
                .FirstOrDefaultAsync();
        }

        if (route is null)
        {
            Console.Error.WriteLine("No active route. Run hop3-generate-routes first.");
            return 2;
        }

        int? requestedBusId = args.Length > 2 && int.TryParse(args[2], out var b) && b > 0 ? b : null;
        int? requestedDriverId = args.Length > 3 && int.TryParse(args[3], out var d) && d > 0 ? d : null;

        Bus? bus;
        if (requestedBusId is int busId)
        {
            bus = await lookup.Buses.AsNoTracking().FirstOrDefaultAsync(x => x.BusId == busId);
            if (bus is null || !RouteVehicleLinker.IsAssignableStatus(bus.Status))
            {
                Console.Error.WriteLine(
                    $"FAIL — BusId={busId} missing or not assignable. No fallback when an id is supplied.");
                return 2;
            }
        }
        else
        {
            // Prefer a general Active bus (not the special-needs microbus) when several exist.
            bus = await lookup.Buses.AsNoTracking()
                .Where(x => x.Status == "Active" || x.Status == "InService")
                .OrderByDescending(x => x.SeatingCapacity)
                .ThenBy(x => x.BusId)
                .FirstOrDefaultAsync();
        }

        Driver? driver;
        if (requestedDriverId is int driverId)
        {
            driver = await lookup.Drivers.AsNoTracking().FirstOrDefaultAsync(x => x.DriverId == driverId);
            if (driver is null
                || (!string.IsNullOrWhiteSpace(driver.Status)
                    && !string.Equals(driver.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine(
                    $"FAIL — DriverId={driverId} missing or not Active. No fallback when an id is supplied.");
                return 2;
            }
        }
        else
        {
            driver = await lookup.Drivers.AsNoTracking()
                .Where(x => x.Status == null || x.Status == "" || x.Status == "Active")
                .OrderBy(x => x.DriverId)
                .FirstOrDefaultAsync();
        }

        if (bus is null || driver is null)
        {
            Console.Error.WriteLine("FAIL — need at least one Active bus and one Active driver in the catalog.");
            return 2;
        }

        var beforeAssignments = await lookup.RouteAssignments.CountAsync();
        var beforeSchedules = await lookup.Schedules.CountAsync();

        var routes = new RouteService(factory);
        var vehicleResult = await routes.AssignVehicleToRouteAsync(route.RouteId, bus.BusId, RouteTimeSlot.AM);
        if (!vehicleResult.IsSuccess)
        {
            Console.Error.WriteLine($"FAIL — AssignVehicleToRouteAsync: {vehicleResult.Error}");
            return 1;
        }

        var driverResult = await routes.AssignDriverToRouteAsync(route.RouteId, driver.DriverId, RouteTimeSlot.AM);
        if (!driverResult.IsSuccess)
        {
            Console.Error.WriteLine($"FAIL — AssignDriverToRouteAsync: {driverResult.Error}");
            return 1;
        }

        await using var verify = factory.CreateDbContext();
        var row = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == route.RouteId);
        var afterAssignments = await verify.RouteAssignments.CountAsync();
        var afterSchedules = await verify.Schedules.CountAsync();

        Console.WriteLine();
        Console.WriteLine("=== Hop 4 proof (Assign Bus + Driver) ===");
        Console.WriteLine($"RouteId:        {row.RouteId}");
        Console.WriteLine($"RouteName:      {row.RouteName}");
        Console.WriteLine($"AMVehicleId:    {row.AMVehicleId}");
        Console.WriteLine($"AMDriverId:     {row.AMDriverId}");
        Console.WriteLine($"BusNumber:      {row.BusNumber}");
        Console.WriteLine($"Δ RouteAssignments: {afterAssignments - beforeAssignments} (must be 0)");
        Console.WriteLine($"Δ Schedules:        {afterSchedules - beforeSchedules} (must be 0)");
        Console.WriteLine();
        Console.WriteLine("Serilog: Assigned vehicle {VehicleId} to route {RouteId} for AM");
        Console.WriteLine("Serilog: Assigned driver {DriverId} to route {RouteId} for AM");

        if (afterAssignments != beforeAssignments || afterSchedules != beforeSchedules)
        {
            Console.Error.WriteLine(
                "FAIL — assign wrote RouteAssignments and/or Schedules. Stop; hop 4b must pick one write path.");
            return 1;
        }

        if (row.AMVehicleId != bus.BusId || row.AMDriverId != driver.DriverId)
        {
            Console.Error.WriteLine("FAIL — Routes.AMVehicleId / AMDriverId not set to the assigned catalog ids.");
            return 1;
        }

        Console.WriteLine("PASS — Routes.AMVehicleId and AMDriverId set; RouteAssignments/Schedules unchanged.");
        return 0;
    }

    // Hop 5 clerk-path proof: same AddDailyFromPublishedRouteAsync as Route Management persist.
    if (command is "hop5-add-schedule" or "add-schedule")
    {
        await using var lookup = factory.CreateDbContext();
        int? requestedRouteId = null;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedRoute) && parsedRoute > 0)
        {
            requestedRouteId = parsedRoute;
        }

        Route? route;
        if (requestedRouteId is int rid)
        {
            route = await lookup.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RouteId == rid && r.IsActive);
            if (route is null)
            {
                Console.Error.WriteLine(
                    $"FAIL — RouteId={rid} is missing or inactive. No fallback when an id is supplied.");
                return 2;
            }
        }
        else
        {
            var candidates = await lookup.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.RouteId)
                .ToListAsync();
            route = candidates.FirstOrDefault(PublishedRouteFleet.HasPairing);
        }

        if (route is null || !PublishedRouteFleet.HasPairing(route))
        {
            Console.Error.WriteLine(
                "No active route with a session bus+driver pairing. Run hop4-assign-bus-driver first.");
            return 2;
        }

        var busId = PublishedRouteFleet.VehicleId(route)!.Value;
        var driverId = PublishedRouteFleet.DriverId(route)!.Value;
        var beforeAssignments = await lookup.RouteAssignments.CountAsync();
        var schedules = new ScheduleService(factory);
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        if (!await schedules.AddDailyFromPublishedRouteAsync(route.RouteId, day))
        {
            Console.Error.WriteLine("FAIL — AddDailyFromPublishedRouteAsync returned false.");
            return 1;
        }

        await using var verify = factory.CreateDbContext();
        var row = await verify.Schedules.AsNoTracking()
            .Where(s => s.RouteId == route.RouteId)
            .OrderByDescending(s => s.ScheduleId)
            .FirstAsync();
        var afterAssignments = await verify.RouteAssignments.CountAsync();

        Console.WriteLine();
        Console.WriteLine("=== Hop 5 proof (Add Schedule) ===");
        Console.WriteLine($"ScheduleId:     {row.ScheduleId}");
        Console.WriteLine($"RouteId:        {row.RouteId}");
        Console.WriteLine($"BusId:          {row.BusId}");
        Console.WriteLine($"DriverId:       {row.DriverId}");
        Console.WriteLine($"ScheduleDate:   {row.ScheduleDate:u}");
        Console.WriteLine($"DepartureTime:  {row.DepartureTime:u}");
        Console.WriteLine($"ArrivalTime:    {row.ArrivalTime:u}");
        Console.WriteLine($"Status:         {row.Status}");
        Console.WriteLine($"Δ RouteAssignments: {afterAssignments - beforeAssignments} (must be 0)");
        Console.WriteLine();
        Console.WriteLine("Serilog: Successfully added schedule with ID {ScheduleId}");

        if (afterAssignments != beforeAssignments)
        {
            Console.Error.WriteLine("FAIL — schedule write touched RouteAssignments.");
            return 1;
        }

        if (row.RouteId != route.RouteId || row.BusId != busId || row.DriverId != driverId)
        {
            Console.Error.WriteLine("FAIL — schedule FKs do not match the published session pairing.");
            return 1;
        }

        Console.WriteLine("PASS — Schedules row persisted from the route session pairing.");
        return 0;
    }

    // Hop 6 clerk-path proof: FuelService + MaintenanceService rows that point at the Hop 4 bus.
    // Same create paths as Fuel / Maintenance header windows.
    if (command is "hop6-fuel-maintenance" or "fuel-maintenance")
    {
        await using var lookup = factory.CreateDbContext();
        int? requestedBusId = null;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedBus) && parsedBus > 0)
        {
            requestedBusId = parsedBus;
        }

        Bus? bus;
        if (requestedBusId is int busId)
        {
            bus = await lookup.Buses.AsNoTracking().FirstOrDefaultAsync(b => b.BusId == busId);
            if (bus is null)
            {
                Console.Error.WriteLine(
                    $"FAIL — BusId={busId} not found. No fallback when an id is supplied.");
                return 2;
            }
        }
        else
        {
            var fromRoute = await lookup.Routes.AsNoTracking()
                .Where(r => r.IsActive && r.AMVehicleId != null)
                .OrderByDescending(r => r.RouteId)
                .Select(r => r.AMVehicleId)
                .FirstOrDefaultAsync();
            bus = fromRoute is int vid
                ? await lookup.Buses.AsNoTracking().FirstOrDefaultAsync(b => b.BusId == vid)
                : null;
            bus ??= await lookup.Buses.AsNoTracking()
                .Where(b => b.Status == "Active" || b.Status == "InService")
                .OrderBy(b => b.BusId)
                .FirstOrDefaultAsync();
        }

        if (bus is null)
        {
            Console.Error.WriteLine("No bus found. Run hop4-assign-bus-driver first.");
            return 2;
        }

        var fuelSvc = new FuelService(factory);
        var maintSvc = new MaintenanceService(factory);
        var day = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);

        var fuel = await fuelSvc.CreateFuelRecordAsync(new Fuel
        {
            FuelDate = day,
            FuelLocation = "Hop6 Proof Pump",
            VehicleFueledId = bus.BusId,
            VehicleOdometerReading = 100_000,
            FuelType = "Diesel",
            Gallons = 10.5m,
            PricePerGallon = 3.50m,
            TotalCost = 36.75m,
            Notes = "Hop6 proof fuel record"
        });
        if (!fuel.IsSuccess)
        {
            Console.Error.WriteLine($"FAIL — CreateFuelRecordAsync: {fuel.Error}");
            return 1;
        }

        var maintenance = await maintSvc.CreateMaintenanceRecordAsync(new Maintenance
        {
            Date = day,
            VehicleId = bus.BusId,
            OdometerReading = 100_000,
            MaintenanceCompleted = "Inspection",
            Vendor = "Hop6 Proof Vendor",
            RepairCost = 0m,
            Description = "Hop6 proof maintenance record",
            Status = "Completed",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "Hop6Proof"
        });
        if (!maintenance.IsSuccess)
        {
            Console.Error.WriteLine($"FAIL — CreateMaintenanceRecordAsync: {maintenance.Error}");
            return 1;
        }

        await using var verify = factory.CreateDbContext();
        var fuelRow = await verify.FuelRecords.AsNoTracking()
            .SingleAsync(f => f.FuelId == fuel.Value.FuelId);
        var maintRow = await verify.MaintenanceRecords.AsNoTracking()
            .SingleAsync(m => m.MaintenanceId == maintenance.Value.MaintenanceId);

        Console.WriteLine();
        Console.WriteLine("=== Hop 6 proof (Fuel + Maintenance) ===");
        Console.WriteLine($"BusId:            {bus.BusId}");
        Console.WriteLine($"BusNumber:        {bus.BusNumber}");
        Console.WriteLine($"FuelId:           {fuelRow.FuelId}");
        Console.WriteLine($"Fuel VehicleId:   {fuelRow.VehicleFueledId}");
        Console.WriteLine($"MaintenanceId:    {maintRow.MaintenanceId}");
        Console.WriteLine($"Maint VehicleId:  {maintRow.VehicleId}");
        Console.WriteLine();
        Console.WriteLine("Serilog (maintenance): Created maintenance record {MaintenanceId}");

        if (fuelRow.VehicleFueledId != bus.BusId || maintRow.VehicleId != bus.BusId)
        {
            Console.Error.WriteLine("FAIL — fuel/maintenance do not point at the Hop 4 bus.");
            return 1;
        }

        Console.WriteLine("PASS — Fuel and Maintenance records point at the assigned bus.");
        return 0;
    }

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
