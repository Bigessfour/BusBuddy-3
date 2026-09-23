using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentPlotLocationTests
{
    [Test]
    public void TryFromStored_PrefersCatalogPickupOverHome()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak", Latitude = 38.16m, Longitude = -102.71m }
        ]);
        var student = new Student
        {
            PickupStopId = 7,
            Latitude = 38.0m,
            Longitude = -102.0m
        };

        var point = StudentPlotLocation.TryFromStored(student, pickups);

        Assert.That(point, Is.Not.Null);
        Assert.That(point!.Value.AtPickup, Is.True);
        Assert.That(point.Value.Latitude, Is.EqualTo(38.16).Within(0.0001));
        Assert.That(point.Value.Longitude, Is.EqualTo(-102.71).Within(0.0001));
        Assert.That(point.Value.PickupName, Is.EqualTo("Oak"));
    }

    [Test]
    public void PinsFromStored_IncludesPickupAndHomeWhenBothExist()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak", Latitude = 38.16m, Longitude = -102.71m }
        ]);
        var student = new Student
        {
            PickupStopId = 7,
            Latitude = 38.0m,
            Longitude = -102.0m
        };

        var pins = StudentPlotLocation.PinsFromStored(student, pickups);

        Assert.That(pins, Has.Count.EqualTo(2));
        Assert.That(pins[0].AtPickup, Is.True);
        Assert.That(pins[1].AtPickup, Is.False);
        Assert.That(pins[1].Latitude, Is.EqualTo(38.0).Within(0.0001));
    }

    [Test]
    public void PinsFromStored_SpecialNeeds_PlotsHomeNotCatalogStop()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak", Latitude = 38.16m, Longitude = -102.71m }
        ]);
        var student = new Student
        {
            RequiresSpecialNeedsBus = true,
            PickupStopId = 7,
            Latitude = 38.08m,
            Longitude = -102.62m
        };

        var pins = StudentPlotLocation.PinsFromStored(student, pickups);

        Assert.That(pins, Has.Count.EqualTo(1));
        Assert.That(pins[0].AtPickup, Is.False);
        Assert.That(pins[0].Latitude, Is.EqualTo(38.08).Within(0.0001));
        Assert.That(pins[0].Longitude, Is.EqualTo(-102.62).Within(0.0001));
    }

    [Test]
    public void PinsFromStored_SkipsDuplicateHomeWhenSameAsPickup()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak", Latitude = 38.16m, Longitude = -102.71m }
        ]);
        var student = new Student
        {
            PickupStopId = 7,
            Latitude = 38.16m,
            Longitude = -102.71m
        };

        Assert.That(StudentPlotLocation.PinsFromStored(student, pickups), Has.Count.EqualTo(1));
    }

    [Test]
    public void TryFromStored_UsesHomeWhenNoPickup()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };

        var point = StudentPlotLocation.TryFromStored(student, new Dictionary<int, PickupStop>());

        Assert.That(point, Is.Not.Null);
        Assert.That(point!.Value.AtPickup, Is.False);
        Assert.That(point.Value.Latitude, Is.EqualTo(38.14).Within(0.0001));
        Assert.That(point.Value.PickupName, Is.Null);
    }

    [Test]
    public void TryFromStored_ReturnsNullWhenNothingMapped()
    {
        var student = new Student { PickupStopId = 99, HomeAddress = "1 Main" };

        Assert.That(StudentPlotLocation.TryFromStored(student, new Dictionary<int, PickupStop>()), Is.Null);
    }

    [Test]
    public void Index_MapsStopsById()
    {
        var indexed = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 3, Name = "Elm", Latitude = 1m, Longitude = 2m }
        ]);

        Assert.That(indexed[3].Name, Is.EqualTo("Elm"));
        Assert.That(StudentPlotLocation.Index(null), Is.Empty);
    }

    [Test]
    public void PinsFromStored_PickupWithoutCoords_PlotsHomeOnly()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak", Latitude = 0m, Longitude = 0m }
        ]);
        var student = new Student
        {
            PickupStopId = 7,
            Latitude = 38.14m,
            Longitude = -102.73m
        };

        var pins = StudentPlotLocation.PinsFromStored(student, pickups);

        Assert.That(pins, Has.Count.EqualTo(1));
        Assert.That(pins[0].AtPickup, Is.False);
        Assert.That(pins[0].Latitude, Is.EqualTo(38.14).Within(0.0001));
    }

    [Test]
    public void PinsFromStored_PickupWithoutCoordsAndNoHome_IsEmpty()
    {
        var pickups = StudentPlotLocation.Index(
        [
            new PickupStop { PickupStopId = 7, Name = "Oak" }
        ]);
        var student = new Student { PickupStopId = 7, HomeAddress = "1 Main" };

        Assert.That(StudentPlotLocation.PinsFromStored(student, pickups), Is.Empty);
    }

    [Test]
    public void PinsFromStored_UsesNavigationPickupWhenCatalogMissing()
    {
        var student = new Student
        {
            PickupStopId = 7,
            PickupStop = new PickupStop
            {
                PickupStopId = 7,
                Name = "Oak",
                Latitude = 38.16m,
                Longitude = -102.71m
            }
        };

        var pins = StudentPlotLocation.PinsFromStored(student, new Dictionary<int, PickupStop>());

        Assert.That(pins, Has.Count.EqualTo(1));
        Assert.That(pins[0].AtPickup, Is.True);
        Assert.That(pins[0].PickupName, Is.EqualTo("Oak"));
    }

    [Test]
    public void PickupStop_HasGpsCoordinates_RejectsUnsetOrigin()
    {
        Assert.That(new PickupStop().HasGpsCoordinates, Is.False);
        Assert.That(
            new PickupStop { Latitude = 38.16m, Longitude = -102.71m }.HasGpsCoordinates,
            Is.True);
    }
}
