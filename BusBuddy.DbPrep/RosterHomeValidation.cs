using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusBuddy.DbPrep;

/// <summary>
/// Validates imported general-ed homes with Address Validation. When the typed house number
/// is not deliverable, the first Places suggestion is validated and stored instead.
/// Counts only — no names or street lines on the console.
/// </summary>
internal static class RosterHomeValidation
{
    public static async Task<int> RunAsync(BusBuddyDbContextFactory factory)
    {
        var mapsOptions = new GoogleMapsOptions
        {
            EnableUspsCass = true,
            RegionCode = "US",
        };
        mapsOptions.Normalize();
        var options = Options.Create(mapsOptions);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var addressClient = new GoogleAddressValidationClient(http, options);
        using var places = new GooglePlacesAutocompleteService(http, options);

        if (string.IsNullOrWhiteSpace(addressClient.ResolvedApiKey) || !places.IsConfigured)
        {
            Console.Error.WriteLine("GOOGLE_MAPS_API_KEY is not set. Places suggestions and Address Validation are off.");
            return 2;
        }

        Console.WriteLine("Places suggestions: enabled");
        Console.WriteLine("Address Validation: enabled");

        List<PendingHome> pending;
        await using (var read = factory.CreateDbContext())
        {
            pending = await read.Students.AsNoTracking()
                .Where(s => s.Active && !s.RequiresSpecialNeedsBus && (s.Latitude == null || s.Longitude == null))
                .OrderBy(s => s.StudentId)
                .Select(s => new PendingHome(
                    s.StudentId,
                    s.HomeAddress ?? string.Empty,
                    s.City ?? string.Empty,
                    s.State ?? string.Empty,
                    s.Zip ?? string.Empty))
                .ToListAsync();
        }

        Console.WriteLine($"Homes awaiting a pin: {pending.Count}");
        var students = new StudentService(factory);
        var accepted = 0;
        var viaSuggestion = 0;
        var rejected = 0;

        foreach (var home in pending)
        {
            var (result, usedSuggestion) = await ResolveAsync(addressClient, places, home);
            if (result.MappingUnconfigured)
            {
                Console.Error.WriteLine("Address Validation reported that mapping is not configured.");
                return 2;
            }

            if (!TryAccept(result, home, out var street, out var city, out var state, out var zip, out var latitude, out var longitude))
            {
                rejected++;
                continue;
            }

            var addressWrite = await students.UpdateStudentAddressAsync(home.StudentId, street, city, state, zip);
            if (!addressWrite.IsSuccess || addressWrite.Value != true)
            {
                rejected++;
                continue;
            }

            var pinned = await students.UpdateHomeGeocodeAsync(home.StudentId, latitude, longitude, result.PlaceId);
            if (!pinned)
            {
                rejected++;
                continue;
            }

            accepted++;
            if (usedSuggestion)
            {
                viaSuggestion++;
            }

            if ((accepted + rejected) % 25 == 0)
            {
                Console.WriteLine($"Progress accepted={accepted} rejected={rejected}");
            }
        }

        await using var verify = factory.CreateDbContext();
        var stillUnpinned = await verify.Students.CountAsync(s =>
            s.Active && !s.RequiresSpecialNeedsBus && (s.Latitude == null || s.Longitude == null));
        var pinnedGeneral = await verify.Students.CountAsync(s =>
            s.Active && !s.RequiresSpecialNeedsBus && s.Latitude != null && s.Longitude != null);

        Console.WriteLine();
        Console.WriteLine("=== Roster home validation (PII-free) ===");
        Console.WriteLine($"Accepted pins:              {accepted}");
        Console.WriteLine($"Of those, Places suggestion: {viaSuggestion}");
        Console.WriteLine($"Rejected (no map pin):      {rejected}");
        Console.WriteLine($"General-ed homes with a pin: {pinnedGeneral}");
        Console.WriteLine($"General-ed homes still awaiting geocode: {stillUnpinned}");
        return stillUnpinned == 0 ? 0 : 1;
    }

    private static async Task<(MapsGeocodeResult Result, bool UsedSuggestion)> ResolveAsync(
        GoogleAddressValidationClient addressClient,
        GooglePlacesAutocompleteService places,
        PendingHome home)
    {
        var direct = await addressClient.ValidateAndGeocodeAsync(home.Street, home.City, home.State, home.Zip);
        if (direct.Ok || direct.MappingUnconfigured)
        {
            return (direct, false);
        }

        var query = $"{home.Street}, {home.City}, {home.State} {home.Zip}";
        var suggestions = await places.GetSuggestionsAsync(query);
        var first = suggestions.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.PlaceId));
        if (first is null)
        {
            return (direct, false);
        }

        var details = await places.GetPlaceDetailsAsync(first.PlaceId);
        if (details is null || string.IsNullOrWhiteSpace(details.StreetLine))
        {
            return (direct, false);
        }

        var suggested = await addressClient.ValidateAndGeocodeAsync(
            details.StreetLine,
            string.IsNullOrWhiteSpace(details.City) ? home.City : details.City,
            string.IsNullOrWhiteSpace(details.State) ? home.State : details.State,
            string.IsNullOrWhiteSpace(details.Zip) ? home.Zip : details.Zip);
        return (suggested, true);
    }

    private static bool TryAccept(
        MapsGeocodeResult result,
        PendingHome home,
        out string street,
        out string city,
        out string state,
        out string zip,
        out decimal latitude,
        out decimal longitude)
    {
        street = string.Empty;
        city = string.Empty;
        state = string.Empty;
        zip = string.Empty;
        latitude = 0;
        longitude = 0;

        if (!result.Ok
            || result.Latitude is not double lat
            || result.Longitude is not double lon
            || !LocationCoordinate.IsPlotPrecision(result.Precision)
            || string.IsNullOrWhiteSpace(result.Street))
        {
            return false;
        }

        var normalizedState = NormalizeState(result.State, home.State);
        var normalizedZip = NormalizeZip(result.Zip, home.Zip);
        if (normalizedState.Length != 2 || string.IsNullOrWhiteSpace(normalizedZip) || result.Street.Length > 200)
        {
            return false;
        }

        street = result.Street.Trim();
        city = string.IsNullOrWhiteSpace(result.City) ? home.City.Trim() : result.City.Trim();
        state = normalizedState;
        zip = normalizedZip;
        latitude = (decimal)lat;
        longitude = (decimal)lon;
        return !string.IsNullOrWhiteSpace(city);
    }

    private static string NormalizeState(string? candidate, string fallback)
    {
        var text = (candidate ?? string.Empty).Trim();
        if (text.Length == 2)
        {
            return text.ToUpperInvariant();
        }

        var original = (fallback ?? string.Empty).Trim();
        return original.Length == 2 ? original.ToUpperInvariant() : string.Empty;
    }

    private static string NormalizeZip(string? candidate, string fallback)
    {
        var fromCandidate = FirstZip(candidate);
        if (fromCandidate is not null)
        {
            return fromCandidate;
        }

        return FirstZip(fallback) ?? string.Empty;
    }

    private static string? FirstZip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(c => char.IsDigit(c) || c == '-').ToArray());
        if (digits.Length >= 10 && digits[5] == '-')
        {
            return digits[..10];
        }

        if (digits.Length >= 5)
        {
            return digits[..5];
        }

        return null;
    }

    private sealed record PendingHome(int StudentId, string Street, string City, string State, string Zip);
}
