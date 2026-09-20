using BusBuddy.Core.Services.GoogleMaps;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class AddressValidationPinPolicyTests
{
    [Test]
    public void Accept_PremiseAcceptComplete_IsOk()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "ACCEPT",
            "PREMISE",
            "PREMISE",
            addressComplete: true,
            dpvConfirmation: "Y",
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void Reject_OtherGranularityEvenWhenComplete()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "FIX",
            "OTHER",
            "OTHER",
            addressComplete: true,
            dpvConfirmation: null,
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "could not confirm this as a house");
    }

    [Test]
    public void Reject_PremiseAddressWithCityGeocode()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "ACCEPT",
            "PREMISE",
            "OTHER",
            addressComplete: true,
            dpvConfirmation: "Y",
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "did not place this at a building");
    }

    [Test]
    public void Reject_MissingUnit()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "CONFIRM_ADD_SUBPREMISES",
            "PREMISE",
            "PREMISE",
            addressComplete: true,
            dpvConfirmation: "D",
            missingComponentTypes: new[] { "subpremise" },
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "apartment");
    }

    [Test]
    public void Reject_PoBox()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "ACCEPT",
            "PREMISE",
            "PREMISE",
            addressComplete: true,
            dpvConfirmation: "Y",
            missingComponentTypes: null,
            isPoBox: true,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "PO Box");
    }

    [Test]
    public void ConfirmWithPremiseProximity_IsOk()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "CONFIRM",
            "PREMISE_PROXIMITY",
            "PREMISE_PROXIMITY",
            addressComplete: true,
            dpvConfirmation: null,
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: null,
            out var error);

        Assert.That(ok, Is.True, error);
    }

    [Test]
    public void Reject_UnconfirmedStreetEvenIfPremise()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "ACCEPT",
            "PREMISE",
            "PREMISE",
            addressComplete: true,
            dpvConfirmation: null,
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: new[] { "street_number", "route" },
            geocodePlaceTypes: new[] { "street_address" },
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "not a real deliverable address");
        Assert.That(error, Does.Contain("house number"));
        Assert.That(error, Does.Contain("Google suggestion"));
    }

    [Test]
    public void Reject_LocalityPlaceTypesEvenIfPremise()
    {
        var ok = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            "ACCEPT",
            "PREMISE",
            "PREMISE",
            addressComplete: true,
            dpvConfirmation: null,
            missingComponentTypes: null,
            isPoBox: false,
            unconfirmedComponentTypes: null,
            geocodePlaceTypes: new[] { "locality", "political" },
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "only found a city");
    }

    [Test]
    public void GeocodeFallback_RangeInterpolated_IsNotOk()
    {
        var ok = AddressValidationPinPolicy.TryAcceptGeocodeFallbackPin(
            "RANGE_INTERPOLATED",
            new[] { "street_address" },
            out var error);

        Assert.That(ok, Is.False);
        AssertClerkReject(error, "could not confirm this house");
    }

    [Test]
    public void GeocodeFallback_RooftopStreetAddress_IsOk()
    {
        var ok = AddressValidationPinPolicy.TryAcceptGeocodeFallbackPin(
            "ROOFTOP",
            new[] { "street_address" },
            out var error);

        Assert.That(ok, Is.True, error);
    }

    private static void AssertClerkReject(string error, string distinctive)
    {
        Assert.That(error, Does.StartWith("Rejected."));
        Assert.That(error, Does.Contain("No map pin."));
        Assert.That(error, Does.Contain("Validate Address"));
        Assert.That(error, Does.Contain(distinctive).IgnoreCase);
        Assert.That(AddressValidationPinPolicy.IsClerkRejectCopy(error), Is.True);
    }
}
