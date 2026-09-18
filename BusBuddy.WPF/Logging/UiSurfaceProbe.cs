using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using Serilog.Events;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.Maps;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Logging;

/// <summary>
/// On each BusBuddy <see cref="UserControl"/> / <see cref="Window"/> Loaded, logs DataContext,
/// empty item sources, and failed bindings so a UI session captures misconfigured elements.
/// </summary>
public static class UiSurfaceProbe
{
    public const string BusBuddyNamespacePrefix = "BusBuddy.WPF";
    private const int MaxVisualNodes = 250;

    private static readonly ILogger Logger = Log.ForContext(typeof(UiSurfaceProbe));
    private static readonly object ProbedMarker = new();
    private static readonly ConditionalWeakTable<FrameworkElement, object> Probed = new();
    private static bool _registered;

    public static bool IsBusBuddySurface(Type? type)
    {
        var ns = type?.Namespace;
        return !string.IsNullOrEmpty(ns)
            && ns.StartsWith(BusBuddyNamespacePrefix, StringComparison.Ordinal);
    }

    public static string DescribeDataContext(object? dataContext)
    {
        return dataContext is null ? "(null)" : dataContext.GetType().FullName ?? dataContext.GetType().Name;
    }

    /// <summary>
    /// Leaf controls under <c>BusBuddy.WPF.Controls</c> (PlacesAddressBox) use dependency
    /// properties, not a view-model. Do not treat a null DataContext as a miswire.
    /// </summary>
    public static bool IsEmbeddedControl(Type? type)
    {
        var ns = type?.Namespace;
        return !string.IsNullOrEmpty(ns)
            && ns.Equals("BusBuddy.WPF.Controls", StringComparison.Ordinal);
    }

    /// <summary>
    /// Chrome hosts (VehicleForm → VehiclesView → VehicleManagementView) and document windows
    /// (PdfPreviewWindow) load with a null DataContext by design. Warn only when a feature
    /// UserControl has neither its own nor a descendant view-model.
    /// </summary>
    public static LogEventLevel ClassifyDataContextLevel(
        object? dataContext,
        bool descendantHasDataContext,
        bool isWindow,
        bool isEmbeddedControl = false)
    {
        if (dataContext is not null || descendantHasDataContext || isWindow || isEmbeddedControl)
        {
            return LogEventLevel.Information;
        }

        return LogEventLevel.Warning;
    }

    /// <summary>Register class handlers once during startup.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(UserControl),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSurfaceLoaded),
            true);
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSurfaceLoaded),
            true);

        UiDiagnosticsLog.Write(
            Logger,
            LogEventLevel.Information,
            "UI surface probe registered — Loaded UserControl/Window in {Namespace} will be inspected",
            BusBuddyNamespacePrefix);
    }

    private static void OnSurfaceLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement root || !IsBusBuddySurface(root.GetType()))
        {
            return;
        }

        try
        {
            Probed.Add(root, ProbedMarker);
        }
        catch (ArgumentException)
        {
            return;
        }

        LogSurface(root, "Loaded");
        root.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            try
            {
                InspectBindingsAndSources(root);
            }
            catch (Exception ex)
            {
                UiDiagnosticsLog.Write(
                    Logger,
                    LogEventLevel.Warning,
                    ex,
                    "UI surface idle inspect failed View={View}",
                    root.GetType().Name);
            }
        });
    }

    private static void LogSurface(FrameworkElement root, string phase)
    {
        var dc = DescribeDataContext(root.DataContext);
        var level = ClassifyDataContextLevel(
            root.DataContext,
            HasDescendantDataContext(root),
            root is Window,
            IsEmbeddedControl(root.GetType()));
        UiDiagnosticsLog.Write(
            Logger,
            level,
            "UI surface {Phase} View={View} DataContext={DataContext} ActualSize={Width:F0}x{Height:F0}",
            phase,
            root.GetType().Name,
            dc,
            root.ActualWidth,
            root.ActualHeight);
    }

    private static bool HasDescendantDataContext(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.DataContext is not null)
            {
                return true;
            }

            if (HasDescendantDataContext(child))
            {
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<string> InspectBindingsAndSources(FrameworkElement root)
    {
        var warnings = new List<string>();
        var emptySources = new List<string>();
        Walk(root, 0, warnings, emptySources);

        foreach (var finding in warnings)
        {
            UiDiagnosticsLog.Write(Logger, LogEventLevel.Warning, "{Finding} View={View}", finding, root.GetType().Name);
        }

        foreach (var empty in emptySources)
        {
            UiDiagnosticsLog.Write(
                Logger,
                LogEventLevel.Information,
                "UI empty source {Finding} View={View}",
                empty,
                root.GetType().Name);
        }

        if (warnings.Count == 0)
        {
            UiDiagnosticsLog.Write(
                Logger,
                LogEventLevel.Information,
                "UI surface idle inspect View={View} DataContext={DataContext} BindingErrors=0 EmptySources={EmptyCount}",
                root.GetType().Name,
                DescribeDataContext(root.DataContext),
                emptySources.Count);
        }

        return warnings;
    }

    private static void Walk(DependencyObject current, int visited, List<string> warnings, List<string> emptySources)
    {
        if (visited > MaxVisualNodes)
        {
            return;
        }

        if (current is FrameworkElement fe)
        {
            CollectElementFindings(fe, warnings, emptySources);
        }

        var count = VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            Walk(child, visited + 1, warnings, emptySources);
            if (warnings.Count > 40)
            {
                return;
            }
        }
    }

    private static void CollectElementFindings(FrameworkElement fe, List<string> warnings, List<string> emptySources)
    {
        CheckBinding(fe, FrameworkElement.DataContextProperty, warnings);
        CheckBinding(fe, ItemsControl.ItemsSourceProperty, warnings);
        CheckBinding(fe, Selector.SelectedItemProperty, findings: warnings);
        CheckBinding(fe, Selector.SelectedValueProperty, findings: warnings);
        CheckBinding(fe, TextBox.TextProperty, findings: warnings);
        CheckBinding(fe, ButtonBase.CommandProperty, findings: warnings);

        if (fe is ButtonBase button)
        {
            var commandBinding = BindingOperations.GetBinding(button, ButtonBase.CommandProperty);
            if (commandBinding is not null && button.Command is null)
            {
                warnings.Add($"Command unbound Name={NameOf(fe)} Path={commandBinding.Path?.Path}");
            }
        }

        if (TryGetItemsSource(fe, out var source, out var kind) && IsEmptySource(source))
        {
            var named = !string.IsNullOrEmpty(fe.Name);
            if (named || fe is SfDataGrid || fe is ComboBoxAdv)
            {
                emptySources.Add($"{kind} ItemsSource empty Name={NameOf(fe)} Type={fe.GetType().Name}");
            }
        }

        if (fe is SfMap map && map.Layers.Count == 0)
        {
            warnings.Add($"SfMap has no layers Name={NameOf(fe)}");
        }
    }

    private static void CheckBinding(FrameworkElement fe, DependencyProperty property, List<string> findings)
    {
        var expr = BindingOperations.GetBindingExpression(fe, property);
        if (expr is { HasError: true })
        {
            findings.Add(
                $"Binding error Name={NameOf(fe)} Property={property.Name} Path={expr.ParentBinding?.Path?.Path} Status={expr.Status}");
        }
    }

    private static bool TryGetItemsSource(FrameworkElement fe, out object? source, out string kind)
    {
        switch (fe)
        {
            case SfDataGrid grid:
                source = grid.ItemsSource;
                kind = "SfDataGrid";
                return true;
            case ComboBoxAdv combo:
                source = combo.ItemsSource;
                kind = "ComboBoxAdv";
                return true;
            case ItemsControl items:
                source = items.ItemsSource;
                kind = items.GetType().Name;
                return true;
            default:
                source = null;
                kind = string.Empty;
                return false;
        }
    }

    private static bool IsEmptySource(object? source)
    {
        if (source is null)
        {
            return true;
        }

        if (source is ICollection collection)
        {
            return collection.Count == 0;
        }

        if (source is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            try
            {
                return !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        return false;
    }

    private static string NameOf(FrameworkElement fe) =>
        string.IsNullOrEmpty(fe.Name) ? "(unnamed)" : fe.Name;
}
