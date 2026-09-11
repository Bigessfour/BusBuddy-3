using System;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class MaintenanceRecordValidatorTests
{
    [Test]
    public void ValidateForPersist_NormalizesDateToUtcMidnight()
    {
        var record = ValidRecord();
        record.Date = new DateTime(2026, 9, 11, 15, 30, 0);

        MaintenanceRecordValidator.ValidateForPersist(record);

        record.Date.Should().Be(new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
        record.Date.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void ValidateForPersist_RejectsEmptyWorkAndVendor()
    {
        var record = ValidRecord();
        record.MaintenanceCompleted = "  ";
        Action act = () => MaintenanceRecordValidator.ValidateForPersist(record);
        act.Should().Throw<ArgumentException>().WithMessage("*Work*");

        record = ValidRecord();
        record.Vendor = "";
        act = () => MaintenanceRecordValidator.ValidateForPersist(record);
        act.Should().Throw<ArgumentException>().WithMessage("*Vendor*");
    }

    [Test]
    public void ValidateForPersist_CanonicalizesStatusAndPriority()
    {
        var record = ValidRecord();
        record.Status = "in progress";
        record.Priority = "high";

        MaintenanceRecordValidator.ValidateForPersist(record);

        record.Status.Should().Be("In Progress");
        record.Priority.Should().Be("High");
    }

    [Test]
    public void GetWorkError_RequiresNonEmpty()
    {
        MaintenanceRecordValidator.GetWorkError("").Should().NotBeNullOrEmpty();
        MaintenanceRecordValidator.GetWorkError("Oil change").Should().BeNull();
    }

    private static Maintenance ValidRecord() => new()
    {
        Date = DateTime.UtcNow.Date,
        VehicleId = 1,
        OdometerReading = 1000,
        MaintenanceCompleted = "Oil change",
        Vendor = "Shop",
        RepairCost = 50m,
        Status = "Scheduled",
        Priority = "Normal"
    };
}
