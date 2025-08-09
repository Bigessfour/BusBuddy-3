using System.Threading.Tasks;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Provides address to coordinate geocoding.
    /// Implementations may be offline stubs or call external providers.
    /// </summary>
    public interface IGeocodingService
    {
        /// <summary>
        /// Attempts to geocode the provided address components to latitude/longitude.
        /// Returns null if the address could not be resolved.
        /// </summary>
        Task<(double latitude, double longitude)?> GeocodeAsync(string? addressLine1, string? city, string? state, string? zip);
    }
}
