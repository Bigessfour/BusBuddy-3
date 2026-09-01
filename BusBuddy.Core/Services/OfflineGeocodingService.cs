using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Deterministic, offline geocoder for tests only — not registered in production DI.
    /// Hashes addresses into a small box around the continental-US fallback center.
    /// </summary>
    public sealed class OfflineGeocodingService : IGeocodingService
    {
        private const double CenterLat = MapDefaults.FallbackLatitude;
        private const double CenterLon = MapDefaults.FallbackLongitude;
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

            double lat = CenterLat + (r2 * MaxOffsetDeg);
            double lon = CenterLon + (r1 * MaxOffsetDeg);
            return Task.FromResult<(double, double)?>((lat, lon));
        }
    }
}
