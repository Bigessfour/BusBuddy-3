using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BusBuddy.Core.Mapping;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Interaction trace for the District Map (VM troubleshooting).
/// <para>
/// Records mouse buttons, wheel, key presses (key name + modifiers only — the map has no text input),
/// Syncfusion <see cref="SfMap"/> zoom/pan events, <see cref="ImageryLayer"/> camera events and Google tile
/// requests into a ring buffer and a dedicated rolling file <c>logs/map-interactions-.log</c>. When a dispatcher
/// exception or a WPF binding error fires while the map is loaded, the last <see cref="BreadcrumbCapacity"/>
/// breadcrumbs are dumped next to the error so the failing interaction can be replayed.
/// </para>
/// <para>
/// Enabled by <c>Map:InteractionDiagnostics</c> (appsettings) and overridable with
/// <c>BUSBUDDY_MAP_DIAGNOSTICS=0|1</c>. Detach with <see cref="Dispose"/> when the view unloads.
/// </para>
/// </summary>
public sealed class MapInteractionDiagnostics : IDisposable
{
    public const string ConfigKey = "Map:InteractionDiagnostics";
    public const string EnvironmentOverride = "BUSBUDDY_MAP_DIAGNOSTICS";
    public const int BreadcrumbCapacity = 30;

    private static readonly ILogger AppLogger = Log.ForContext<MapInteractionDiagnostics>();

    private readonly SfMap _map;
    private readonly GoogleMapTilesImageryLayer? _layer;
    private readonly Logger? _fileLogger;
    private readonly Dispatcher _dispatcher;
    private readonly Queue<string> _breadcrumbs = new(BreadcrumbCapacity);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BindingTraceListener _bindingListener;
    private int _tileRequests;
    private long _lastTileLogTick;
    private DateTime _mouseDownUtc;
    private bool _disposed;

    private MapInteractionDiagnostics(SfMap map, GoogleMapTilesImageryLayer? layer, Logger? fileLogger)
    {
        _map = map;
        _layer = layer;
        _fileLogger = fileLogger;
        _dispatcher = map.Dispatcher;
        _bindingListener = new BindingTraceListener(this);

        map.PreviewMouseDown += OnMouseDown;
        map.PreviewMouseUp += OnMouseUp;
        map.PreviewMouseWheel += OnMouseWheel;
        map.PreviewKeyDown += OnKeyDown;
        map.MouseLeave += OnMouseLeave;
        map.ZoomedIn += OnZoomedIn;
        map.ZoomedOut += OnZoomedOut;
        map.Panned += OnPanned;
        map.MapToolTipOpening += OnToolTipOpening;

        if (layer is not null)
        {
            layer.ZoomLevelChanging += OnZoomLevelChanging;
            layer.CenterChanged += OnCenterChanged;
            layer.MarkerSelected += OnMarkerSelected;
            layer.TileRequested += OnTileRequested;
        }

        _dispatcher.UnhandledException += OnDispatcherUnhandledException;
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(_bindingListener);
        if (PresentationTraceSources.DataBindingSource.Switch.Level < SourceLevels.Warning)
        {
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        }

        Record("session-start", $"size={map.ActualWidth:F0}x{map.ActualHeight:F0} zoom={map.ZoomLevel} google={(layer?.IsGoogleTilesActive ?? false)}");
    }

    /// <summary>True when config/env asks for the trace (env var wins).</summary>
    public static bool IsEnabled(IConfiguration? configuration)
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentOverride);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim() is "1" or "true" or "True" or "TRUE" or "yes";
        }

        return configuration?.GetValue<bool?>(ConfigKey) ?? false;
    }

    /// <summary>Attaches when enabled; returns null (no-op) otherwise or on any failure.</summary>
    public static MapInteractionDiagnostics? TryAttach(
        SfMap? map,
        GoogleMapTilesImageryLayer? layer,
        IConfiguration? configuration)
    {
        if (map is null || !IsEnabled(configuration))
        {
            return null;
        }

        try
        {
            var diagnostics = new MapInteractionDiagnostics(map, layer, CreateFileLogger());
            AppLogger.Information(
                "Map interaction diagnostics enabled — trace file {File}",
                diagnostics._fileLogger is null ? "(app log only)" : ResolveLogPath());
            return diagnostics;
        }
        catch (Exception ex)
        {
            AppLogger.Warning(ex, "Map interaction diagnostics could not attach");
            return null;
        }
    }

    /// <summary>Snapshot of the breadcrumb ring (oldest first) for error reports.</summary>
    public IReadOnlyList<string> Breadcrumbs
    {
        get
        {
            lock (_breadcrumbs)
            {
                return _breadcrumbs.ToArray();
            }
        }
    }

    /// <summary>Formats one breadcrumb line: elapsed ms, event name, detail.</summary>
    internal static string FormatBreadcrumb(long elapsedMs, string name, string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"+{elapsedMs,7}ms {name,-18} {detail}");

    /// <summary>Key names only — never characters — so the trace stays free of typed text.</summary>
    internal static string DescribeKey(Key key, ModifierKeys modifiers) =>
        modifiers == ModifierKeys.None ? key.ToString() : $"{modifiers}+{key}";

    public void Record(string name, string detail)
    {
        if (_disposed)
        {
            return;
        }

        var line = FormatBreadcrumb(_clock.ElapsedMilliseconds, name, detail);
        lock (_breadcrumbs)
        {
            if (_breadcrumbs.Count == BreadcrumbCapacity)
            {
                _breadcrumbs.Dequeue();
            }

            _breadcrumbs.Enqueue(line);
        }

        _fileLogger?.Information("{Breadcrumb}", line);
        AppLogger.Debug("Map {Event} {Detail}", name, detail);
    }

    /// <summary>Writes the exception plus the interaction breadcrumbs that preceded it.</summary>
    public void RecordError(string source, Exception exception)
    {
        var crumbs = Breadcrumbs;
        var sb = new StringBuilder();
        foreach (var crumb in crumbs)
        {
            sb.Append("    ").AppendLine(crumb);
        }

        var zoom = _map.ZoomLevel;
        var center = _layer?.Center ?? default;
        AppLogger.Error(
            exception,
            "Map runtime error from {Source} at zoom={Zoom} center=({Lat:F5},{Lon:F5}) google={Google}; last {Count} interactions:{NewLine}{Breadcrumbs}",
            source,
            zoom,
            center.X,
            center.Y,
            _layer?.IsGoogleTilesActive ?? false,
            crumbs.Count,
            Environment.NewLine,
            sb.ToString());
        _fileLogger?.Error(exception, "ERROR {Source} zoom={Zoom} center=({Lat:F5},{Lon:F5}){NewLine}{Breadcrumbs}", source, zoom, center.X, center.Y, Environment.NewLine, sb.ToString());
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownUtc = DateTime.UtcNow;
        var p = e.GetPosition(_map);
        Record("mouse-down", $"{e.ChangedButton} at ({p.X:F0},{p.Y:F0}) clicks={e.ClickCount} mods={Keyboard.Modifiers}");
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_map);
        var held = _mouseDownUtc == default ? 0 : (DateTime.UtcNow - _mouseDownUtc).TotalMilliseconds;
        Record("mouse-up", $"{e.ChangedButton} at ({p.X:F0},{p.Y:F0}) held={held:F0}ms zoom={_map.ZoomLevel}");
        _mouseDownUtc = default;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var p = e.GetPosition(_map);
        Record("wheel", $"delta={e.Delta} at ({p.X:F0},{p.Y:F0}) zoomBefore={_map.ZoomLevel}");
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        Record("key", $"{DescribeKey(key, Keyboard.Modifiers)} repeat={e.IsRepeat}");
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
        {
            // Drag that left the control with a button still down: the classic stuck-pan signature.
            Record("mouse-leave", $"button still pressed L={e.LeftButton} R={e.RightButton}");
        }
    }

    private void OnZoomedIn(object sender, ZoomEventArgs e) =>
        Record("sf-zoomed-in", $"level={e.ZoomLevel} at ({e.Latitude:F5},{e.Longitude:F5})");

    private void OnZoomedOut(object sender, ZoomEventArgs e) =>
        Record("sf-zoomed-out", $"level={e.ZoomLevel} at ({e.Latitude:F5},{e.Longitude:F5})");

    private void OnPanned(object sender, PanEventArgs e) =>
        Record("sf-panned", $"{e.PanningDirection} to ({e.Latitude:F5},{e.Longitude:F5})");

    private void OnToolTipOpening(object? sender, ToolTipOpeningEventArgs e) =>
        Record("sf-tooltip", e.Data?.GetType().Name ?? "(null)");

    private void OnZoomLevelChanging(object? sender, ZoomLevelChangingEventArgs e) =>
        Record("layer-zoom", $"{e.PreviousLevel}->{e.CurrentLevel}");

    private void OnCenterChanged(object? sender, CenterChangedEventArgs e)
    {
        var c = _layer?.Center ?? default;
        Record("layer-center", $"({c.Y:F5},{c.X:F5})");
    }

    private void OnMarkerSelected(object? sender, MarkerSelectedEventArgs e) =>
        Record("marker-selected", MapMarkerTemplateSelector.Unwrap(e.SelectedMarker)?.Label ?? e.SelectedMarker?.GetType().Name ?? "(null)");

    private void OnTileRequested(object? sender, GoogleMapTilesImageryLayer.TileRequestedEventArgs e)
    {
        // Tiles arrive in bursts; log the first of each burst and a running count, never the URL (session + key).
        var n = ++_tileRequests;
        var now = _clock.ElapsedMilliseconds;
        if (now - _lastTileLogTick > 500)
        {
            _lastTileLogTick = now;
            Record("tiles", $"z={e.Zoom} first=({e.X},{e.Y}) total={n} google={e.IsGoogle}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!_map.IsLoaded)
        {
            return;
        }

        // App.OnDispatcherUnhandledException still owns Handled/MessageBox; this only adds context.
        RecordError("Dispatcher", e.Exception);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Record("session-end", $"tiles={_tileRequests}");

        _map.PreviewMouseDown -= OnMouseDown;
        _map.PreviewMouseUp -= OnMouseUp;
        _map.PreviewMouseWheel -= OnMouseWheel;
        _map.PreviewKeyDown -= OnKeyDown;
        _map.MouseLeave -= OnMouseLeave;
        _map.ZoomedIn -= OnZoomedIn;
        _map.ZoomedOut -= OnZoomedOut;
        _map.Panned -= OnPanned;
        _map.MapToolTipOpening -= OnToolTipOpening;

        if (_layer is not null)
        {
            _layer.ZoomLevelChanging -= OnZoomLevelChanging;
            _layer.CenterChanged -= OnCenterChanged;
            _layer.MarkerSelected -= OnMarkerSelected;
            _layer.TileRequested -= OnTileRequested;
        }

        _dispatcher.UnhandledException -= OnDispatcherUnhandledException;
        PresentationTraceSources.DataBindingSource.Listeners.Remove(_bindingListener);
        _fileLogger?.Dispose();
    }

    private static string ResolveLogPath()
    {
        // Same folder as the Serilog "..\logs\" sinks in appsettings.json.
        var logsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "logs"));
        return Path.Combine(logsDir, "map-interactions-.log");
    }

    private static Logger? CreateFileLogger()
    {
        try
        {
            var path = ResolveLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    fileSizeLimitBytes: 5 * 1024 * 1024,
                    shared: true,
                    outputTemplate: "{Timestamp:HH:mm:ss.fff} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        catch (Exception ex)
        {
            AppLogger.Warning(ex, "Map interaction trace file unavailable — using app log only");
            return null;
        }
    }

    /// <summary>Routes WPF binding errors raised while the map is up into the trace with breadcrumbs.</summary>
    private sealed class BindingTraceListener : TraceListener
    {
        private readonly MapInteractionDiagnostics _owner;

        public BindingTraceListener(MapInteractionDiagnostics owner) => _owner = owner;

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (string.IsNullOrWhiteSpace(message) || _owner._disposed)
            {
                return;
            }

            // Only the map surface and its templates are interesting here.
            if (message.Contains("MapView", StringComparison.Ordinal)
                || message.Contains("MapMarker", StringComparison.Ordinal)
                || message.Contains("ImageryLayer", StringComparison.Ordinal)
                || message.Contains("SfMap", StringComparison.Ordinal))
            {
                _owner.Record("binding-error", message.Trim());
            }
        }
    }
}
