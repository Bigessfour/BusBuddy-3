using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Syncfusion.SfSkinManager; // Syncfusion WPF Theming — see official docs: https://help.syncfusion.com/wpf/themes/overview
using BusBuddy.WPF.ViewModels.Route;
using CommunityToolkit.Mvvm.Input; // IAsyncRelayCommand

namespace BusBuddy.WPF.Views.Route
{
    /// <summary>
    /// Phase 2 Route Management View
    /// Enhanced route planning and management interface
    /// </summary>
    public partial class RouteManagementView : UserControl
    {
    // Ensure Serilog per standards — https://learn.microsoft.com/dotnet/core/diagnostics/serilog-logging
    private static readonly ILogger Logger = Log.ForContext<RouteManagementView>();
    private bool _isDataReady;
    private DateTime _loadStartedUtc;
    private bool _auditRun;

        public RouteManagementView()
        {
            Logger.Debug("RouteManagementView ctor start");
        try
        {
            InitializeComponent();

            // Ensure DataContext is set so bindings/commands are active
            try
            {
                if (this.DataContext is null)
                {
                    this.DataContext = new RouteManagementViewModel();
                    Logger.Information("RouteManagementView DataContext set to RouteManagementViewModel");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to set DataContext for RouteManagementView");
            }

            // Add async lifecycle handlers after InitializeComponent
            Loaded += OnLoadedAsync;
            // Only run audit once, not on Unloaded

            // Attach bubbling interaction diagnostics
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                AddHandler(Selector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnAnySelectionChanged), true);
                AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteManagementView: failed to attach global handlers");
            }

            Logger.Information("RouteManagementView initialized");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "RouteManagementView initialization failed");
            throw;
        }
        }

        // Microsoft docs — FrameworkElement.Loaded/Unloaded events:
        // https://learn.microsoft.com/dotnet/api/system.windows.frameworkelement.loaded
        // https://learn.microsoft.com/dotnet/api/system.windows.frameworkelement.unloaded
        // Syncfusion modal best practice — host UserControl inside ChromelessWindow and call ShowDialog:
        // https://help.syncfusion.com/wpf/chromeless-window/getting-started

        private async void OnLoadedAsync(object sender, RoutedEventArgs e)
        {
            if (!_auditRun)
            {
                AuditButtonsAccessibility();
                _auditRun = true;
            }
            _loadStartedUtc = DateTime.UtcNow;
            var vm = DataContext;

            try
            {
                // Preferred: await a documented async command exposed by the ViewModel
                if (vm is not null &&
                    vm.GetType().GetProperty("RefreshCommand")?.GetValue(vm) is IAsyncRelayCommand asyncCmd)
                {
                    await asyncCmd.ExecuteAsync(null);
                }
                else
                {
                    // Fallback: reflectively await LoadRoutesAsync if present (public or non-public)
                    var mi = vm?.GetType().GetMethod(
                        "LoadRoutesAsync",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);

                    if (mi != null && typeof(Task).IsAssignableFrom(mi.ReturnType))
                    {
                        var task = (Task)mi.Invoke(vm, null)!;
                        await task.ConfigureAwait(true);
                    }
                }

                _isDataReady = true;

                var elapsedMs = (DateTime.UtcNow - _loadStartedUtc).TotalMilliseconds;
                Logger.Information("RouteManagementView data ready — time-to-modal-ready {ElapsedMs} ms", elapsedMs);

                // Safe point to show a modal dialog that hosts this view or a child UserControl.
                // Syncfusion guidance: host UserControls in a ChromelessWindow and call ShowDialog after data readiness.
                // Example (leave modal creation where it belongs):
                // var win = new Syncfusion.Windows.Tools.Controls.ChromelessWindow { Content = new RouteDialogContent() };
                // win.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed during RouteManagementView data preload");
            }
        }

        // Based on Syncfusion WPF SfDataGrid row interaction pattern — using MouseDoubleClick to trigger Manage Route
        // API ref: https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html
        private void RoutesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (DataContext is RouteManagementViewModel vm && vm.AssignStudentsCommand.CanExecute(null))
                {
                    vm.AssignStudentsCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Double-click manage route failed");
            }
        }

        private void OnUnloadedGuard(object? sender, RoutedEventArgs e)
        {
            if (!_isDataReady)
            {
                // Prevent premature unload while data is not ready (per request)
                e.Handled = true;
                Logger.Warning("Prevented premature unload of RouteManagementView — data not ready");
            }
        }

        private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? "(unknown)";

                if (src is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    bool? canExec = null; try { if (badv.Command != null) canExec = badv.Command.CanExecute(badv.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(badv);
                    Logger.Information("RouteMgmt ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}",
                        name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("RouteMgmt Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}",
                        name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("RouteMgmt Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteManagementView: button click logging failed");
            }
        }

    private void OnAnySelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
        var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Information("RouteMgmt SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteManagementView: selection logging failed");
            }
        }

        private void OnAnyTextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is not DependencyObject src) return;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src.GetType().Name;
                int? len = src is TextBox tb ? tb.Text?.Length : null;
                Logger.Information("RouteMgmt TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteManagementView: text logging failed");
            }
        }

        private void OnValidationError(object? sender, ValidationErrorEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Warning("RouteMgmt Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteManagementView: validation logging failed");
            }
        }

        private void AuditButtonsAccessibility()
        {
            int total = 0, adv = 0, missingLabel = 0, missingAuto = 0, noCmd = 0;
            var queue = new System.Collections.Generic.Queue<DependencyObject>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                var d = queue.Dequeue();
                int count = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(d, i);
                    if (child != null) queue.Enqueue(child);
                }
                if (d is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    total++; adv++;
                    var label = badv.Label; var autoName = AutomationProperties.GetName(badv);
                    bool hasCmd = badv.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(label)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
#if DEBUG
                    if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("RouteMgmt Audit — ButtonAdv missing label and AutomationProperties.Name: {Name}", (badv as FrameworkElement)?.Name ?? "(unnamed)");
#endif
                }
                else if (d is Button btn)
                {
                    total++;
                    var content = btn.Content?.ToString(); var autoName = AutomationProperties.GetName(btn);
                    bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
#if DEBUG
                    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("RouteMgmt Audit — Button missing Content and AutomationProperties.Name: {Name}", btn.Name ?? "(unnamed)");
#endif
                }
            }
            Logger.Information("RouteMgmt Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
        }

    // Traverse method removed; replaced with queue-based traversal in AuditButtonsAccessibility
    }
}
