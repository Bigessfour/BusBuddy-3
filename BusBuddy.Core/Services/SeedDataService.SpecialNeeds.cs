using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
{
    public partial class SeedDataService
    {
        /// <inheritdoc />
        public async Task EnsureMapDemoGeoAsync()
        {
            try
            {
                await SeedSpecialNeedsTransportPrepAsync();

                using var context = _contextFactory.CreateWriteDbContext();

                var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteName == "Special Needs Route")
                    ?? await context.Routes.FirstOrDefaultAsync(r => r.IsActive);

                if (route is not null && string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    route.WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
                    {
                        (38.1535, -102.7195),
                        (38.1550, -102.7210),
                        (38.1565, -102.7180),
                        (38.1535, -102.7195)
                    });
                }

                await context.SaveChangesAsync();
                Logger.Information(
                    "Map demo geo ensured SchoolStudentsSeeded=true RouteHasWaypoints={HasWaypoints}",
                    route is not null && !string.IsNullOrWhiteSpace(route.WaypointsJson));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "EnsureMapDemoGeoAsync failed");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SpecialNeedsPrepSummary> SeedSpecialNeedsTransportPrepAsync()
        {
            const string schoolName = "Wiley K-12 School";
            const string routeName = "Special Needs Route";
            const string driverName = "Pat Special";
            const string busNumber = "BUS-SN01";
            var messages = new List<string>();
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            using var context = _contextFactory.CreateWriteDbContext();

            var school = await context.Destinations
                .FirstOrDefaultAsync(d =>
                    d.DestinationType == DestinationTypes.School &&
                    d.Name == schoolName);

            if (school is null)
            {
                school = new Destination
                {
                    Name = schoolName,
                    Address = "403 N Main St",
                    City = "Wiley",
                    State = "CO",
                    ZipCode = "81092",
                    DestinationType = DestinationTypes.School,
                    DistrictName = "Wiley School District RE-13",
                    Latitude = 38.1535m,
                    Longitude = -102.7195m,
                    StartTime = TimeSpan.FromHours(8),
                    DismissalTime = TimeSpan.FromHours(15) + TimeSpan.FromMinutes(30),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Destinations.Add(school);
                await context.SaveChangesAsync();
                messages.Add($"Created school destination '{schoolName}'");
            }
            else if (school.Latitude is null || school.Longitude is null)
            {
                school.Latitude = 38.1535m;
                school.Longitude = -102.7195m;
                school.StartTime ??= TimeSpan.FromHours(8);
                school.DismissalTime ??= TimeSpan.FromHours(15) + TimeSpan.FromMinutes(30);
                await context.SaveChangesAsync();
                messages.Add($"Updated GPS and bell times for '{schoolName}'");
            }

            var driver = await context.Drivers
                .FirstOrDefaultAsync(d => d.DriverName == driverName);
            if (driver is null)
            {
                driver = new Driver
                {
                    DriverName = driverName,
                    FirstName = "Pat",
                    LastName = "Special",
                    DriverPhone = "(719) 555-0142",
                    DriverEmail = "pat.special@wiley.k12.co.us",
                    DriversLicenceType = "CDL",
                    TrainingComplete = true,
                    Status = "Active",
                    HireDate = DateTime.UtcNow.AddYears(-4),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Drivers.Add(driver);
                await context.SaveChangesAsync();
                messages.Add($"Created special-needs driver '{driverName}'");
            }

            var bus = await context.Buses
                .FirstOrDefaultAsync(b => b.BusNumber == busNumber);
            if (bus is null)
            {
                bus = new Bus
                {
                    BusNumber = busNumber,
                    Year = 2021,
                    Make = "Thomas Built",
                    Model = "Saf-T-Liner HDX",
                    SeatingCapacity = 12,
                    FleetType = "Special Needs",
                    VINNumber = "1T8SNBUS21W000001",
                    LicenseNumber = "SN1001",
                    Status = "Active",
                    Description = "Wheelchair lift, tie-downs, aide seating",
                    DateLastInspection = DateTime.UtcNow.AddMonths(-2),
                    PurchaseDate = DateTime.SpecifyKind(new DateTime(2021, 6, 1), DateTimeKind.Utc),
                    PurchasePrice = 118000m,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Buses.Add(bus);
                await context.SaveChangesAsync();
                messages.Add($"Created special-needs bus '{busNumber}'");
            }
            else if (!string.Equals(bus.FleetType, "Special Needs", StringComparison.OrdinalIgnoreCase))
            {
                bus.FleetType = "Special Needs";
                bus.SeatingCapacity = Math.Min(bus.SeatingCapacity, 12) > 0 ? Math.Min(bus.SeatingCapacity, 12) : 12;
                await context.SaveChangesAsync();
                messages.Add($"Updated bus '{busNumber}' FleetType to Special Needs");
            }

            var route = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteName == routeName);
            if (route is null)
            {
                route = new Route
                {
                    RouteName = routeName,
                    Description = "Door-to-door special-needs transport to Wiley K-12",
                    School = schoolName,
                    Date = todayUtc,
                    IsActive = true,
                    IsSpecialNeedsRoute = true,
                    AMDriverId = driver.DriverId,
                    PMDriverId = driver.DriverId,
                    AMVehicleId = bus.BusId,
                    PMVehicleId = bus.BusId,
                    DriverName = driver.DriverName,
                    BusNumber = bus.BusNumber,
                    AMBeginTime = TimeSpan.FromHours(7) + TimeSpan.FromMinutes(15),
                    PMBeginTime = TimeSpan.FromHours(15) + TimeSpan.FromMinutes(45),
                    WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
                    {
                        (38.1535, -102.7195),
                        (38.1550, -102.7210),
                        (38.1565, -102.7180),
                        (38.1535, -102.7195)
                    })
                };
                context.Routes.Add(route);
                await context.SaveChangesAsync();
                messages.Add($"Created route '{routeName}'");
            }
            else
            {
                route.IsSpecialNeedsRoute = true;
                route.School = schoolName;
                route.IsActive = true;
                if (!route.AMDriverId.HasValue)
                {
                    route.AMDriverId = driver.DriverId;
                    route.DriverName = driver.DriverName;
                }
                if (!route.PMDriverId.HasValue)
                {
                    route.PMDriverId = driver.DriverId;
                }
                if (!route.AMVehicleId.HasValue)
                {
                    route.AMVehicleId = bus.BusId;
                    route.BusNumber = bus.BusNumber;
                }
                if (!route.PMVehicleId.HasValue)
                {
                    route.PMVehicleId = bus.BusId;
                }
                if (string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    route.WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
                    {
                        (38.1535, -102.7195),
                        (38.1550, -102.7210),
                        (38.1565, -102.7180),
                        (38.1535, -102.7195)
                    });
                }
                await context.SaveChangesAsync();
                messages.Add($"Updated route '{routeName}' special-needs flag without replacing assigned bus/driver");
            }

            const string regularRouteName = "North Elementary";
            var regularRoute = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteName == regularRouteName);
            if (regularRoute is null)
            {
                regularRoute = new Route
                {
                    RouteName = regularRouteName,
                    Description = "Regular home-to-school route — Wiley K-12",
                    School = schoolName,
                    Date = todayUtc,
                    IsActive = true,
                    IsSpecialNeedsRoute = false
                };
                context.Routes.Add(regularRoute);
                await context.SaveChangesAsync();
                messages.Add($"Created regular route '{regularRouteName}'");
            }

            // Synthetic tokens only — never anything that could be mistaken for a real child.
            // specs/students.md: student PII must not be committed to git.
            var specialStudentSpecs = new[]
            {
                new
                {
                    Name = "TEST_STUDENT_SN_01",
                    Guardian = "TEST_GUARDIAN_SN_01",
                    Grade = "5",
                    Address = "100 Test St",
                    City = "TESTVILLE",
                    Lat = 38.1512m,
                    Lon = -102.7210m,
                    Wheelchair = false,
                    Aide = true,
                    Notes = "TEST DATA: requires aide assistance boarding"
                },
                new
                {
                    Name = "TEST_STUDENT_SN_02",
                    Guardian = "TEST_GUARDIAN_SN_02",
                    Grade = "3",
                    Address = "200 Test St",
                    City = "TESTVILLE",
                    Lat = 38.1548m,
                    Lon = -102.7162m,
                    Wheelchair = true,
                    Aide = true,
                    Notes = "TEST DATA: wheelchair lift; secure tie-downs required"
                },
                new
                {
                    Name = "TEST_STUDENT_SN_03",
                    Guardian = "TEST_GUARDIAN_SN_03",
                    Grade = "7",
                    Address = "300 Test St",
                    City = "TESTVILLE",
                    Lat = 38.1485m,
                    Lon = -102.7248m,
                    Wheelchair = false,
                    Aide = false,
                    Notes = "TEST DATA: seat belt harness; monitor at drop-off"
                }
            };

            var snCount = 0;
            foreach (var spec in specialStudentSpecs)
            {
                var existing = await context.Students
                    .FirstOrDefaultAsync(s => s.StudentName == spec.Name);
                if (existing is null)
                {
                    existing = new Student
                    {
                        StudentName = spec.Name,
                        Grade = spec.Grade,
                        HomeAddress = spec.Address,
                        City = spec.City,
                        State = "CO",
                        Zip = "00000",
                        Latitude = spec.Lat,
                        Longitude = spec.Lon,
                        ParentGuardian = spec.Guardian,
                        CellPhone = "555-0100",
                        School = schoolName,
                        DestinationId = school.DestinationId,
                        RequiresSpecialNeedsBus = true,
                        RequiresWheelchair = spec.Wheelchair,
                        RequiresAide = spec.Aide,
                        RequiresSeatBelt = true,
                        HasMedicalNeeds = spec.Wheelchair,
                        TransportationNotes = spec.Notes,
                        // Assigned to both runs, so state eligibility for both — the model no longer
                        // assumes it and the grid/scheduler read these flags, not the route strings.
                        RidesAm = true,
                        RidesPm = true,
                        SchoolYear = StudentRecordNormalizer.CurrentSchoolYear(),
                        Active = true,
                        EnrollmentDate = todayUtc,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    };
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.AM, route);
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.PM, route);
                    StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(existing);
                    context.Students.Add(existing);
                    snCount++;
                }
                else
                {
                    existing.RequiresSpecialNeedsBus = true;
                    existing.RequiresWheelchair = spec.Wheelchair;
                    existing.RequiresAide = spec.Aide;
                    existing.RequiresSeatBelt = true;
                    existing.DestinationId = school.DestinationId;
                    existing.School = schoolName;
                    existing.Latitude ??= spec.Lat;
                    existing.Longitude ??= spec.Lon;
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.AM, route);
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.PM, route);
                    existing.RidesAm = true;
                    existing.RidesPm = true;
                    existing.TransportationNotes = spec.Notes;
                    StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(existing);
                    snCount++;
                }
            }

            await context.SaveChangesAsync();
            messages.Add($"Prepared {snCount} special-needs student(s) on '{routeName}'");

            // Synthetic tokens only — see note on specialStudentSpecs above.
            // Both rows deliberately share one address so sibling grouping stays exercised.
            var regularStudentSpecs = new[]
            {
                new { Name = "TEST_STUDENT_REG_01", Grade = "3", Address = "400 Test St", City = "TESTVILLE", Lat = 38.1555m, Lon = -102.7180m },
                new { Name = "TEST_STUDENT_REG_02", Grade = "1", Address = "400 Test St", City = "TESTVILLE", Lat = 38.1556m, Lon = -102.7181m }
            };

            var regCount = 0;
            foreach (var spec in regularStudentSpecs)
            {
                var existing = await context.Students
                    .FirstOrDefaultAsync(s => s.StudentName == spec.Name);
                if (existing is null)
                {
                    existing = new Student
                    {
                        StudentName = spec.Name,
                        Grade = spec.Grade,
                        HomeAddress = spec.Address,
                        City = spec.City,
                        State = "CO",
                        Zip = "00000",
                        Latitude = spec.Lat,
                        Longitude = spec.Lon,
                        ParentGuardian = "TEST_GUARDIAN_REG",
                        CellPhone = "555-0200",
                        School = schoolName,
                        DestinationId = school.DestinationId,
                        RidesAm = true,
                        RidesPm = true,
                        SchoolYear = StudentRecordNormalizer.CurrentSchoolYear(),
                        Active = true,
                        EnrollmentDate = todayUtc,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    };
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.AM, regularRoute);
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.PM, regularRoute);
                    context.Students.Add(existing);
                    regCount++;
                }
                else if (existing.RequiresSpecialNeedsBus)
                {
                    // leave SN students alone
                }
                else
                {
                    existing.DestinationId = school.DestinationId;
                    existing.School = schoolName;
                    existing.Latitude ??= spec.Lat;
                    existing.Longitude ??= spec.Lon;
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.AM, regularRoute);
                    StudentRouteAssignment.SetSlot(existing, RouteTimeSlot.PM, regularRoute);

                    // Assigned to both runs on this seed, so both are stated.
                    existing.RidesAm = true;
                    existing.RidesPm = true;
                    regCount++;
                }
            }

            await context.SaveChangesAsync();
            messages.Add($"Prepared {regCount} regular student(s) for contrast routing");

            route.StudentCount = await context.Students.WhereOnRoute(route).CountAsync();
            await context.SaveChangesAsync();

            Logger.Information(
                "Special-needs transport prep complete Route={RouteId} Students={SnCount}",
                route.RouteId, snCount);

            return new SpecialNeedsPrepSummary
            {
                SchoolDestinationId = school.DestinationId,
                SpecialNeedsRouteId = route.RouteId,
                SpecialNeedsRouteName = route.RouteName,
                SpecialNeedsDriverId = driver.DriverId,
                SpecialNeedsBusId = bus.BusId,
                SpecialNeedsStudentsPrepared = snCount,
                RegularStudentsPrepared = regCount,
                Messages = messages
            };
        }
    }
}
