using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for validating addresses and finding nearby bus stops
    /// </summary>
    public interface IAddressValidationService
    {
        /// <summary>
        /// Validates an address and returns normalized version if valid
        /// </summary>
        /// <param name="address">The address to validate</param>
        /// <param name="city">The city</param>
        /// <param name="state">The state</param>
        /// <param name="zip">The zip code</param>
        /// <returns>Tuple with validity result and normalized address if valid</returns>
        Task<(bool IsValid, string? NormalizedAddress)> ValidateAddressAsync(
            string address, string? city = null, string? state = null, string? zip = null);

        /// <summary>
        /// Finds nearby bus stops for a validated address
        /// </summary>
        /// <param name="address">The validated address</param>
        /// <param name="city">The city</param>
        /// <param name="state">The state</param>
        /// <param name="zip">The zip code</param>
        /// <returns>List of nearby bus stop names</returns>
        Task<List<string>> FindNearbyBusStopsAsync(
            string address, string? city = null, string? state = null, string? zip = null);

        /// <summary>
        /// Gets the distance between an address and a bus stop
        /// </summary>
        /// <param name="address">The address</param>
        /// <param name="busStop">The bus stop name</param>
        /// <returns>Distance in miles</returns>
        Task<double> GetDistanceToBusStopAsync(string address, string busStop);

        /// <summary>
        /// Formats an address for display
        /// </summary>
        /// <param name="address">Street address</param>
        /// <param name="city">City</param>
        /// <param name="state">State</param>
        /// <param name="zip">Zip code</param>
        /// <returns>Formatted address string</returns>
        string FormatAddress(string address, string? city = null, string? state = null, string? zip = null);

        /// <summary>
        /// Parses a formatted address into components
        /// </summary>
        /// <param name="formattedAddress">The full formatted address</param>
        /// <returns>Tuple with address components</returns>
        (string Address, string? City, string? State, string? Zip) ParseAddress(string formattedAddress);
    }
}
