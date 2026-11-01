using System;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DutyEventValidatorTests
{
    private static DutyEvent ValidDraft() =>
        new()
        {
            DriverId = 1,
            BusId = 2,
            Session = DutySession.AM,
            EventType = DutyEventType.PreTrip,
            EventedAt = DateTime.UtcNow
        };

    [Test]
    public void ValidateForPersist_RequiresDriverAndBus()
    {
        var noDriver = ValidDraft();
        noDriver.DriverId = 0;
        Action act = () => DutyEventValidator.ValidateForPersist(noDriver);
        act.Should().Throw<ArgumentException>().WithMessage("*driver*");

        var noBus = ValidDraft();
        noBus.BusId = 0;
        act = () => DutyEventValidator.ValidateForPersist(noBus);
        act.Should().Throw<ArgumentException>().WithMessage("*bus*");
    }

    [Test]
    public void ValidateForPersist_DefectRequiresNotes()
    {
        var defect = ValidDraft();
        defect.EventType = DutyEventType.Defect;
        defect.Notes = " ";
        Action act = () => DutyEventValidator.ValidateForPersist(defect);
        act.Should().Throw<ArgumentException>().WithMessage("*notes*");

        defect.Notes = "Brake light out";
        act = () => DutyEventValidator.ValidateForPersist(defect);
        act.Should().NotThrow();
    }

    [Test]
    public void ValidateForPersist_ClearsFuelIdUnlessFuelType()
    {
        var preTrip = ValidDraft();
        preTrip.FuelId = 9;
        DutyEventValidator.ValidateForPersist(preTrip);
        preTrip.FuelId.Should().BeNull();

        var fuel = ValidDraft();
        fuel.EventType = DutyEventType.Fuel;
        fuel.FuelId = 9;
        DutyEventValidator.ValidateForPersist(fuel);
        fuel.FuelId.Should().Be(9);
    }

    [Test]
    public void ValidateForPersist_NormalizesEventedAtToUtc()
    {
        var draft = ValidDraft();
        draft.EventedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Unspecified);
        DutyEventValidator.ValidateForPersist(draft);
        draft.EventedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
