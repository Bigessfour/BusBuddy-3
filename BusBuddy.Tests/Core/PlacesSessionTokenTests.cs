using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class PlacesSessionTokenTests
{
    [Test]
    public void CreateSessionToken_IsUuidWithoutHyphens()
    {
        var token = PlacesAddressAutocompleteCoordinator.CreateSessionToken();
        Assert.That(token, Has.Length.EqualTo(32));
        Assert.That(token, Does.Not.Contain("-"));
    }

    [Test]
    public async Task Coordinator_SendsSameSessionToken_OnAutocompleteAndDetails()
    {
        var places = new RecordingPlacesService();
        var coordinator = new PlacesAddressAutocompleteCoordinator(places);

        await coordinator.RefreshSuggestionsAsync("100 Main").ConfigureAwait(false);
        Assert.That(places.AutocompleteTokens, Has.Count.EqualTo(1));
        var session = places.AutocompleteTokens[0];
        Assert.That(session, Is.Not.Null.And.Not.Empty);

        var suggestion = places.Suggestions[0];
        var applied = await coordinator.ApplySuggestionAsync(suggestion).ConfigureAwait(false);

        Assert.That(applied, Is.Not.Null);
        Assert.That(places.DetailsTokens, Has.Count.EqualTo(1));
        Assert.That(places.DetailsTokens[0], Is.EqualTo(session));

        // Session discarded after details — next refresh gets a new token.
        await coordinator.RefreshSuggestionsAsync("200 Oak").ConfigureAwait(false);
        Assert.That(places.AutocompleteTokens, Has.Count.EqualTo(2));
        Assert.That(places.AutocompleteTokens[1], Is.Not.EqualTo(session));
    }

    private sealed class RecordingPlacesService : IPlacesAutocompleteService
    {
        public bool IsConfigured => true;
        public List<string?> AutocompleteTokens { get; } = new();
        public List<string?> DetailsTokens { get; } = new();
        public IReadOnlyList<PlaceAutocompleteSuggestion> Suggestions { get; set; } =
            new[]
            {
                new PlaceAutocompleteSuggestion { PlaceId = "ChIJabc", DisplayText = "100 Main St" }
            };

        public Task<IReadOnlyList<PlaceAutocompleteSuggestion>> GetSuggestionsAsync(
            string input,
            string? sessionToken,
            CancellationToken cancellationToken = default)
        {
            AutocompleteTokens.Add(sessionToken);
            return Task.FromResult(Suggestions);
        }

        public Task<PlaceAddressDetails?> GetPlaceDetailsAsync(
            string placeId,
            string? sessionToken,
            CancellationToken cancellationToken = default)
        {
            DetailsTokens.Add(sessionToken);
            return Task.FromResult<PlaceAddressDetails?>(new PlaceAddressDetails
            {
                StreetLine = "100 Main St",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            });
        }
    }
}
