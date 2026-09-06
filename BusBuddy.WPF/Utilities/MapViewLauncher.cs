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
        var effectiveOwner = DialogOwner.Resolve(owner, _mapWindow);

        if (_mapWindow is { IsLoaded: true })
        {
            Logger.Debug("Activating existing district map window");
            TrySetOwner(_mapWindow, effectiveOwner);
            ApplyConfigure(configure);
            BringToFront(_mapWindow);
            return;
        }

        Logger.Information("Opening district map window Owner={Owner}", effectiveOwner?.GetType().Name ?? "(none)");
        var mapView = new MapView();
        _mapWindow = new Window
        {
            Title = "District Map",
            Width = 1200,
            Height = 900,
            ShowActivated = true,
            WindowStartupLocation = effectiveOwner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            Owner = effectiveOwner,
            Content = mapView,
        };

        _mapWindow.Closed += (_, _) => _mapWindow = null;
        _mapWindow.Loaded += (_, _) =>
        {
            ApplyConfigure(configure);
            BringToFront(_mapWindow);
        };
        _mapWindow.Show();
        BringToFront(_mapWindow);
    }

    internal static Window? ResolveOwner(Window? requested) => DialogOwner.Resolve(requested, _mapWindow);

    private static void TrySetOwner(Window map, Window? owner)
    {
        if (owner is null || ReferenceEquals(map, owner) || ReferenceEquals(map.Owner, owner))
        {
            return;
        }

        for (var walk = owner.Owner; walk is not null; walk = walk.Owner)
        {
            if (ReferenceEquals(walk, map))
            {
                return;
            }
        }

        try
        {
            map.Owner = owner;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Could not reparent district map owner");
        }
    }

    private static void BringToFront(Window? window)
    {
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Focus();
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
