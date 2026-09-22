namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Student-home pin gate from Google's Address Validation + Geocoding docs.
/// Address quality and pin quality are separate signals.
/// </summary>
/// <remarks>
/// Risk-averse checkout flow:
/// https://developers.google.com/maps/documentation/address-validation/build-validation-logic
/// Verdict fields:
/// https://developers.google.com/maps/documentation/address-validation/understand-response
/// USPS DPV:
/// https://developers.google.com/maps/documentation/address-validation/handle-us-address
/// Geocoding granularity (v4):
/// https://developers.google.com/maps/documentation/geocoding/reference/rest/v4/GeocodeResult.Granularity
/// Geocoding does not prove an address exists:
/// https://developers.google.com/maps/architecture/geocoding-address-validation
/// </remarks>
public static class AddressValidationPinPolicy
{
    /// <summary>
    /// Address Validation <c>validationGranularity</c> that means a mailing destination at building
    /// or unit level. Google: PREMISE or SUB_PREMISE is likely deliverable; ROUTE/OTHER is not.
    /// PREMISE_PROXIMITY approximates the building (rural homes).
    /// </summary>
    public static bool IsDeliverableAddressGranularity(string? validationGranularity) =>
        EqualsAny(validationGranularity, "SUB_PREMISE", "PREMISE", "PREMISE_PROXIMITY");

    /// <summary>
    /// Address Validation <c>geocodeGranularity</c> for the lat/lng point itself. Google notes this
    /// can be coarser than validationGranularity (unit confirmed, pin at the building).
    /// ROUTE/OTHER is a street or city point — not a student-home pin.
    /// </summary>
    public static bool IsBuildingGeocodeGranularity(string? geocodeGranularity) =>
        EqualsAny(geocodeGranularity, "SUB_PREMISE", "PREMISE", "PREMISE_PROXIMITY");

    /// <summary>
    /// Geocoding API v4 <c>granularity</c>. ROOFTOP is a real plot. RANGE_INTERPOLATED places a
    /// number halfway between known endpoints and does not prove the address exists.
    /// </summary>
    public static bool IsRooftopGeocodeGranularity(string? geocodeGranularity) =>
        EqualsAny(geocodeGranularity, "ROOFTOP");

    /// <summary>
    /// Clerk copy starts with "Rejected." and names a next click. Do not prefix it again in the form.
    /// </summary>
    public static bool IsClerkRejectCopy(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.StartsWith("Rejected.", StringComparison.OrdinalIgnoreCase);

    public static bool TryAcceptAddressValidationPin(
        string? possibleNextAction,
        string? validationGranularity,
        string? geocodeGranularity,
        bool addressComplete,
        string? dpvConfirmation,
        IReadOnlyList<string>? missingComponentTypes,
        bool isPoBox,
        IReadOnlyList<string>? unconfirmedComponentTypes,
        IReadOnlyList<string>? geocodePlaceTypes,
        out string error)
    {
        if (isPoBox)
        {
            error = ClerkReject(
                "A PO Box is not a bus pickup.",
                "Enter the physical home street, then click Validate Address.");
            return false;
        }

        if (!addressComplete)
        {
            error = ClerkReject(
                "Street, city, or ZIP is missing or unresolved.",
                "Fill those fields, then click Validate Address.");
            return false;
        }

        if (HasUnconfirmedStreet(unconfirmedComponentTypes))
        {
            error = ClerkReject(
                "Google does not recognize this street or house number. It is not a real deliverable address.",
                NextFixStreetAndValidate);
            return false;
        }

        if (IsLocalityOnlyGeocode(geocodePlaceTypes))
        {
            error = ClerkReject(
                "Google only found a city, not a house.",
                NextFixStreetAndValidate);
            return false;
        }

        if (EqualsAny(dpvConfirmation, "N"))
        {
            error = ClerkReject(
                "USPS does not know that house number on this street.",
                "Check the house number, then click Validate Address.");
            return false;
        }

        if (EqualsAny(possibleNextAction, "FIX")
            || EqualsAny(validationGranularity, "OTHER", "ROUTE", "BLOCK", "GRANULARITY_UNSPECIFIED"))
        {
            error = ClerkReject(
                "Google could not confirm this as a house.",
                NextFixStreetAndValidate);
            return false;
        }

        if (EqualsAny(possibleNextAction, "CONFIRM_ADD_SUBPREMISES")
            || EqualsAny(dpvConfirmation, "D")
            || MissingOnlySubpremise(missingComponentTypes))
        {
            error = ClerkReject(
                "Google found the building but needs an apartment or unit.",
                "Add Apt/Unit, then click Validate Address.");
            return false;
        }

        if (!IsDeliverableAddressGranularity(validationGranularity))
        {
            error = ClerkReject(
                "Google did not confirm this as a building.",
                NextFixStreetAndValidate);
            return false;
        }

        if (!IsBuildingGeocodeGranularity(geocodeGranularity))
        {
            error = ClerkReject(
                "Google did not place this at a building.",
                NextFixStreetAndValidate);
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryAcceptGeocodeFallbackPin(
        string? granularity,
        IReadOnlyList<string>? resultTypes,
        out string error)
    {
        if (!IsRooftopGeocodeGranularity(granularity))
        {
            error = ClerkReject(
                "Address Validation could not confirm this house.",
                NextFixStreetAndValidate);
            return false;
        }

        if (resultTypes is null || resultTypes.Count == 0)
        {
            error = ClerkReject(
                "Google did not return a place type for this match.",
                NextFixStreetAndValidate);
            return false;
        }

        if (!resultTypes.Any(t => EqualsAny(t, "street_address", "premise", "subpremise")))
        {
            error = ClerkReject(
                "Google matched a city or area, not a house.",
                NextFixStreetAndValidate);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private const string NextFixStreetAndValidate =
        "Correct the house number and street name, or pick a Google suggestion, then click Validate Address.";

    private static string ClerkReject(string reason, string nextStep) =>
        $"Rejected. {reason} No map pin. {nextStep}";

    private static bool HasUnconfirmedStreet(IReadOnlyList<string>? unconfirmed) =>
        unconfirmed is { Count: > 0 }
        && unconfirmed.Any(t => EqualsAny(t, "street_number", "route"));

    /// <summary>
    /// Address Validation <c>geocode.placeTypes</c> of only locality/political is the city centroid
    /// a city-level result, not a student-home pin.
    /// </summary>
    private static bool IsLocalityOnlyGeocode(IReadOnlyList<string>? placeTypes)
    {
        if (placeTypes is null || placeTypes.Count == 0)
        {
            return false;
        }

        if (placeTypes.Any(t => EqualsAny(t, "street_address", "premise", "subpremise", "route")))
        {
            return false;
        }

        return placeTypes.Any(t => EqualsAny(t, "locality", "political", "administrative_area_level_1", "postal_code"));
    }

    private static bool MissingOnlySubpremise(IReadOnlyList<string>? missing) =>
        missing is { Count: > 0 }
        && missing.Any(t => EqualsAny(t, "subpremise"))
        && missing.All(t => EqualsAny(t, "subpremise"));

    private static bool EqualsAny(string? value, params string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var item in allowed)
        {
            if (value.Equals(item, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
