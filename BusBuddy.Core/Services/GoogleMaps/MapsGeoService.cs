using BusBuddy.Core.Services.Interfaces;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Facade over <see cref="GoogleAddressValidationClient"/> with address-result caching.
/// </summary>
public sealed class MapsGeoService : IMapsGeoService
{
    private static readonly ILogger Logger = Log.ForContext<MapsGeoService>();

    private readonly GoogleAddressValidationClient _client;
    private readonly IMapsAddressCache _cache;

    public MapsGeoService(GoogleAddressValidationClient client, IMapsAddressCache cache)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_client.ResolvedApiKey);

    public async Task<MapsGeocodeResult> ValidateAndGeocodeAsync(
        string? street,
        string? city,
        string? state,
        string? zip,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = MapsAddressCache.BuildCacheKey(street, city, state, zip);
        if (_cache.TryGet(cacheKey, out var cached) && cached is not null)
        {
            Logger.Information(
                "Maps validate/geocode cache hit ViaService={ViaService} Precision={Precision}",
                true,
                cached.Precision);
            return cached;
        }

        using (LogContext.PushProperty("Operation", "MapsValidateGeocode"))
        {
            var result = await _client.ValidateAndGeocodeAsync(street, city, state, zip, cancellationToken)
                .ConfigureAwait(false);
            if (result.Ok)
            {
                _cache.Set(cacheKey, result);
            }

            return result;
        }
    }

    public Task<(double latitude, double longitude)?> GeocodeAsync(
        string? addressLine1,
        string? city,
        string? state,
        string? zip) =>
        GeocodeAsync(addressLine1, city, state, zip, CancellationToken.None);

    public async Task<(double latitude, double longitude)?> GeocodeAsync(
        string? street,
        string? city,
        string? state,
        string? zip,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateAndGeocodeAsync(street, city, state, zip, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || !result.Latitude.HasValue || !result.Longitude.HasValue)
        {
            return null;
        }

        return (result.Latitude.Value, result.Longitude.Value);
    }
}
