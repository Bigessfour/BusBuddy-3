using System.IO;
using System.Text;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct RouteGeoExportResult(bool Success, string Message, string? Path, bool Cancelled = false);

/// <summary>
/// Writes the selected map route as GeoJSON (stop coordinates only — never student names).
/// Always available: the clerk pressed the button, so no Settings gate stands in the way.
/// </summary>
internal static class MapRouteExporter
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapRouteExporter));

    public static async Task<RouteGeoExportResult> ExportSelectedAsync(
        IGeoDataService geoDataService,
        Route? selected,
        CancellationToken cancellationToken = default)
    {
        if (selected is null)
        {
            return new RouteGeoExportResult(false, "Select a route to export", null);
        }

        var path = ExportFilePrompt.TryGetPath(
            DefaultFileName(selected),
            "GeoJSON (*.geojson)|*.geojson|JSON (*.json)|*.json",
            ".geojson");
        if (string.IsNullOrWhiteSpace(path))
        {
            return new RouteGeoExportResult(false, "Export cancelled", null, Cancelled: true);
        }

        return await ExportAsync(geoDataService, selected, path, cancellationToken).ConfigureAwait(false);
    }

    public static string DefaultFileName(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return $"BusBuddy_Route_{SafeFileStem(route.RouteName)}_{DateTime.Now:yyyyMMdd}.geojson";
    }

    public static async Task<RouteGeoExportResult> ExportAsync(
        IGeoDataService geoDataService,
        Route selected,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(geoDataService);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var route = await geoDataService.GetRouteGeoDataAsync(selected.RouteId).ConfigureAwait(false)
            ?? selected;
        var json = RouteGeoJsonExporter.TryBuild(route);
        if (json is null)
        {
            Logger.Information(
                "Route GeoJSON export skipped RouteId={RouteId} Reason=NoWaypoints",
                selected.RouteId);
            return new RouteGeoExportResult(false, "Selected route has no map waypoints to export", null);
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(destinationPath, json, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);

        Logger.Information(
            "Route GeoJSON exported RouteId={RouteId} RouteName={RouteName} Path={Path}",
            route.RouteId,
            route.RouteName,
            destinationPath);
        return new RouteGeoExportResult(
            true,
            $"Exported {route.RouteName} to {Path.GetFileName(destinationPath)}",
            destinationPath);
    }

    internal static string SafeFileStem(string? routeName)
    {
        var stem = string.IsNullOrWhiteSpace(routeName) ? "Route" : routeName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(c, '_');
        }

        stem = stem.Replace(' ', '_');
        return stem.Length > 40 ? stem[..40] : stem;
    }
}
