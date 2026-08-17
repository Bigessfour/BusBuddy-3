using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class DriverAvailabilityCalculatorTests
{
    [Test]
    public void AvailableDates_SkipsDaysWithActiveSchedule()
    {
        var from = new DateTime(2026, 8, 17);
        var schedules = new[]
        {
            new Schedule { DriverId = 1, ScheduleDate = from.AddDays(1), Status = "Scheduled" },
            new Schedule { DriverId = 1, ScheduleDate = from.AddDays(2), Status = "Cancelled" },
            new Schedule { DriverId = 2, ScheduleDate = from, Status = "Scheduled" }
        };

        var available = DriverAvailabilityCalculator.AvailableDates(schedules, driverId: 1, from, dayCount: 4);

        Assert.That(available, Is.EqualTo(new[]
        {
            from,
            from.AddDays(2),
            from.AddDays(3)
        }));
    }

    [Test]
    public void AvailableDates_EmptySchedules_ReturnsFullWindow()
    {
        var from = new DateTime(2026, 8, 17);
        var available = DriverAvailabilityCalculator.AvailableDates(Array.Empty<Schedule>(), 9, from, 3);
        Assert.That(available, Has.Count.EqualTo(3));
    }

    [Test]
    public void AvailableDates_SkipsDaysWithActivityTrip()
    {
        var from = new DateTime(2026, 8, 17);
        var schedules = Array.Empty<Schedule>();
        var activities = new[]
        {
            new ActivitySchedule
            {
                ScheduledDriverId = 1,
                ScheduledDate = from.AddDays(1),
                Status = "Scheduled"
            },
            new ActivitySchedule
            {
                ScheduledDriverId = 1,
                ScheduledDate = from.AddDays(2),
                Status = "Cancelled"
            }
        };

        var available = DriverAvailabilityCalculator.AvailableDates(schedules, activities, driverId: 1, from, 4);

        Assert.That(available, Is.EqualTo(new[]
        {
            from,
            from.AddDays(2),
            from.AddDays(3)
        }));
    }
}
