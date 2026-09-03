namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Google Places API (New) autocomplete for clerk address type-ahead (spec 007 US4).
/// No-op when <c>GOOGLE_MAPS_API_KEY</c> is missing.
/// </summary>
public interface IPlacesAutocompleteService
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<PlaceAutocompleteSuggestion>> GetSuggestionsAsync(
        string input,
        string? sessionToken = null,
        CancellationToken cancellationToken = default);

    Task<PlaceAddressDetails?> GetPlaceDetailsAsync(
        string placeId,
        string? sessionToken = null,
        CancellationToken cancellationToken = default);
}
