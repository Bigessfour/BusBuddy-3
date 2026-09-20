using System.Threading.Tasks;
using BusBuddy.Core.Services.GoogleMaps;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Address validation: Google Maps Platform when a key is present.
    /// Local street/city/state/zip join is display fallback only — never postal success.
    /// </summary>
    public class AddressValidationService : IAddressValidationService
    {
        private readonly IMapsGeoService? _mapsGeo;
        private static readonly ILogger Logger = Log.ForContext<AddressValidationService>();

        public AddressValidationService(IMapsGeoService? mapsGeo = null)
        {
            _mapsGeo = mapsGeo;
        }

        public async Task<(bool IsValid, string? NormalizedAddress)> ValidateAddressAsync(
            string address, string? city = null, string? state = null, string? zip = null)
        {
            if (string.IsNullOrWhiteSpace(address) || _mapsGeo is null)
            {
                if (_mapsGeo is null)
                {
                    Logger.Warning("Maps geo service not registered — rejecting address as unvalidated");
                }

                return (false, null);
            }

            try
            {
                var maps = await _mapsGeo.ValidateAndGeocodeAsync(address, city, state, zip).ConfigureAwait(false);
                if (maps.Ok)
                {
                    return (true, maps.FormattedAddress ?? JoinForDisplay(address, city, state, zip));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating address");
            }

            return (false, null);
        }

        private static string JoinForDisplay(string address, string? city, string? state, string? zip)
        {
            var parts = new List<string> { address.Trim() };
            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(state))
            {
                parts.Add($"{city.Trim()}, {state.Trim().ToUpperInvariant()}");
            }
            else if (!string.IsNullOrWhiteSpace(city))
            {
                parts.Add(city.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(state))
            {
                parts.Add(state.Trim().ToUpperInvariant());
            }

            if (!string.IsNullOrWhiteSpace(zip))
            {
                parts.Add(zip.Trim());
            }

            return string.Join(" ", parts);
        }
    }
}
