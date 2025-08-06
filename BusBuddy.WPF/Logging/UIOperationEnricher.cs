using Serilog.Core;
using Serilog.Events;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// UI operation enricher that adds context for WPF user interface operations.
    /// Tracks UI interactions, view navigation, control events, and threading context.
    /// Based on best practices for WPF application monitoring and debugging.
    /// </summary>
    public class UIOperationEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Check if this is a UI-related log event
            var messageTemplate = logEvent.MessageTemplate.Text;
            var isUILog = messageTemplate.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("View", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Window", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Click", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Navigate", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Loading", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Dashboard", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("ViewModel", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("MainWindow", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Control", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Theme", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase);

            if (isUILog)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsUIOperation", true));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIContext", "BusBuddyWPF"));

                // Add thread context - critical for WPF applications
                var isUIThread = Application.Current?.Dispatcher?.CheckAccess() == true;
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsUIThread", isUIThread));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));

                if (isUIThread)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadType", "UIThread"));
                }
                else
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadType", "BackgroundThread"));
                }

                // Add UI operation type context
                if (messageTemplate.Contains("navigate", StringComparison.OrdinalIgnoreCase) ||
                    messageTemplate.Contains("navigation", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "Navigation"));
                }
                else if (messageTemplate.Contains("click", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("button", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "UserInteraction"));
                }
                else if (messageTemplate.Contains("loading", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("load", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "Loading"));
                }
                else if (messageTemplate.Contains("window", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("show", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("hide", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "WindowManagement"));
                }
                else if (messageTemplate.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("style", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "Styling"));
                }
                else if (messageTemplate.Contains("viewmodel", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("binding", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("property", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "DataBinding"));
                }
                else if (messageTemplate.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
                         messageTemplate.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIOperationType", "Validation"));
                }

                // Add view context if we can determine the current view
                if (messageTemplate.Contains("dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CurrentView", "Dashboard"));
                }
                else if (messageTemplate.Contains("loading", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CurrentView", "Loading"));
                }
                else if (messageTemplate.Contains("main", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CurrentView", "Main"));
                }

                // Enhanced Syncfusion control detection and properties
                if (messageTemplate.Contains("syncfusion", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIFramework", "Syncfusion"));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsThirdPartyControl", true));

                    // Detect specific Syncfusion control types and add relevant properties
                    DetectSyncfusionControlType(messageTemplate, logEvent, propertyFactory);
                }
                else
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIFramework", "NativeWPF"));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsThirdPartyControl", false));
                }

                // Add dispatcher priority context if available (useful for performance analysis)
                try
                {
                    if (Application.Current?.Dispatcher != null)
                    {
                        var dispatcher = Application.Current.Dispatcher;
                        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherHasShutdownStarted",
                            dispatcher.HasShutdownStarted));
                        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherHasShutdownFinished",
                            dispatcher.HasShutdownFinished));
                    }
                }
                catch
                {
                    // Ignore dispatcher access errors - they can happen during shutdown
                }
            }

            // Add startup context for UI initialization
            if (messageTemplate.Contains("STARTUP", StringComparison.OrdinalIgnoreCase) && isUILog)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsStartupUIOperation", true));
            }

            // Add performance context for timing-related UI operations
            if (messageTemplate.Contains("ms", StringComparison.OrdinalIgnoreCase) ||
                messageTemplate.Contains("millisecond", StringComparison.OrdinalIgnoreCase) ||
                messageTemplate.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsPerformanceMetric", true));
            }
        }

        /// <summary>
        /// Detects specific Syncfusion control types and adds relevant properties for enhanced logging
        /// </summary>
        private static void DetectSyncfusionControlType(string messageTemplate, LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Chart Controls
            if (messageTemplate.Contains("SfChart", StringComparison.OrdinalIgnoreCase) ||
                messageTemplate.Contains("chart", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfChart"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "DataVisualization"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "Series,Axes,Legend,Tooltip,Zoom"));
            }

            // Grid Controls
            else if (messageTemplate.Contains("SfDataGrid", StringComparison.OrdinalIgnoreCase) ||
                     messageTemplate.Contains("datagrid", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfDataGrid"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "GridControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "Sorting,Filtering,Grouping,Editing,Export"));
            }

            // Button Controls
            else if (messageTemplate.Contains("ButtonAdv", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "ButtonAdv"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ButtonControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "CornerRadius,CustomContent,Theming"));
            }
            else if (messageTemplate.Contains("DropDownButtonAdv", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "DropDownButtonAdv"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ButtonControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "DropDown,CustomContent,Theming"));
            }
            else if (messageTemplate.Contains("SplitButtonAdv", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SplitButtonAdv"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ButtonControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "SplitAction,DropDown,Theming"));
            }

            // Input Controls
            else if (messageTemplate.Contains("SfTextInputLayout", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfTextInputLayout"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "InputControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "FloatingLabel,Validation,Theming"));
            }
            else if (messageTemplate.Contains("SfMaskedEdit", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfMaskedEdit"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "InputControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "InputMask,Validation,Formatting"));
            }
            else if (messageTemplate.Contains("SfDatePicker", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfDatePicker"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "InputControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "DateSelection,Formatting,Validation"));
            }

            // Navigation Controls
            else if (messageTemplate.Contains("SfAccordion", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfAccordion"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NavigationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "Expandable,Collapsible,Theming"));
            }
            else if (messageTemplate.Contains("SfTabControl", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfTabControl"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NavigationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "TabSelection,Closable,Scrolling"));
            }
            else if (messageTemplate.Contains("SfBreadcrumb", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfBreadcrumb"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NavigationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "PathNavigation,Theming"));
            }

            // Layout Controls
            else if (messageTemplate.Contains("DockingManager", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "DockingManager"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "LayoutControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "Docking,FloatingWindows,AutoHidden,Tabbed"));
            }
            else if (messageTemplate.Contains("SfTileView", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfTileView"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "LayoutControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "TileArrangement,Maximizable,Theming"));
            }

            // Notification Controls
            else if (messageTemplate.Contains("SfHubTile", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfHubTile"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NotificationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "LiveTiles,Animation,Theming"));
            }
            else if (messageTemplate.Contains("SfBusyIndicator", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfBusyIndicator"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NotificationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "LoadingIndicator,Animation,Theming"));
            }

            // Progress Controls
            else if (messageTemplate.Contains("SfProgressBar", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfProgressBar"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ProgressControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "LinearProgress,Animation,Theming"));
            }
            else if (messageTemplate.Contains("SfCircularProgressBar", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfCircularProgressBar"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ProgressControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "CircularProgress,Animation,Theming"));
            }

            // Notification and Popups
            else if (messageTemplate.Contains("SfNotification", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfNotification"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "NotificationControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "ToastNotification,AutoClose,Theming"));
            }

            // Theming Controls
            else if (messageTemplate.Contains("SfSkinManager", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfSkinManager"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "ThemingControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "ThemeApplication,Office2019,FluentDesign"));

                // Add current theme context if available
                try
                {
                    var currentTheme = Syncfusion.SfSkinManager.SfSkinManager.GetTheme(Application.Current.MainWindow);
                    if (currentTheme != null)
                    {
                        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCurrentTheme", currentTheme.ToString()));
                    }
                }
                catch
                {
                    // Ignore theme detection errors
                }
            }

            // Gauge Controls
            else if (messageTemplate.Contains("SfCircularGauge", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfCircularGauge"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "GaugeControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "CircularGauge,Ranges,Pointers,Animation"));
            }
            else if (messageTemplate.Contains("SfLinearGauge", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfLinearGauge"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "GaugeControls"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "LinearGauge,Ranges,Pointers,Animation"));
            }

            // Maps and Diagram Controls
            else if (messageTemplate.Contains("SfMap", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfMap"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "DataVisualization"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "ShapeFiles,Markers,Legends,Zoom"));
            }
            else if (messageTemplate.Contains("SfDiagram", StringComparison.OrdinalIgnoreCase))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "SfDiagram"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "DataVisualization"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlFeatures", "Nodes,Connectors,Layout,Serialization"));
            }

            // Generic Syncfusion control if specific type not detected
            else
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionControlType", "GenericSyncfusionControl"));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionCategory", "Unknown"));
            }

            // Add Syncfusion version context
            try
            {
                var syncfusionAssembly = System.Reflection.Assembly.GetAssembly(typeof(Syncfusion.SfSkinManager.SfSkinManager));
                if (syncfusionAssembly != null)
                {
                    var version = syncfusionAssembly.GetName().Version?.ToString() ?? "Unknown";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SyncfusionVersion", version));
                }
            }
            catch
            {
                // Ignore version detection errors
            }
        }
    }
}
