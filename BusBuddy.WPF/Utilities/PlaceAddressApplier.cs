using BusBuddy.Core.Services.GoogleMaps;

namespace BusBuddy.WPF.Utilities;

/// <summary>Maps Places details onto clerk address fields (student, school, etc.).</summary>
public static class PlaceAddressApplier
{
    public sealed record AppliedAddress(
        string? Street,
        string? City,
        string? State,
        string? Zip,
        double? Latitude,
        double? Longitude);

    public static AppliedAddress Apply(
        PlaceAutocompleteSuggestion suggestion,
        PlaceAddressDetails details)
    {
        var street = !string.IsNullOrWhiteSpace(details.StreetLine)
            ? details.StreetLine
            : string.IsNullOrWhiteSpace(suggestion.PrimaryText) ? null : suggestion.PrimaryText;

        return new AppliedAddress(
            street,
            details.City,
            details.State,
            details.Zip,
            details.Latitude,
            details.Longitude);
    }
}
