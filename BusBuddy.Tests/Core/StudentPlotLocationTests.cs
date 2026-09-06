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
}
