using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Shared gate for assigning <see cref="ImageryLayer.Markers"/> after the visual tree can
/// <c>TransformToVisual</c>. Binding <c>Markers</c> or <c>MarkerTemplateSelector</c> in XAML
/// inflates Syncfusion <c>CustomDataSymbol</c> during first Measure (VM runtime-errors.log
/// 2026-09-17 District Map cascade).
/// </summary>
public static class MapMarkerHost
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(150);
    public const int RetryLimit = 20;

    public static bool CanHost(SfMap? map, ImageryLayer? layer)
    {
        if (map is null || layer is null)
        {
            return false;
        }

        if (map.ActualWidth <= 0 || map.ActualHeight <= 0)
        {
            return false;
        }

        if (PresentationSource.FromVisual(map) is null || PresentationSource.FromVisual(layer) is null)
        {
            return false;
        }

        try
        {
            _ = layer.TransformToVisual(map);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TryAssign(
        SfMap? map,
        ImageryLayer? layer,
        IEnumerable? markers,
        DataTemplateSelector? templateSelector = null)
    {
        if (!CanHost(map, layer) || layer is null)
        {
            return false;
        }

        if (templateSelector is not null)
        {
            layer.MarkerTemplateSelector = templateSelector;
            layer.MarkerTemplate = null;
        }

        layer.Markers = markers;
        return true;
    }

    /// <summary>
    /// Assign markers, then require a completed measure/arrange that did not swallow
    /// <c>TransformToVisual</c>. <see cref="TryAssign"/> alone can return true while
    /// <c>CustomDataSymbol.ApplyTemplate</c> still fails on the next layout pass.
    /// </summary>
    public static bool TryAssignAndLayout(
        SfMap? map,
        ImageryLayer? layer,
        IEnumerable? markers,
        DataTemplateSelector? templateSelector = null)
    {
        if (!TryAssign(map, layer, markers, templateSelector))
        {
            return false;
        }

        if (layer is not GoogleMapTilesImageryLayer tiles)
        {
            return true;
        }

        tiles.BeginMarkerHostCheck();
        tiles.InvalidateMeasure();
        tiles.InvalidateArrange();
        try
        {
            tiles.UpdateLayout();
        }
        catch (Exception ex) when (GoogleMapTilesImageryLayer.IsVisualTreeNotReady(ex))
        {
            return false;
        }

        return tiles.LastMeasureCompleted && !tiles.LastLayoutSkippedVisualTree;
    }

    /// <summary>
    /// Retries a host-gated assign until layout succeeds or <see cref="RetryLimit"/> is reached.
    /// SizeChanged is not guaranteed after the first layout.
    /// </summary>
    public sealed class RetryScheduler
    {
        private readonly Dispatcher _dispatcher;
        private readonly Func<bool> _tryAssign;
        private readonly Action<int>? _onGiveUp;
        private DispatcherTimer? _timer;
        private int _retries;

        public RetryScheduler(Dispatcher dispatcher, Func<bool> tryAssign, Action<int>? onGiveUp = null)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _tryAssign = tryAssign ?? throw new ArgumentNullException(nameof(tryAssign));
            _onGiveUp = onGiveUp;
        }

        public void Arm()
        {
            _timer ??= new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = RetryInterval,
            };
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            if (!_timer.IsEnabled)
            {
                _retries = 0;
                _timer.Start();
            }
        }

        public void Stop()
        {
            _timer?.Stop();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _retries++;
            if (_tryAssign())
            {
                Stop();
                return;
            }

            if (_retries >= RetryLimit)
            {
                Stop();
                _onGiveUp?.Invoke(RetryLimit);
            }
        }
    }
}
