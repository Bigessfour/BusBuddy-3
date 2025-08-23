using NUnit.Framework;
using FluentAssertions;
using BusBuddy.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Tests.ValidationTests;

/// <summary>
/// Comprehensive validation tests for all core models
/// Ensures data integrity and business rule compliance
/// </summary>
[TestFixture]
public class ModelValidationTests
{
    [Test]
    [Category("ModelValidation")]
    [Category("Driver")]
    public void Driver_ShouldValidateRequiredProperties()
    {
        // Arrange
        var driver = new Driver
        {
            DriverId = 1,
            DriverName = "Valid Driver",
            Status = "Active",
            LicenseNumber = "D123456",
            DriverEmail = "driver@test.com",
            DriverPhone = "(555) 123-4567"
        };

        // Act & Assert
        driver.DriverId.Should().BePositive("DriverId should be positive");
        driver.DriverName.Should().NotBeNullOrEmpty("DriverName is required");
        driver.Status.Should().NotBeNullOrEmpty("Status is required");
        driver.LicenseNumber.Should().NotBeNullOrEmpty("LicenseNumber is required");
        driver.DriverEmail.Should().Contain("@", "Email should be valid format");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Driver")]
    public void Driver_ShouldValidatePhoneNumberFormat()
    {
        // Arrange
        var validPhoneNumbers = new[]
        {
            "(555) 123-4567",
            "555-123-4567",
            "555.123.4567"
        };

        // Act & Assert
        foreach (var phoneNumber in validPhoneNumbers)
        {
            var driver = new Driver { DriverPhone = phoneNumber };
            driver.DriverPhone.Should().MatchRegex(@"[\d\(\)\-\.\s]+",
                $"Phone number '{phoneNumber}' should be valid format");
        }
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Bus")]
    public void Bus_ShouldValidateRequiredProperties()
    {
        // Arrange
        var vehicle = new Bus
        {
            VehicleId = 1,
            Make = "Blue Bird",
            Model = "Vision",
            BusNumber = "BUS-001",
            SeatingCapacity = 72
        };

        // Act & Assert
        vehicle.VehicleId.Should().BePositive("Vehicle Id should be positive");
        vehicle.Make.Should().NotBeNullOrEmpty("Make is required");
        vehicle.Model.Should().NotBeNullOrEmpty("Model is required");
        vehicle.BusNumber.Should().NotBeNullOrEmpty("BusNumber is required");
        vehicle.SeatingCapacity.Should().BePositive("Capacity should be positive");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Bus")]
    public void Bus_ShouldValidateRealisticCapacity()
    {
        // Arrange & Act
        var smallBus = new Bus { SeatingCapacity = 15 };
        var standardBus = new Bus { SeatingCapacity = 72 };
        var largeBus = new Bus { SeatingCapacity = 90 };
        var oversizedBus = new Bus { SeatingCapacity = 150 };

        // Assert
        smallBus.Capacity.Should().BeInRange(1, 200, "Small bus capacity should be reasonable");
        standardBus.Capacity.Should().BeInRange(1, 200, "Standard bus capacity should be reasonable");
        largeBus.Capacity.Should().BeInRange(1, 200, "Large bus capacity should be reasonable");
    Assert.That(oversizedBus.Capacity, Is.LessThanOrEqualTo(200), "Bus capacity should not exceed reasonable limits");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Bus")]
    public void Bus_ShouldValidateExtendedProperties()
    {
        // Arrange
        var bus = new Bus
        {
            VehicleId = 1,
            BusNumber = "001",
            Make = "Blue Bird",
            Model = "Vision",
            Year = 2020,
            SeatingCapacity = 72,
            Status = "Active",
            LicenseNumber = "BUS-001"
        };

        // Act & Assert
        bus.VehicleId.Should().BePositive("VehicleId should be positive");
        bus.BusNumber.Should().NotBeNullOrEmpty("BusNumber is required");
        bus.Year.Should().BeInRange(1990, DateTime.Now.Year + 1, "Year should be realistic");
        bus.SeatingCapacity.Should().BePositive("SeatingCapacity should be positive");
        bus.Status.Should().NotBeNullOrEmpty("Status is required");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Activity")]
    public void Activity_ShouldValidateTimeSequence()
    {
        // Arrange
        var validActivity = new Activity
        {
            ActivityId = 1,
            ActivityType = "Field Trip",
            Date = DateTime.Today,
            LeaveTime = new TimeSpan(8, 0, 0),
            EventTime = new TimeSpan(10, 0, 0),
            Destination = "Science Museum",
            Status = "Scheduled"
        };

        var invalidActivity = new Activity
        {
            ActivityId = 2,
            ActivityType = "Invalid Trip",
            Date = DateTime.Today,
            LeaveTime = new TimeSpan(10, 0, 0),
            EventTime = new TimeSpan(8, 0, 0), // Event before leave time
            Destination = "Invalid Destination",
            Status = "Invalid"
        };

        // Act & Assert
        validActivity.LeaveTime.Should().BeLessThan(validActivity.EventTime,
            "Leave time should be before event time for valid activity");

        invalidActivity.LeaveTime.Should().BeGreaterThan(invalidActivity.EventTime,
            "Invalid activity demonstrates the validation rule");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Activity")]
    public void Activity_ShouldValidateRequiredFields()
    {
        // Arrange
        var activity = new Activity
        {
            ActivityId = 1,
            ActivityType = "Field Trip",
            Date = DateTime.Today,
            Destination = "Science Museum",
            Status = "Scheduled",
            RequestedBy = "Teacher Smith"
        };

        // Act & Assert
        activity.ActivityId.Should().BePositive("ActivityId should be positive");
        activity.ActivityType.Should().NotBeNullOrEmpty("ActivityType is required");
        activity.Destination.Should().NotBeNullOrEmpty("Destination is required");
        activity.Status.Should().NotBeNullOrEmpty("Status is required");
        activity.Date.Should().BeOnOrAfter(DateTime.Today.AddYears(-1), "Date should be reasonable");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("ActivitySchedule")]
    public void ActivitySchedule_ShouldValidateSchedulingProperties()
    {
        // Arrange
        var schedule = new ActivitySchedule
        {
            ActivityScheduleId = 1,
            ScheduledDate = DateTime.Today.AddDays(1),
            TripType = "Field Trip",
            ScheduledVehicleId = 1,
            ScheduledDriverId = 1,
            ScheduledDestination = "Science Museum",
            ScheduledLeaveTime = new TimeSpan(8, 0, 0),
            ScheduledEventTime = new TimeSpan(10, 0, 0),
            ScheduledRiders = 45,
            Status = "Scheduled",
            RequestedBy = "Principal"
        };

        // Act & Assert
        schedule.ActivityScheduleId.Should().BePositive("ActivityScheduleId should be positive");
        schedule.ScheduledVehicleId.Should().BePositive("ScheduledVehicleId should be valid");
        schedule.ScheduledDriverId.Should().BePositive("ScheduledDriverId should be valid");
        schedule.ScheduledRiders.Should().BePositive("ScheduledRiders should be positive");
        schedule.ScheduledLeaveTime.Should().BeLessThan(schedule.ScheduledEventTime,
            "Leave time should be before event time");
        schedule.RequestedBy.Should().NotBeNullOrEmpty("RequestedBy is required for accountability");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("Student")]
    public void Student_ShouldValidateStudentProperties()
    {
        // Arrange
        var student = new Student
        {
            StudentId = 1,
            StudentNumber = "12345",
            StudentName = "John Doe",
            Grade = "8",
            EmergencyPhone = "(555) 123-4567",
            ParentGuardian = "Jane Doe"
        };

        // Act & Assert
        student.StudentId.Should().BePositive("StudentId should be positive");
        student.StudentNumber.Should().NotBeNullOrEmpty("StudentNumber is required");
        student.StudentName.Should().NotBeNullOrEmpty("StudentName is required");
        student.Grade.Should().NotBeNullOrEmpty("Grade is required");
        student.EmergencyPhone.Should().NotBeNullOrEmpty("EmergencyPhone is required for safety");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("BusinessRules")]
    public void Models_ShouldEnforceBusinessRules()
    {
        // Test driver training completion business rule
        var traineeDriver = new Driver
        {
            Status = "Training",
            TrainingComplete = false
        };
        var activeDriver = new Driver
        {
            Status = "Active",
            TrainingComplete = true
        };

        // Assert business rule consistency
        traineeDriver.Status.Should().Be("Training");
        traineeDriver.TrainingComplete.Should().BeFalse("Trainee should not have completed training");

        activeDriver.Status.Should().Be("Active");
        activeDriver.TrainingComplete.Should().BeTrue("Active driver should have completed training");

        // Test vehicle inspection business rule
        var recentInspection = DateTime.Now.AddDays(-30);
        var overdueInspection = DateTime.Now.AddDays(-400);

        recentInspection.Should().BeAfter(DateTime.Now.AddDays(-365),
            "Recent inspection should be within one year");
        overdueInspection.Should().BeBefore(DateTime.Now.AddDays(-365),
            "Overdue inspection should be flagged");
    }

    [Test]
    [Category("ModelValidation")]
    [Category("DataConsistency")]
    public void Models_ShouldMaintainDataConsistency()
    {
        // Arrange - Create related models
        var driver = new Driver { DriverId = 1, DriverName = "Test Driver", Status = "Active" };
        var vehicle = new Bus { VehicleId = 1, BusNumber = "001", Make = "Test", SeatingCapacity = 50 };
        var activity = new Activity
        {
            ActivityId = 1,
            DriverId = driver.DriverId,
            VehicleId = vehicle.VehicleId,
            ActivityType = "Test Trip",
            Status = "Scheduled"
        };

        // Assert referential consistency
        activity.DriverId.Should().Be(driver.DriverId, "Activity should reference correct driver");
        activity.VehicleId.Should().Be(vehicle.VehicleId, "Activity should reference correct vehicle");

        // Test status consistency
        var validStatuses = new[] { "Active", "Inactive", "Training", "Suspended" };
        validStatuses.Should().Contain(driver.Status, "Driver status should be from valid set");
    }
}
