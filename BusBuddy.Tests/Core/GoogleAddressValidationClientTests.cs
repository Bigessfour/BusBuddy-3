using System;
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
public class GoogleAddressValidationClientTests
{
    private static GoogleMapsOptions TestOptions => new()
    {
        ApiKey = "test-key",
        QuotaProject = "busbuddy-507301",
        EnableUspsCass = true,
        RegionCode = "US"
    };

    [Test]
    public async Task ValidateAndGeocode_Deliverable_ReturnsCoords()
    {
        var json = """
            {
              "result": {
                "verdict": { "addressComplete": true, "validationGranularity": "PREMISE", "geocodeGranularity": "PREMISE", "possibleNextAction": "ACCEPT" },
                "address": { "formattedAddress": "100 Main St, Oakridge, CO 80000, USA" },
                "geocode": {
                  "placeId": "ChIJtestplace",
                  "location": { "latitude": 38.1527, "longitude": -102.7204 }
                }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Oakridge", "CO", "80000");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Latitude, Is.EqualTo(38.1527).Within(0.0001));
        Assert.That(result.Longitude, Is.EqualTo(-102.7204).Within(0.0001));
        Assert.That(result.PlaceId, Is.EqualTo("ChIJtestplace"));
        Assert.That(result.FormattedAddress, Does.Contain("Oakridge"));
    }

    [Test]
    public async Task ValidateAndGeocode_Undeliverable_ReturnsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": { "addressComplete": false, "validationGranularity": "OTHER" },
                "address": { "formattedAddress": "Nowhere" }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("999 Fake Rd", "Nowhere", "CO", "00000");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(await client.GeocodeAsync("999 Fake Rd", "Nowhere", "CO", "00000"), Is.Null);
    }

    [Test]
    public async Task ValidateAndGeocode_OtherGranularityWithCoords_IsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": {
                  "addressComplete": true,
                  "validationGranularity": "OTHER",
                  "geocodeGranularity": "OTHER",
                  "possibleNextAction": "FIX"
                },
                "address": { "formattedAddress": "Lamar, CO 81052, USA" },
                "geocode": {
                  "placeId": "ChIJcitycenter",
                  "location": { "latitude": 38.0872, "longitude": -102.6208 }
                }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("12200 BenVerified Ave", "Lamar", "CO", "81052");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(result.Precision, Is.EqualTo("OTHER"));
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
        Assert.That(result.ErrorMessage, Does.Contain("No map pin."));
        Assert.That(await client.GeocodeAsync("12200 BenVerified Ave", "Lamar", "CO", "81052"), Is.Null);
    }

    [Test]
    public async Task ValidateAndGeocode_PremiseAddressWithOtherGeocode_IsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": {
                  "addressComplete": true,
                  "validationGranularity": "PREMISE",
                  "geocodeGranularity": "OTHER",
                  "possibleNextAction": "ACCEPT"
                },
                "address": { "formattedAddress": "Lamar, CO 81052, USA" },
                "geocode": {
                  "placeId": "ChIJcitycenter",
                  "location": { "latitude": 38.0872, "longitude": -102.6208 }
                }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("12200 BenVerified Ave", "Lamar", "CO", "81052");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
        Assert.That(result.ErrorMessage, Does.Contain("did not place this at a building"));
    }

    [Test]
    public async Task ValidateAndGeocode_UspsDpvN_IsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": {
                  "addressComplete": true,
                  "validationGranularity": "PREMISE",
                  "geocodeGranularity": "PREMISE",
                  "possibleNextAction": "FIX"
                },
                "address": { "formattedAddress": "12200 BenVerified Ave, Lamar, CO 81052, USA" },
                "geocode": {
                  "placeId": "ChIJx",
                  "location": { "latitude": 38.0872, "longitude": -102.6208 }
                },
                "uspsData": { "dpvConfirmation": "N" }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("12200 BenVerified Ave", "Lamar", "CO", "81052");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
    }

    [Test]
    public async Task ValidateAndGeocode_ConfirmAddSubpremises_IsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": {
                  "addressComplete": true,
                  "validationGranularity": "PREMISE",
                  "geocodeGranularity": "PREMISE",
                  "possibleNextAction": "CONFIRM_ADD_SUBPREMISES"
                },
                "address": {
                  "formattedAddress": "100 Main St, Lamar, CO 81052, USA",
                  "missingComponentTypes": ["subpremise"]
                },
                "geocode": {
                  "placeId": "ChIJbuilding",
                  "location": { "latitude": 38.0872, "longitude": -102.6207 }
                },
                "uspsData": { "dpvConfirmation": "D" }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Lamar", "CO", "81052");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("apartment"));
    }

    [Test]
    public async Task ValidateAndGeocode_LiveBenVerifiedShape_IsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": {
                  "inputGranularity": "PREMISE",
                  "validationGranularity": "OTHER",
                  "geocodeGranularity": "OTHER",
                  "addressComplete": true,
                  "hasUnconfirmedComponents": true,
                  "possibleNextAction": "FIX"
                },
                "address": {
                  "formattedAddress": "12200 BenVerified Ave, Lamar, CO 81052, USA",
                  "unconfirmedComponentTypes": ["street_number", "route"]
                },
                "geocode": {
                  "placeId": "ChIJcitycenter",
                  "placeTypes": ["locality", "political"],
                  "location": { "latitude": 38.0872307, "longitude": -102.6207496 }
                }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("12200 BenVerified Ave", "Lamar", "CO", "81052");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(result.Precision, Is.EqualTo("OTHER"));
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
        Assert.That(result.ErrorMessage, Does.Contain("No map pin."));
        Assert.That(result.ErrorMessage, Does.Contain("not a real deliverable address"));
        Assert.That(result.ErrorMessage, Does.Contain("Validate Address"));
    }

    [Test]
    public void ParseGeocodeJson_ApproximateWithCoords_IsNotOk()
    {
        var result = GoogleAddressValidationClient.ParseGeocodeJson(
            """{"results":[{"placeId":"ChIJapprox","formattedAddress":"Lamar, CO","location":{"latitude":38.0872,"longitude":-102.6208},"granularity":"APPROXIMATE"}]}""",
            1);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(result.Precision, Is.EqualTo("APPROXIMATE"));
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
    }

    [Test]
    public async Task ValidateAndGeocode_MissingKey_ReturnsMappingUnconfigured()
    {
        var previous = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
            using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
            var opts = new GoogleMapsOptions { ApiKey = "${GOOGLE_MAPS_API_KEY}" };
            var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(opts));

            var result = await client.ValidateAndGeocodeAsync("100 Main St", "Oakridge", "CO", "80000");

            Assert.That(result.MappingUnconfigured, Is.True);
            Assert.That(result.Ok, Is.False);
            Assert.That(await client.GeocodeAsync("100 Main St", "Oakridge", "CO", "80000"), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", previous);
        }
    }

    [Test]
    public async Task ValidateAndGeocode_Forbidden_FallsBackToGeocoding()
    {
        // Geocoding API v4 GeocodeAddressResponse shape (camelCase, location.latitude/longitude, granularity).
        var geocodeJson = """
            {
              "results": [{
                "placeId": "ChIJgeocode",
                "formattedAddress": "1600 Amphitheatre Parkway, Mountain View, CA 94043, USA",
                "location": { "latitude": 37.422, "longitude": -122.084 },
                "granularity": "ROOFTOP",
                "types": ["street_address"]
              }]
            }
            """;
        HttpRequestMessage? geocodeRequest = null;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, "{\"error\":{\"status\":\"PERMISSION_DENIED\"}}"),
            (HttpStatusCode.OK, geocodeJson),
            onRequest: req =>
            {
                if (req.RequestUri?.AbsolutePath.Contains("geocode", StringComparison.OrdinalIgnoreCase) == true)
                {
                    geocodeRequest = req;
                }
            }));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync(
            "1600 Amphitheatre Parkway", "Mountain View", "CA", "94043");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.Precision, Is.EqualTo("ROOFTOP"));
        Assert.That(result.PlaceId, Is.EqualTo("ChIJgeocode"));
        Assert.That(result.Latitude, Is.EqualTo(37.422).Within(0.001));
        Assert.That(result.Longitude, Is.EqualTo(-122.084).Within(0.001));
        Assert.That(result.FormattedAddress, Does.Contain("Mountain View"));
        Assert.That(geocodeRequest, Is.Not.Null);
        // Documented v4 endpoint + header auth (the legacy maps/api/geocode/json only documents ?key=).
        Assert.That(geocodeRequest!.RequestUri!.Host, Is.EqualTo("geocode.googleapis.com"));
        Assert.That(geocodeRequest.RequestUri.AbsolutePath, Does.StartWith("/v4/geocode/address/"));
        Assert.That(geocodeRequest.RequestUri.Query, Does.Not.Contain("key="));
        Assert.That(geocodeRequest.RequestUri.Query, Does.Contain("regionCode=US"));
        Assert.That(geocodeRequest.Headers.Contains("X-Goog-Api-Key"), Is.True);
        Assert.That(geocodeRequest.Headers.Contains("X-Goog-FieldMask"), Is.True);
    }

    [Test]
    public void BuildGeocodeV4Uri_EscapesAddressAndDefaultsRegion()
    {
        var uri = GoogleAddressValidationClient.BuildGeocodeV4Uri("100 Main St, Wiley, CO 81092", null);

        Assert.That(uri.Host, Is.EqualTo("geocode.googleapis.com"));
        Assert.That(uri.AbsolutePath, Is.EqualTo("/v4/geocode/address/100%20Main%20St%2C%20Wiley%2C%20CO%2081092"));
        Assert.That(uri.Query, Is.EqualTo("?regionCode=US"));
    }

    [Test]
    public void ParseGeocodeJson_V4EmptyResults_IsNoMatch()
    {
        var result = GoogleAddressValidationClient.ParseGeocodeJson("""{"results":[]}""", 1);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("No geocode match"));
    }

    [Test]
    public void ParseGeocodeJson_V4MissingLocation_ReportsMissingCoordinates()
    {
        var result = GoogleAddressValidationClient.ParseGeocodeJson(
            """{"results":[{"placeId":"ChIJx","formattedAddress":"Somewhere","granularity":"APPROXIMATE"}]}""",
            1);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.PlaceId, Is.EqualTo("ChIJx"));
        Assert.That(result.Precision, Is.EqualTo("APPROXIMATE"));
        Assert.That(result.ErrorMessage, Does.Contain("missing coordinates"));
    }

    [Test]
    public void ParseGeocodeJson_RooftopLocality_IsNotOk()
    {
        var result = GoogleAddressValidationClient.ParseGeocodeJson(
            """{"results":[{"placeId":"ChIJcity","formattedAddress":"Lamar, CO","location":{"latitude":38.0872,"longitude":-102.6208},"granularity":"ROOFTOP","types":["locality","political"]}]}""",
            1);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.ErrorMessage, Does.StartWith("Rejected."));
    }

    [Test]
    public void ParseGeocodeJson_RooftopStreetAddress_IsOk()
    {
        var result = GoogleAddressValidationClient.ParseGeocodeJson(
            """{"results":[{"placeId":"ChIJhouse","formattedAddress":"100 Main St","location":{"latitude":38.0872,"longitude":-102.6207},"granularity":"ROOFTOP","types":["street_address"]}]}""",
            1);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Latitude, Is.EqualTo(38.0872).Within(0.0001));
    }

    [Test]
    public void DescribeGeocodeFailure_MapsV4HttpStatuses()
    {
        var avForbidden = GoogleAddressValidationClient.ClassifyMapsForbidden(
            """{"error":{"status":"PERMISSION_DENIED","details":[{"reason":"SERVICE_DISABLED"}]}}""");
        var geoDenied = GoogleAddressValidationClient.ClassifyMapsForbidden(
            """{"error":{"status":"PERMISSION_DENIED"}}""");

        Assert.That(
            GoogleAddressValidationClient.DescribeGeocodeFailure(HttpStatusCode.NotFound, geoDenied, avForbidden),
            Does.Contain("No geocode match"));
        Assert.That(
            GoogleAddressValidationClient.DescribeGeocodeFailure((HttpStatusCode)429, geoDenied, avForbidden),
            Does.Contain("rate limited"));
        // Generic PERMISSION_DENIED on Geocoding → report the more specific Address Validation cause.
        Assert.That(
            GoogleAddressValidationClient.DescribeGeocodeFailure(HttpStatusCode.Forbidden, geoDenied, avForbidden),
            Does.Contain("not enabled"));

        var geoDisabled = GoogleAddressValidationClient.ClassifyMapsForbidden(
            """{"error":{"status":"PERMISSION_DENIED","details":[{"reason":"SERVICE_DISABLED"}]}}""");
        Assert.That(
            GoogleAddressValidationClient.DescribeGeocodeFailure(HttpStatusCode.Forbidden, geoDisabled, avForbidden),
            Does.Contain("Geocoding API is not enabled"));
    }

    [Test]
    public async Task ValidateAndGeocode_ForbiddenApiNotEnabled_DoesNotMarkMappingUnconfigured()
    {
        var avJson = """
            {
              "error": {
                "code": 403,
                "message": "Address Validation API has not been used in project busbuddy-507301 before or it is disabled.",
                "status": "PERMISSION_DENIED",
                "details": [{ "reason": "SERVICE_DISABLED" }]
              }
            }
            """;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, avJson),
            (HttpStatusCode.Forbidden, "denied")));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Wiley", "CO", "81092");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("not enabled"));
    }

    [Test]
    public async Task ValidateAndGeocode_ForbiddenQuotaProject_DoesNotMarkMappingUnconfigured()
    {
        var avJson = """
            {
              "error": {
                "code": 403,
                "message": "Permission denied on the caller project's quota.",
                "status": "PERMISSION_DENIED",
                "details": [{ "reason": "USER_PROJECT_DENIED" }]
              }
            }
            """;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, avJson),
            (HttpStatusCode.Forbidden, "denied")));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Wiley", "CO", "81092");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("quota project"));
        Assert.That(result.ErrorMessage, Does.Contain("busbuddy-507301"));
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private sealed class SequenceStubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;
        private readonly Action<HttpRequestMessage>? _onRequest;

        public SequenceStubHandler(
            params (HttpStatusCode Status, string Body)[] responses)
            : this(null, responses)
        {
        }

        public SequenceStubHandler(
            Action<HttpRequestMessage>? onRequest,
            params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
            _onRequest = onRequest;
        }

        // Convenience overload used by the geocode-header assertion test.
        public SequenceStubHandler(
            (HttpStatusCode Status, string Body) first,
            (HttpStatusCode Status, string Body) second,
            Action<HttpRequestMessage>? onRequest)
            : this(onRequest, first, second)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);
            var (status, body) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }
}
