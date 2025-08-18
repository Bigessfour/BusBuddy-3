using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Tests.ValidationTests
{
    [TestFixture]
    [Category("Unit")]
    public class StudentServiceValidationTests
    {
        private BusBuddyDbContext _context = null!;
        private StudentService _service = null!;
        private string? _origMode;
        private string? _origSkip;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly BusBuddyDbContext _ctx;
            public TestDbContextFactory(BusBuddyDbContext ctx) => _ctx = ctx;
            public BusBuddyDbContext CreateDbContext() => _ctx;
            public BusBuddyDbContext CreateWriteDbContext() => _ctx;
        }

        [SetUp]
        public void SetUp()
        {
            // Save original env to restore later
            _origMode = Environment.GetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE");
            _origSkip = Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION");

            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"BusBuddy_Unit_{Guid.NewGuid()}")
                .Options;
            _context = new BusBuddyDbContext(options);
            _service = new StudentService(new TestDbContextFactory(_context), null);
        }

        [TearDown]
        public void TearDown()
        {
            // Restore env
            if (_origMode is null) Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", null);
            else Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", _origMode);
            if (_origSkip is null) Environment.SetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION", null);
            else Environment.SetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION", _origSkip);

            _context.Dispose();
        }

        [Test]
        public async Task StrictMode_Blocks_InvalidPhone()
        {
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", "strict");
            Environment.SetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION", null);

            var s = new Student { StudentName = "Test Student", HomePhone = "15555555555" }; // 11 digits, no separator

            var errors = await _service.ValidateStudentAsync(s);
            errors.Should().Contain(e => e.Contains("Invalid home phone number", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public async Task WarnMode_Allows_InvalidPhone_NoError()
        {
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", "warn");
            Environment.SetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION", null);

            var s = new Student { StudentName = "Test Student", HomePhone = "555-abc-5555" }; // invalid pattern

            var errors = await _service.ValidateStudentAsync(s);
            errors.Should().NotContain(e => e.Contains("Invalid home phone number", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public async Task OffMode_Allows_InvalidPhone_NoError()
        {
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", "off");
            Environment.SetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION", null);

            var s = new Student { StudentName = "Test Student", EmergencyPhone = "abc" }; // invalid

            var errors = await _service.ValidateStudentAsync(s);
            errors.Should().NotContain(e => e.Contains("Invalid emergency phone number", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        [Category("Performance")]
        public async Task Add50Students_E2E_CompletesUnder2Seconds()
        {
            // Ensure validation won’t block on phones or other optional fields
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", "warn");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
            {
                var s = new Student
                {
                    StudentName = $"Student {i}",
                    StudentNumber = $"S{i:00000}",
                    Active = true
                };
                await _service.AddStudentAsync(s);
            }
            sw.Stop();

            // Verify count and timing
            (await _service.GetAllStudentsAsync()).Count.Should().BeGreaterOrEqualTo(50);
            sw.Elapsed.TotalSeconds.Should().BeLessThan(2.0, "Adding 50 students should meet MVP perf target");
        }

        [Test]
        public async Task Export_ReturnsHeaderAndRows()
        {
            // Seed a few students
            for (int i = 0; i < 3; i++)
            {
                await _service.AddStudentAsync(new Student { StudentName = $"S{i}", StudentNumber = $"N{i}" });
            }

            var csv = await _service.ExportStudentsToCsvAsync();
            csv.Should().NotBeNullOrEmpty();

            // Expect header + rows
            var lines = csv.TrimEnd('\r', '\n').Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            lines.Length.Should().BeGreaterOrEqualTo(1 + 3);
        }
    }
}
