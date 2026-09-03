using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class GooglePlacesAutocompleteServiceTests
{
    private static GoogleMapsOptions TestOptions => new()
    {
        ApiKey = "test-key",
        QuotaProject = "busbuddy-507301",
        AutocompleteBiasLatitude = 38.0872,
        AutocompleteBiasLongitude = -102.6208,
        AutocompleteBiasRadiusMeters = 80_000,
    };

    [Test]
    public async Task GetSuggestions_ReturnsPredictions()
    {
        var json = """
            {
              "suggestions": [{
                "placePrediction": {
                  "placeId": "ChIJ_test_place",
                  "text": { "text": "100 Main St, Wiley, CO 81092, USA" },
                  "structuredFormat": {
                    "mainText": { "text": "100 Main St" },
                    "secondaryText": { "text": "Wiley, CO 81092, USA" }
                  }
                }
              }]
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var svc = new GooglePlacesAutocompleteService(http, Options.Create(TestOptions));

        var suggestions = await svc.GetSuggestionsAsync("100 Main");

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].PlaceId, Is.EqualTo("ChIJ_test_place"));
        Assert.That(suggestions[0].DisplayText, Does.Contain("Wiley"));
    }

    [Test]
    public async Task GetSuggestions_MissingKey_ReturnsEmpty()
    {
        var previous = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
            using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
            var svc = new GooglePlacesAutocompleteService(
                http,
                Options.Create(new GoogleMapsOptions { ApiKey = "${GOOGLE_MAPS_API_KEY}" }));

            var suggestions = await svc.GetSuggestionsAsync("100 Main");

            Assert.That(suggestions, Is.Empty);
            Assert.That(svc.IsConfigured, Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", previous);
        }
    }

    [Test]
    public async Task GetPlaceDetails_ParsesAddressComponents()
    {
        var json = """
            {
              "formattedAddress": "100 Main St, Wiley, CO 81092, USA",
              "location": { "latitude": 38.0872, "longitude": -102.6208 },
              "addressComponents": [
                { "types": ["street_number"], "shortText": "100", "longText": "100" },
                { "types": ["route"], "shortText": "Main St", "longText": "Main Street" },
                { "types": ["locality"], "shortText": "Wiley", "longText": "Wiley" },
                { "types": ["administrative_area_level_1"], "shortText": "CO", "longText": "Colorado" },
                { "types": ["postal_code"], "shortText": "81092", "longText": "81092" }
              ]
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var svc = new GooglePlacesAutocompleteService(http, Options.Create(TestOptions));

        var details = await svc.GetPlaceDetailsAsync("places/ChIJ_test_place");

        Assert.That(details, Is.Not.Null);
        Assert.That(details!.StreetLine, Is.EqualTo("100 Main Street"));
        Assert.That(details.City, Is.EqualTo("Wiley"));
        Assert.That(details.State, Is.EqualTo("CO"));
        Assert.That(details.Zip, Is.EqualTo("81092"));
        Assert.That(details.Latitude, Is.EqualTo(38.0872).Within(0.0001));
    }

    [Test]
    public void NormalizePlaceId_StripsPlacesPrefix()
    {
        Assert.That(
            GooglePlacesAutocompleteService.NormalizePlaceId("places/ChIJabc"),
            Is.EqualTo("ChIJabc"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
