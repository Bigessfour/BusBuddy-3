using System.Linq;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BusBuddy.WPF.Utilities;

/// <summary>WPF entry to <see cref="DistrictMapAnchor"/> using clerk Settings + school catalog.</summary>
internal static class DistrictCameraUi
{
    public static RoutingDistrictSettings? CurrentSettings()
    {
        var sp = App.ServiceProvider;
        return sp?.GetService<IDistrictSettingsAccessor>()?.Current
            ?? sp?.GetService<IOptions<RoutingDistrictSettings>>()?.Value;
    }

    public static (double Latitude, double Longitude, int ZoomLevel) Resolve(
        double? schoolLatitude = null,
        double? schoolLongitude = null) =>
        DistrictMapAnchor.ResolveCamera(CurrentSettings(), schoolLatitude, schoolLongitude);

    public static async Task<(double Latitude, double Longitude, int ZoomLevel)> ResolveAsync(
        IServiceProvider? services)
    {
        double? schoolLat = null;
        double? schoolLon = null;
        var dest = services?.GetService<IDestinationService>();
        if (dest is not null)
        {
            var school = (await dest.GetActiveSchoolsAsync().ConfigureAwait(false))
                .FirstOrDefault(s => s.HasGpsCoordinates);
            if (school is not null)
            {
                schoolLat = (double)school.Latitude!;
                schoolLon = (double)school.Longitude!;
            }
        }

        var settings = services?.GetService<IDistrictSettingsAccessor>()?.Current
            ?? services?.GetService<IOptions<RoutingDistrictSettings>>()?.Value
            ?? CurrentSettings();
        return DistrictMapAnchor.ResolveCamera(settings, schoolLat, schoolLon);
    }
}
