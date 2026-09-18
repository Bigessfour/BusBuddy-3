using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Google Address Validation when a Maps key is present. Format regex is never success.
    /// Catalog stops live on RouteStop / PickupStop — this type does not invent nearby stops.
    /// </summary>
    public interface IAddressValidationService
    {
        /// <summary>
        /// Validates an address. Success is Google Maps only — never a local format check.
        /// </summary>
        Task<(bool IsValid, string? NormalizedAddress)> ValidateAddressAsync(
            string address, string? city = null, string? state = null, string? zip = null);
    }
}
