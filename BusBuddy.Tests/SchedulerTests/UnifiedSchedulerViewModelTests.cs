using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels;

namespace BusBuddy.Tests.SchedulerTests
{
    [TestFixture]
    [Category("Integration")]
    [Category("Scheduler")]
    public class UnifiedSchedulerViewModelTests
    {
        private DbContextOptions<BusBuddyDbContext> _options = null!;

        [SetUp]
        public void Setup()
        {
            // Use a unique in-memory database per test run
            _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: $"BusBuddyTests_{Guid.NewGuid()}")
                .Options;
        }

        private static void SeedBasicData(BusBuddyDbContext ctx)
        {
            // Seed minimal related entities
            var bus = new Bus { VehicleId = 1, BusNumber = "B1", VINNumber = "VIN00000000000001", LicenseNumber = "LIC1", Make = "Ford", Model = "E350", Year = 2018 };
            var driver = new Driver { DriverId = 1, DriverName = "Jane Driver" };
            var route = new Route { RouteId = 1, RouteName = "R1" };
            ctx.Buses.Add(bus);
            ctx.Drivers.Add(driver);
            ctx.Routes.Add(route);

            // ActivitySchedule sample
            ctx.ActivitySchedules.Add(new ActivitySchedule
            {
                ActivityScheduleId = 1,
                ScheduledDate = new DateTime(2025, 8, 12),
                TripType = "Activity Trip",
                ScheduledVehicleId = bus.VehicleId,
                ScheduledDestination = "Science Museum",
                ScheduledLeaveTime = new TimeSpan(9, 0, 0),
                ScheduledEventTime = new TimeSpan(11, 0, 0),
                ScheduledDriverId = driver.DriverId,
                RequestedBy = "Mr. Smith",
                Status = "Scheduled",
                CreatedDate = DateTime.UtcNow
            });

            // Sports schedule sample
            ctx.Schedules.Add(new Schedule
            {
                ScheduleId = 1,
                BusId = bus.VehicleId,
                RouteId = route.RouteId,
                DriverId = driver.DriverId,
                DepartureTime = new DateTime(2025, 8, 12, 15, 30, 0),
                ArrivalTime = new DateTime(2025, 8, 12, 19, 0, 0),
                ScheduleDate = new DateTime(2025, 8, 12),
                SportsCategory = "Volleyball",
                Opponent = "Rivals",
                Location = "Away - Rivals High School",
                DestinationTown = "Rivals Town"
            });

            ctx.SaveChanges();
        }

        [Test]
        public async Task LoadAppointmentsAsync_Merges_ActivitySchedule_And_Schedules()
        {
            using var ctx = new BusBuddyDbContext(_options);
            SeedBasicData(ctx);

            var vm = new UnifiedSchedulerViewModel(ctx);
            await vm.LoadAppointmentsAsync();

            vm.Appointments.Should().HaveCount(2);
            vm.Appointments.Any(a => a.Subject.Contains("Activity Trip", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            vm.Appointments.Any(a => a.Subject.Contains("Volleyball", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }

        [Test]
        public async Task LoadAppointmentsAsync_Sets_Location_And_Times()
        {
            using var ctx = new BusBuddyDbContext(_options);
            SeedBasicData(ctx);

            var vm = new UnifiedSchedulerViewModel(ctx);
            await vm.LoadAppointmentsAsync();

            // Find volleyball appt
            var sports = vm.Appointments.First(a => a.Subject.Contains("Volleyball"));
            sports.IsAllDay.Should().BeFalse();
            sports.Location.Should().Contain("Rivals");
            sports.EndTime.Should().BeAfter(sports.StartTime);

            // Find activity appt
            var activity = vm.Appointments.First(a => a.Subject.Contains("Activity Trip"));
            activity.Location.Should().Contain("Science Museum");
        }
    }
}
