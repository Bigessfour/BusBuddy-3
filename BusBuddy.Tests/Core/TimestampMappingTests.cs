using System;
using System.Linq;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Guards the timestamp contract established on 2026-09-16.
    ///
    /// Every timestamp column is <c>timestamp with time zone</c>. Npgsql refuses to write a DateTime whose
    /// Kind is Local or Unspecified to that type, and DateTime.Today / DateTime.Now are both Local, so the
    /// context labels every DateTime Kind=Utc without shifting the wall clock. Re-enabling
    /// <c>Npgsql.EnableLegacyTimestampBehavior</c> or reintroducing a timezone conversion would put the
    /// runtime model back at odds with the schema (blocking all migrations) and reintroduce the six-hour
    /// read skew that made a 07:00 stop read back as 01:00.
    /// </summary>
    [TestFixture]
    public class TimestampMappingTests : IDisposable
    {
        private BusBuddyDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"timestamps-{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context = new BusBuddyDbContext(options);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        [Test]
        public void ConfigureNpgsqlAppContext_DoesNotEnableLegacyTimestampBehavior()
        {
            EntityFrameworkPostgresExtensions.ConfigureNpgsqlAppContext();

            var enabled = AppContext.TryGetSwitch("Npgsql.EnableLegacyTimestampBehavior", out var value) && value;

            Assert.That(enabled, Is.False,
                "Npgsql.EnableLegacyTimestampBehavior must stay off: it flips the default DateTime mapping to "
                + "'timestamp without time zone', which contradicts every column in the schema and blocks migrations.");
        }

        [Test]
        public void EveryDateTimeProperty_HasAKindConverter()
        {
            var unconverted = _context.Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties())
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                .Where(p => p.GetValueConverter() is null)
                .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
                .ToList();

            Assert.That(unconverted, Is.Empty,
                "These DateTime properties have no Kind converter, so a Local or Unspecified value would throw "
                + "when written to a timestamptz column: " + string.Join(", ", unconverted));
        }

        [TestCase(DateTimeKind.Unspecified)]
        [TestCase(DateTimeKind.Local)]
        [TestCase(DateTimeKind.Utc)]
        public void StopTime_RoundTripsAsUtcWithoutShiftingTheWallClock(DateTimeKind kind)
        {
            var sevenAm = DateTime.SpecifyKind(new DateTime(2026, 9, 20, 7, 0, 0), kind);

            var route = new Route
            {
                RouteName = $"TS {kind}",
                Date = DateTime.SpecifyKind(new DateTime(2026, 9, 20), DateTimeKind.Utc),
                IsActive = true,
                School = "Timestamp School",
            };
            _context.Routes.Add(route);
            _context.SaveChanges();

            _context.RouteStops.Add(new RouteStop
            {
                RouteId = route.RouteId,
                StopName = "Probe",
                StopOrder = 1,
                EstimatedArrivalTime = sevenAm,
                EstimatedDepartureTime = sevenAm.AddMinutes(2),
            });
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            var saved = _context.RouteStops.AsNoTracking().Single(s => s.StopName == "Probe");

            Assert.That(saved.EstimatedArrivalTime.ToString("yyyy-MM-dd HH:mm"), Is.EqualTo("2026-09-20 07:00"),
                "A 07:00 stop must stay 07:00. Shifting it by the machine offset is the bug that made stop "
                + "times read back as 01:00.");
            Assert.That(saved.EstimatedArrivalTime.Kind, Is.EqualTo(DateTimeKind.Utc),
                "Npgsql only accepts Kind=Utc for a timestamptz column.");
        }

        [Test]
        public void NormalizeRouteDate_LocalPickerValue_KeepsTheCalendarDay()
        {
            // SfDatePicker hands back Kind=Local. Converting to UTC used to land a clerk in a
            // positive-offset zone on the previous day.
            var picked = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Local);

            var normalized = Route.NormalizeRouteDate(picked);

            Assert.That(normalized.Date, Is.EqualTo(new DateTime(2026, 9, 20)));
            Assert.That(normalized.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
