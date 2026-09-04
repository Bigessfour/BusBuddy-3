using System;
using System.Windows;
using BusBuddy.WPF.ViewModels.Map;
using BusBuddy.WPF.Views.Map;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Single entry point for the district map — reuses one window so the singleton
/// <see cref="MapViewModel"/> is not shared across multiple <see cref="MapView"/> hosts.
/// </summary>
public static class MapViewLauncher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapViewLauncher));
    private static Window? _mapWindow;

    public static bool IsOpen => _mapWindow is { IsLoaded: true };

    public static void Show(Window? owner, Action<MapViewModel>? configure = null)
    {
        if (_mapWindow is { IsLoaded: true })
        {
            Logger.Debug("Activating existing district map window");
            ApplyConfigure(configure);
            _mapWindow.Activate();
            _mapWindow.Focus();
            return;
        }

        Logger.Information("Opening district map window");
        var mapView = new MapView();
        _mapWindow = new Window
        {
            Title = "District Map",
            Width = 1200,
            Height = 900,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Content = mapView,
        };

        _mapWindow.Closed += (_, _) => _mapWindow = null;
        _mapWindow.Loaded += (_, _) => ApplyConfigure(configure);
        _mapWindow.Show();
    }

    private static void ApplyConfigure(Action<MapViewModel>? configure)
    {
        if (configure is null)
        {
            return;
        }

        var vm = ResolveMapViewModel();
        if (vm is not null)
        {
            configure(vm);
        }
        else
        {
            Logger.Warning("MapViewLauncher configure skipped — MapViewModel not resolved");
        }
    }

    private static MapViewModel? ResolveMapViewModel() =>
        App.ServiceProvider?.GetService<MapViewModel>();
}
