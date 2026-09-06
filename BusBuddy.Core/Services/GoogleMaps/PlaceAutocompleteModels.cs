namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>One Places Autocomplete (New) suggestion row for WPF type-ahead.</summary>
public sealed class PlaceAutocompleteSuggestion
{
    public string PlaceId { get; init; } = string.Empty;

    /// <summary>Full line shown in the suggestion list (WPF autocomplete popup).</summary>
    public string DisplayText { get; init; } = string.Empty;

    public string PrimaryText { get; init; } = string.Empty;

    public string SecondaryText { get; init; } = string.Empty;
}

/// <summary>Normalized US address parts from Place Details (New).</summary>
public sealed class PlaceAddressDetails
{
    public string? StreetLine { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? Zip { get; init; }

    public string? FormattedAddress { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
