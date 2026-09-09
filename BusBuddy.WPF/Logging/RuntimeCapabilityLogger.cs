using System;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace BusBuddy.WPF.Logging;

/// <summary>
/// One-shot startup snapshot of secrets-presence and district/geo wiring (never logs secret values).
/// </summary>
public static class RuntimeCapabilityLogger
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RuntimeCapabilityLogger));

    public static string DescribePresence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("${", StringComparison.Ordinal))
        {
            return "missing";
        }

        return "present";
    }

    public static void WriteStartupSnapshot(IServiceProvider? services)
    {
        try
        {
            var configuration = services?.GetService<IConfiguration>();
            var district = services?.GetService<IDistrictSettingsAccessor>()?.Current;
            var mapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")
                ?? configuration?["GoogleMaps:ApiKey"];
            var license = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            var gcp = Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                ?? configuration?["GoogleMaps:QuotaProject"];
            var provider = configuration?["DatabaseProvider"] ?? "(unset)";
            var connection = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            var mapsDiagnostics = Environment.GetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride)
                ?? configuration?["Map:InteractionDiagnostics"]
                ?? "(default)";

            UiDiagnosticsLog.Write(
                Logger,
                LogEventLevel.Information,
                "Runtime capability SyncfusionLicense={SyncfusionLicense} MapsKey={MapsKey} GcpProject={GcpProject} DatabaseProvider={DatabaseProvider} BusBuddyConnection={Connection} PostgresEndpoint={Endpoint} DepotLat={DepotLat} DepotLon={DepotLon} BBoxMinLat={BBoxMinLat} BBoxMaxLat={BBoxMaxLat} MapDiagnostics={MapDiagnostics} FuelService={Fuel} MaintenanceService={Maint} ScheduleService={Sched} ActivityLogService={ActivityLog} GeocodingService={Geo} RoutingService={Routing}",
                DescribePresence(license),
                DescribePresence(mapsKey),
                string.IsNullOrWhiteSpace(gcp) ? "missing" : gcp,
                provider,
                DescribePresence(connection),
                PostgresConnectionResolver.DescribeEndpoint(connection) ?? "(appsettings)",
                district?.DepotLatitude,
                district?.DepotLongitude,
                district?.BoundingBoxMinLat,
                district?.BoundingBoxMaxLat,
                mapsDiagnostics,
                services?.GetService<IFuelService>() is not null,
                services?.GetService<IMaintenanceService>() is not null,
                services?.GetService<IScheduleService>() is not null,
                services?.GetService<IActivityLogService>() is not null,
                services?.GetService<IGeocodingService>() is not null,
                services?.GetService<IRoutingService>() is not null);

            if (string.Equals(DescribePresence(mapsKey), "missing", StringComparison.Ordinal))
            {
                UiDiagnosticsLog.Write(
                    Logger,
                    LogEventLevel.Warning,
                    "GOOGLE_MAPS_API_KEY missing — Address Validation / Places / Google tiles fail-open to OSM");
            }

            if (string.Equals(DescribePresence(license), "missing", StringComparison.Ordinal))
            {
                UiDiagnosticsLog.Write(
                    Logger,
                    LogEventLevel.Warning,
                    "SYNCFUSION_LICENSE_KEY missing — Syncfusion controls may watermark or throw on first use");
            }

            if (services?.GetService<IActivityLogService>() is null)
            {
                UiDiagnosticsLog.Write(
                    Logger,
                    LogEventLevel.Warning,
                    "IActivityLogService is not registered — Activity Timeline cannot load");
            }

            if (district is not null
                && district.DepotLatitude.HasValue
                && district.DepotLongitude.HasValue
                && Math.Abs(district.DepotLatitude.Value - MapDefaults.UnconfiguredLatitude) < 0.01
                && Math.Abs(district.DepotLongitude.Value - MapDefaults.UnconfiguredLongitude) < 0.01)
            {
                UiDiagnosticsLog.Write(
                    Logger,
                    LogEventLevel.Warning,
                    "District depot is at US-centroid fallback ({Lat}, {Lon}) — set Settings depot lat/lng before District Map smoke",
                    district.DepotLatitude,
                    district.DepotLongitude);
            }
        }
        catch (Exception ex)
        {
            UiDiagnosticsLog.Write(
                Logger,
                LogEventLevel.Warning,
                ex,
                "Runtime capability snapshot failed");
        }
    }
}
