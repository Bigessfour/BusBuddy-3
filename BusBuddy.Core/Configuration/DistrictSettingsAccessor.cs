using System.Globalization;
using BusBuddy.Core.Services;
using Microsoft.Extensions.Options;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Live district geography: appsettings <c>RoutingDistrict</c> overlaid by clerk Settings.
/// </summary>
public interface IDistrictSettingsAccessor
{
    RoutingDistrictSettings Current { get; }

    void OverlayFromUserSettings(IUserSettingsService userSettings);

    void Replace(RoutingDistrictSettings settings);
}

public sealed class DistrictSettingsAccessor : IDistrictSettingsAccessor
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly object _gate = new();
    private RoutingDistrictSettings _current;

    public DistrictSettingsAccessor(IOptions<RoutingDistrictSettings>? options = null)
    {
        _current = Copy(options?.Value ?? new RoutingDistrictSettings());
    }

    public RoutingDistrictSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void OverlayFromUserSettings(IUserSettingsService userSettings)
    {
        ArgumentNullException.ThrowIfNull(userSettings);

        lock (_gate)
        {
            var next = Copy(_current);
            ApplyUserOverlay(next, userSettings);
            _current = next;
        }
    }

    public void Replace(RoutingDistrictSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            _current = Copy(settings);
        }
    }

    public static RoutingDistrictSettings Copy(RoutingDistrictSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new RoutingDistrictSettings
        {
            BoundingBoxMinLat = source.BoundingBoxMinLat,
            BoundingBoxMinLon = source.BoundingBoxMinLon,
            BoundingBoxMaxLat = source.BoundingBoxMaxLat,
            BoundingBoxMaxLon = source.BoundingBoxMaxLon,
            TargetRidersPerCell = source.TargetRidersPerCell,
            MaxPickupGapMinutes = source.MaxPickupGapMinutes,
            AverageSpeedMph = source.AverageSpeedMph,
            MaxRideMinutes = source.MaxRideMinutes,
            AllowSeatingOverride = source.AllowSeatingOverride,
            StopSuggestMaxMeters = source.StopSuggestMaxMeters,
            DepotName = source.DepotName,
            DepotAddress = source.DepotAddress,
            DepotCity = source.DepotCity,
            DepotState = source.DepotState,
            DepotZipCode = source.DepotZipCode,
            DepotLatitude = source.DepotLatitude,
            DepotLongitude = source.DepotLongitude
        };
    }

    public static async Task WriteToUserAsync(IUserSettingsService userSettings, RoutingDistrictSettings settings)
    {
        ArgumentNullException.ThrowIfNull(userSettings);
        ArgumentNullException.ThrowIfNull(settings);

        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotName, settings.DepotName ?? string.Empty).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotAddress, settings.DepotAddress ?? string.Empty).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotCity, settings.DepotCity ?? string.Empty).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotState, settings.DepotState ?? string.Empty).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotZipCode, settings.DepotZipCode ?? string.Empty).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, FormatCoord(settings.DepotLatitude)).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictDepotLongitude, FormatCoord(settings.DepotLongitude)).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictBoundingBoxMinLat, FormatCoord(settings.BoundingBoxMinLat)).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictBoundingBoxMinLon, FormatCoord(settings.BoundingBoxMinLon)).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictBoundingBoxMaxLat, FormatCoord(settings.BoundingBoxMaxLat)).ConfigureAwait(false);
        await userSettings.SetSettingAsync(UserSettingsKeys.DistrictBoundingBoxMaxLon, FormatCoord(settings.BoundingBoxMaxLon)).ConfigureAwait(false);
    }

    public static string FormatCoord(double? value) =>
        value is double v ? v.ToString("F4", Invariant) : string.Empty;

    public static double? ParseCoord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text.Trim(), NumberStyles.Float, Invariant, out var parsed)
            ? parsed
            : null;
    }

    public static void ApplyUserOverlay(RoutingDistrictSettings target, IUserSettingsService userSettings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(userSettings);

        ApplyString(userSettings, UserSettingsKeys.DistrictDepotName, v => target.DepotName = v);
        ApplyString(userSettings, UserSettingsKeys.DistrictDepotAddress, v => target.DepotAddress = v);
        ApplyString(userSettings, UserSettingsKeys.DistrictDepotCity, v => target.DepotCity = v);
        ApplyString(userSettings, UserSettingsKeys.DistrictDepotState, v => target.DepotState = v);
        ApplyString(userSettings, UserSettingsKeys.DistrictDepotZipCode, v => target.DepotZipCode = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictDepotLatitude, v => target.DepotLatitude = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictDepotLongitude, v => target.DepotLongitude = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictBoundingBoxMinLat, v => target.BoundingBoxMinLat = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictBoundingBoxMinLon, v => target.BoundingBoxMinLon = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictBoundingBoxMaxLat, v => target.BoundingBoxMaxLat = v);
        ApplyCoord(userSettings, UserSettingsKeys.DistrictBoundingBoxMaxLon, v => target.BoundingBoxMaxLon = v);
    }

    private static void ApplyString(IUserSettingsService user, string key, Action<string?> assign)
    {
        if (!user.HasKey(key))
        {
            return;
        }

        var value = user.GetSettingAsync(key, string.Empty).GetAwaiter().GetResult();
        assign(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    private static void ApplyCoord(IUserSettingsService user, string key, Action<double?> assign)
    {
        if (!user.HasKey(key))
        {
            return;
        }

        var raw = user.GetSettingAsync(key, string.Empty).GetAwaiter().GetResult();
        assign(ParseCoord(raw));
    }
}
