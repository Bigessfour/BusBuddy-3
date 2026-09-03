using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.Options;

namespace MapsConnectionProbe;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var key = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("FAIL: GOOGLE_MAPS_API_KEY is not set.");
            Console.Error.WriteLine("Set via macOS Passwords (Name=GOOGLE_MAPS_API_KEY) or export for this shell.");
            return 1;
        }

        var options = Options.Create(new GoogleMapsOptions
        {
            ApiKey = key,
            QuotaProject = Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                ?? "busbuddy-507301",
            RegionCode = "US",
            EnableUspsCass = true,
        });

        using var http = new HttpClient();
        using var addressClient = new GoogleAddressValidationClient(http, options, ownsHttpClient: false);
        using var routingClient = new GoogleRoutingService(http, options, ownsHttpClient: false);
        using var placesClient = new GooglePlacesAutocompleteService(http, options, ownsHttpClient: false);

        Console.WriteLine($"Maps probe starting QuotaProject={options.Value.QuotaProject}");

        var address = await addressClient.ValidateAndGeocodeAsync(
            "100 Main St",
            "Wiley",
            "CO",
            "81092");

        if (!address.Ok)
        {
            Console.Error.WriteLine($"FAIL: Address Validation — {address.ErrorMessage}");
            if (address.MappingUnconfigured)
            {
                Console.Error.WriteLine("Enable Address Validation API: https://developers.google.com/maps/documentation/address-validation");
            }

            return 2;
        }

        Console.WriteLine(
            $"OK: Address Validation lat={address.Latitude:F5} lon={address.Longitude:F5} precision={address.Precision}");

        var origin = (address.Latitude!.Value, address.Longitude!.Value);
        var destination = (37.0842, -102.7253);
        var path = await routingClient.ComputeDrivePathAsync(origin, destination, Array.Empty<(double, double)>());
        if (!path.Succeeded)
        {
            Console.Error.WriteLine($"FAIL: Routes API — {path.Error}");
            Console.Error.WriteLine("Enable Routes API: https://developers.google.com/maps/documentation/routes");
            return 3;
        }

        Console.WriteLine(
            $"OK: Routes API distance={path.DistanceMeters}m duration={path.Duration} points={path.Points.Count}");

        var suggestions = await placesClient.GetSuggestionsAsync("100 Main St Wiley CO", sessionToken: Guid.NewGuid().ToString());
        if (suggestions.Count == 0)
        {
            Console.Error.WriteLine("FAIL: Places Autocomplete — no suggestions returned for test query.");
            Console.Error.WriteLine("Enable Places API (New): https://developers.google.com/maps/documentation/places/web-service/overview");
            return 4;
        }

        Console.WriteLine($"OK: Places Autocomplete suggestions={suggestions.Count} first={suggestions[0].DisplayText}");

        var matrix = await routingClient.ComputeRouteMatrixAsync(
            origin,
            new[] { destination, (38.0872, -102.6208) });
        if (matrix.Count == 0)
        {
            Console.Error.WriteLine("FAIL: Routes API computeRouteMatrix — no elements returned.");
            return 5;
        }

        Console.WriteLine($"OK: Route matrix elements={matrix.Count} firstDistance={matrix[0].DistanceMeters}m");
        Console.WriteLine("Maps probe passed.");
        return 0;
    }
}
