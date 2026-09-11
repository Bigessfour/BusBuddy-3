using System;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class ScheduleTimestampNormalizerTests
{
    [Test]
    public void NormalizeForPersist_CoercesUnspecifiedToUtc()
    {
        var schedule = new Schedule
        {
            ScheduleDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Unspecified),
            DepartureTime = new DateTime(2026, 9, 11, 7, 0, 0, DateTimeKind.Unspecified),
            ArrivalTime = new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Unspecified),
            CreatedDate = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Unspecified),
            RouteId = 1,
            BusId = 1,
            DriverId = 1
        };

        ScheduleTimestampNormalizer.NormalizeForPersist(schedule);

        schedule.ScheduleDate.Kind.Should().Be(DateTimeKind.Utc);
        schedule.DepartureTime.Kind.Should().Be(DateTimeKind.Utc);
        schedule.ArrivalTime.Kind.Should().Be(DateTimeKind.Utc);
        schedule.DepartureTime.Hour.Should().Be(7);
    }

    [Test]
    public void AsUtcDate_StripsTimeComponent()
    {
        var value = new DateTime(2026, 9, 11, 15, 30, 0, DateTimeKind.Unspecified);
        var normalized = ScheduleTimestampNormalizer.AsUtcDate(value);
        normalized.Should().Be(new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
    }
}
