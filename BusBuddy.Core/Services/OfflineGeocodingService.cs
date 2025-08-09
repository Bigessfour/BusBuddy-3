using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Deterministic, offline geocoder suitable for demos and tests.
    /// Uses a simple string hash to map addresses into a bounded area around Wiley, CO.
    /// Replace with a real provider when keys/configs are available.
    /// </summary>
    public sealed class OfflineGeocodingService : IGeocodingService
    {
        // Wiley School coordinates as center point
        private const double CenterLat = 38.1527; // 510 Ward St, Wiley, CO
        private const double CenterLon = -102.7204;
        private const double MaxOffsetDeg = 0.25; // ~27km radius; safe for demo

        public Task<(double latitude, double longitude)?> GeocodeAsync(string? addressLine1, string? city, string? state, string? zip)
        {
            if (string.IsNullOrWhiteSpace(addressLine1))
            {
                return Task.FromResult<(double, double)?>(null);
            }
            var key = new StringBuilder()
                .Append(addressLine1?.Trim())
                .Append('|').Append(city?.Trim())
                .Append('|').Append(state?.Trim())
                .Append('|').Append(zip?.Trim())
                .ToString();

            // Simple FNV-1a 64-bit hash
            ulong hash = 1469598103934665603UL;
            foreach (char c in key)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            // Derive two pseudo-random offsets in [-1, 1]
            double r1 = ((hash & 0xFFFFFFFF) / (double)uint.MaxValue) * 2 - 1;
            double r2 = (((hash >> 32) & 0xFFFFFFFF) / (double)uint.MaxValue) * 2 - 1;

            // Bias city names we know to rough directions (keeps consistent map feel)
            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityLower = city.Trim().ToLowerInvariant();
                if (cityLower.Contains("lajunta") || cityLower.Contains("la junta"))
                {
                    r1 = Math.Abs(r1) * 0.9 + 0.1; // east/southeast of Wiley
                    r2 = r2 * 0.5; // smaller north/south spread
                }
                else if (cityLower.Contains("lamar"))
                {
                    r1 = -Math.Abs(r1) * 0.9 - 0.1; // west of Wiley
                    r2 = r2 * 0.5;
                }
                else if (cityLower.Contains("prowers") || cityLower.Contains("bent"))
                {
                    r1 *= 0.5; r2 *= 0.5; // closer-in scatter within counties
                }
            }

            double lat = CenterLat + (r2 * MaxOffsetDeg);
            double lon = CenterLon + (r1 * MaxOffsetDeg);
            return Task.FromResult<(double, double)?>( (lat, lon) );
        }
    }
}
