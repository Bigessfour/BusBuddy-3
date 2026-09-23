using System;
using System.Linq;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class StudentHomePinViewModelTests
{
    [Test]
    public void DistinctValidatedAndPickup_ShowsGoldAndBluePins()
    {
        var student = new Student
        {
            StudentId = 7,
            Latitude = 38.140000m,
            Longitude = -102.730000m
        };

        var vm = new StudentHomePinViewModel(student, validatedAddress: (38.141500, -102.728000));

        Assert.That(vm.HasValidatedAddressPin, Is.True);
        Assert.That(vm.HasMapPick, Is.True);
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Home), Is.True);
        Assert.That(vm.LatitudeValue, Is.EqualTo(38.14).Within(0.000001));
        Assert.That(vm.LongitudeValue, Is.EqualTo(-102.73).Within(0.000001));
    }

    [Test]
    public void ClickMovesPickupOnly_ValidatedPinStays()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };
        var vm = new StudentHomePinViewModel(student, validatedAddress: (38.14, -102.73));
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1), "same spot merges to one pin");

        vm.ApplyMapClick(38.1412, -102.7311);

        Assert.That(vm.LatitudeValue, Is.EqualTo(38.1412).Within(0.000001));
        Assert.That(vm.LongitudeValue, Is.EqualTo(-102.7311).Within(0.000001));
        Assert.That(vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.EqualTo(1));
        Assert.That(vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Home), Is.EqualTo(1));
        var home = vm.MapMarkers.Single(m => m.Kind == MapMarkerLabels.Kind.Home);
        Assert.That(home.LatitudeDegrees, Is.EqualTo(38.1412).Within(0.000001));
        var google = vm.MapMarkers.Single(m => m.Kind == MapMarkerLabels.Kind.Waypoint);
        Assert.That(google.LatitudeDegrees, Is.EqualTo(38.14).Within(0.000001));
    }

    [Test]
    public void CatalogStop_AddsOrangePinWithoutMovingPickup()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };
        var stop = new PickupStop
        {
            PickupStopId = 3,
            Name = "Oak & 4th",
            Latitude = 38.16m,
            Longitude = -102.71m
        };

        var vm = new StudentHomePinViewModel(student, validatedAddress: (38.14, -102.73), catalogStop: stop);

        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);
        Assert.That(vm.LatitudeValue, Is.EqualTo(38.14).Within(0.000001));
    }

    [Test]
    public void ApplyMapClick_RejectsZeroZeroAndUsCentroid()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };
        var vm = new StudentHomePinViewModel(student);

        vm.ApplyMapClick(0, 0);
        Assert.That(vm.LatitudeValue, Is.EqualTo(38.14).Within(0.000001));
        Assert.That(vm.LongitudeValue, Is.EqualTo(-102.73).Within(0.000001));

        vm.ApplyMapClick(39.8283, -98.5795);
        Assert.That(vm.LatitudeValue, Is.EqualTo(38.14).Within(0.000001));
        Assert.That(vm.MapMarkers.All(m => m.Kind != MapMarkerLabels.Kind.Home ||
            Math.Abs(m.LatitudeDegrees - 38.14) < 0.0001), Is.True);
    }

    [Test]
    public void StoredHome_CameraOpensOnThePickup_NotTheOcean()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };
        var vm = new StudentHomePinViewModel(student, validatedAddress: (38.14, -102.73));

        Assert.That(vm.MapCenter.X, Is.EqualTo(38.14).Within(0.000001));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.73).Within(0.000001));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(16));
        Assert.That(vm.PersistClerkAdjustment, Is.False);
    }

    [Test]
    public void ClickAwayFromValidatedAddress_PersistsClerkAdjustment()
    {
        var student = new Student { Latitude = 38.14m, Longitude = -102.73m };
        var vm = new StudentHomePinViewModel(student, validatedAddress: (38.14, -102.73));

        vm.ApplyMapClick(38.1412, -102.7311);

        Assert.That(vm.PersistClerkAdjustment, Is.True);
        Assert.That(vm.MapCenter.X, Is.EqualTo(38.14).Within(0.000001), "a click moves the pin, not the opening camera");
    }
}
