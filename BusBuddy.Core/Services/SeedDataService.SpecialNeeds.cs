using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
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
                Logger.Information(
                    "Map demo geo skipped invented waypoints. Drive paths come from active schools and assigned homes.");
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
            const string routeName = "Special Needs Route";
            const string driverName = "Pat Special";
            const string busNumber = "BUS-SN01";
            var messages = new List<string>();
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            using var context = _contextFactory.CreateWriteDbContext();

            // Do not invent a Wiley campus. The school catalog is clerk-entered.
            // A route path starts at an active destination or an assigned home.
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
                    DriverEmail = "pat.special@busbuddy.local",
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
                    Description = "Door-to-door special-needs transport",
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
                    PMBeginTime = TimeSpan.FromHours(15) + TimeSpan.FromMinutes(45)
                };
                context.Routes.Add(route);
                await context.SaveChangesAsync();
                messages.Add($"Created route '{routeName}' without a baked-in school");
            }
            else
            {
                route.IsSpecialNeedsRoute = true;
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
                await context.SaveChangesAsync();
                messages.Add($"Updated route '{routeName}' special-needs flag without replacing school, bus, or driver");
            }

            const string regularRouteName = "North Elementary";
            var regularRoute = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteName == regularRouteName);
            if (regularRoute is null)
            {
                regularRoute = new Route
                {
                    RouteName = regularRouteName,
                    Description = "Regular home-to-school route",
                    Date = todayUtc,
                    IsActive = true,
                    IsSpecialNeedsRoute = false
                };
                context.Routes.Add(regularRoute);
                await context.SaveChangesAsync();
                messages.Add($"Created regular route '{regularRouteName}'");
            }

            // specs/students.md: the roster is clerk-entered. Do not insert sample students here.
            // This method used to recreate TEST_STUDENT_* rows whenever they were missing, so a
            // clerk delete disappeared from the grid and came back on the next launch.
            messages.Add("Student roster was not seeded. Deleted students stay deleted.");

            route.StudentCount = await context.Students.WhereOnRoute(route).CountAsync();
            await context.SaveChangesAsync();

            Logger.Information(
                "Special-needs transport prep complete Route={RouteId} StudentsSeeded=0",
                route.RouteId);

            return new SpecialNeedsPrepSummary
            {
                SchoolDestinationId = 0,
                SpecialNeedsRouteId = route.RouteId,
                SpecialNeedsRouteName = route.RouteName,
                SpecialNeedsDriverId = driver.DriverId,
                SpecialNeedsBusId = bus.BusId,
                SpecialNeedsStudentsPrepared = 0,
                RegularStudentsPrepared = 0,
                Messages = messages
            };
        }
    }
}
