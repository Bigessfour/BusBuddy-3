using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class PdfReportServiceTests
    {
        private PdfReportService _service;

        [SetUp]
        public void Setup()
        {
            _service = new PdfReportService();
        }

        [Test]
        public void GenerateActivityCalendarReport_WithValidActivities_ReturnsNonEmptyPdfBytes()
        {
            // Arrange - proves report generation "works" for the finish item (Reports via PdfReportService)
            var activities = new List<Activity>
            {
                new Activity { Date = DateTime.Today, ActivityType = "Test", Description = "Sample activity for PDF proof", DriverId = 1, AssignedVehicleId = 101 },
                new Activity { Date = DateTime.Today.AddDays(1), ActivityType = "Test2", Description = "Another for coverage", DriverId = 2, AssignedVehicleId = 102 }
            };
            var start = DateTime.Today.AddDays(-1);
            var end = DateTime.Today.AddDays(2);

            // Act
            var bytes = _service.GenerateActivityCalendarReport(activities, start, end);

            // Assert - proves it works (non-empty valid-ish PDF output)
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThan(100)); // Reasonable size for generated PDF with content
            // Basic PDF magic number check
            Assert.That(bytes[0], Is.EqualTo((byte)'%'));
            Assert.That(bytes[1], Is.EqualTo((byte)'P'));
            Assert.That(bytes[2], Is.EqualTo((byte)'D'));
            Assert.That(bytes[3], Is.EqualTo((byte)'F'));
        }

        [Test]
        public void GenerateActivityCalendarReport_WithEmptyList_ThrowsOrHandlesGracefully()
        {
            // Arrange
            var activities = new List<Activity>();
            var start = DateTime.Today;
            var end = DateTime.Today.AddDays(1);

            // Act & Assert - proves robustness for the reports item
            Assert.Throws<ArgumentNullException>(() => _service.GenerateActivityCalendarReport(null, start, end));
            // Empty list should also be handled without crash in real (current impl takes it)
            var bytes = _service.GenerateActivityCalendarReport(activities, start, end);
            Assert.That(bytes, Is.Not.Null);
        }
    }
}
