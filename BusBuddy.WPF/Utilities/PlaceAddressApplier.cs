using System.Linq;
using BusBuddy.Core.Services.GoogleMaps;

namespace BusBuddy.WPF.Utilities;

/// <summary>Maps Places details onto clerk address fields (student, school, depot, driver, stops).</summary>
public static class PlaceAddressApplier
{
    public sealed record AppliedAddress(
        string? Street,
        string? City,
        string? State,
        string? Zip,
        double? Latitude,
        double? Longitude,
        string? FormattedAddress,
        string? PlaceId)
    {
        public string SingleLine()
        {
            if (!string.IsNullOrWhiteSpace(FormattedAddress))
            {
                return FormattedAddress;
            }

            return string.Join(
                ", ",
                new[] { Street, City, State, Zip }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

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
            details.Longitude,
            details.FormattedAddress,
            suggestion.PlaceId);
    }
}
