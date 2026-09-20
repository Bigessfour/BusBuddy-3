using System.Text.Json;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Parses Place Details <c>addressComponents</c> into clerk form fields.</summary>
internal static class PlaceAddressComponentParser
{
    public static PlaceAddressDetails Parse(JsonElement root)
    {
        string? formatted = null;
        if (root.TryGetProperty("formattedAddress", out var fa) && fa.ValueKind == JsonValueKind.String)
        {
            formatted = fa.GetString();
        }

        double? lat = null;
        double? lon = null;
        if (root.TryGetProperty("location", out var loc))
        {
            if (loc.TryGetProperty("latitude", out var latEl) && latEl.TryGetDouble(out var latVal))
            {
                lat = latVal;
            }

            if (loc.TryGetProperty("longitude", out var lonEl) && lonEl.TryGetDouble(out var lonVal))
            {
                lon = lonVal;
            }
        }

        string? streetNumber = null;
        string? route = null;
        string? city = null;
        string? state = null;
        string? zip = null;
        string? streetLine = null;

        if (!root.TryGetProperty("addressComponents", out var components) ||
            components.ValueKind != JsonValueKind.Array)
        {
            PlaceAddressFill.FillMissing(ref streetLine, ref city, ref state, ref zip, formatted);
            return new PlaceAddressDetails
            {
                StreetLine = streetLine,
                City = city,
                State = state,
                Zip = zip,
                FormattedAddress = formatted,
                Latitude = lat,
                Longitude = lon,
            };
        }

        foreach (var component in components.EnumerateArray())
        {
            if (!component.TryGetProperty("types", out var typesEl) || typesEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var types = typesEl.EnumerateArray()
                .Select(t => t.GetString())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var shortText = component.TryGetProperty("shortText", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString()
                : null;
            var longText = component.TryGetProperty("longText", out var lt) && lt.ValueKind == JsonValueKind.String
                ? lt.GetString()
                : null;
            var text = component.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String
                ? tx.GetString()
                : null;
            var label = longText ?? shortText ?? text;

            if (types.Contains("street_number"))
            {
                streetNumber = shortText ?? longText ?? text;
            }
            else if (types.Contains("route"))
            {
                route = longText ?? shortText ?? text;
            }
            else if (types.Contains("locality")
                || (string.IsNullOrWhiteSpace(city)
                    && (types.Contains("postal_town")
                        || types.Contains("sublocality")
                        || types.Contains("sublocality_level_1"))))
            {
                city = label;
            }
            else if (types.Contains("administrative_area_level_1"))
            {
                state = shortText ?? longText ?? text;
            }
            else if (types.Contains("postal_code"))
            {
                zip = shortText ?? longText ?? text;
            }
        }

        streetLine = string.Join(
            " ",
            new[] { streetNumber, route }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(streetLine))
        {
            streetLine = null;
        }

        PlaceAddressFill.FillMissing(ref streetLine, ref city, ref state, ref zip, formatted);

        return new PlaceAddressDetails
        {
            StreetLine = streetLine,
            City = city,
            State = state,
            Zip = zip,
            FormattedAddress = formatted,
            Latitude = lat,
            Longitude = lon,
        };
    }
}
