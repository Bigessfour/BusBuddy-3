using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using BusBuddy.WPF.ViewModels.Route;
using Serilog;
using Syncfusion.SfSkinManager;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services;

namespace BusBuddy.WPF.Views.Route
{
    /// <summary>
    /// Interaction logic for RouteAssignmentView.xaml
    /// MVP-ready route assignment interface with drag-drop functionality
    /// Supports comprehensive route building workflow with Syncfusion integration
    /// </summary>
    public partial class RouteAssignmentView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<RouteAssignmentView>();

    public RouteAssignmentView()
        {
            Logger.Debug("RouteAssignmentView constructor starting");
            try
            {
                Logger.Debug("Initializing RouteAssignmentView XAML components");
                InitializeComponent();

                Logger.Debug("Setting DataContext to RouteAssignmentViewModel");
                RouteAssignmentViewModel viewModel;
                try
                {
                    var sp = App.ServiceProvider;
                    if (sp != null)
                    {
                        var routeService = sp.GetService<IRouteService>();
                        viewModel = new RouteAssignmentViewModel(routeService);
                    }
                    else
                    {
                        viewModel = new RouteAssignmentViewModel();
                    }
                }
                catch
                {
                    viewModel = new RouteAssignmentViewModel();
                }
                DataContext = viewModel;

                Logger.Information("RouteAssignmentView initialized successfully with MVP route building interface");
                Loaded += OnLoaded;
                Unloaded += OnUnloaded;

                // Attach bubbling interaction diagnostics (adapted from other views)
                try
                {
                    AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                    AddHandler(Selector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnAnySelectionChanged), true);
                    AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                    AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                }
                catch (Exception exAttach)
                {
                    Logger.Warning(exAttach, "RouteAssignmentView: failed to attach interaction handlers");
                }

                Logger.Debug("RouteAssignmentView constructor completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize RouteAssignmentView - Critical MVP component failure");
                throw; // Re-throw to maintain application stability
            }
        }

        /// <summary>
        /// Overload allowing caller to provide a pre-selected route (used when invoked from RouteManagementView)
        /// </summary>
        /// <param name="preselectedRoute">Route to preselect in assignment UI</param>
        public RouteAssignmentView(BusBuddy.Core.Models.Route preselectedRoute) : this()
        {
            try
            {
                if (DataContext is RouteAssignmentViewModel vm)
                {
                    // Replace DataContext with one that has preselected route so initial load can pick it
                    var sp = App.ServiceProvider;
                    IRouteService? routeService = null;
                    try { routeService = sp?.GetService<IRouteService>(); } catch { }
                    DataContext = new RouteAssignmentViewModel(routeService, preselectedRoute);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply preselected route in RouteAssignmentView");
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // Apply theme to containing Window for consistent styling
                try
                {
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        // Prefer centralized Syncfusion theme application to ensure consistency for modal dialogs
                        BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(window);
                    }
                }
                catch
                {
                    // Fallback to direct theme application if utility path fails
                    try
                    {
                        var window = Window.GetWindow(this);
                        if (window != null)
                        {
                            SfSkinManager.ApplyThemeAsDefaultStyle = true;
                            using var dark = new Theme("FluentDark");
                            SfSkinManager.SetTheme(window, dark);
                        }
                    }
                    catch
                    {
                        try
                        {
                            var window = Window.GetWindow(this);
                            if (window != null)
                            {
                                using var light = new Theme("FluentLight");
                                SfSkinManager.SetTheme(window, light);
                            }
                        }
                        catch { }
                    }
                }

                Logger.Information("Loaded {ViewName} with theme resource {ResourceKey}", GetType().Name, "BusBuddy.Brush.Primary");

                // Run a lightweight accessibility/audit pass for buttons/labels
                try { AuditButtonsAccessibility(); } catch { }
            }
            catch { }
        }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Detach handlers — cleanup
            try
            {
                RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));
                RemoveHandler(Selector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnAnySelectionChanged));
                RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged));
                RemoveHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError));
            }
            catch { }
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
                    Logger.Information("RouteAssign ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("RouteAssign Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("RouteAssign Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteAssignmentView: button logging failed");
            }
        }

        private void OnAnySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Information("RouteAssign SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteAssignmentView: selection logging failed");
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
                Logger.Information("RouteAssign TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteAssignmentView: text logging failed");
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
                Logger.Warning("RouteAssign Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RouteAssignmentView: validation logging failed");
            }
        }

        private void AuditButtonsAccessibility()
        {
            int total = 0, adv = 0, missingLabel = 0, missingAuto = 0, noCmd = 0;
            foreach (var d in Traverse(this))
            {
                if (d is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    total++; adv++;
                    var label = badv.Label; var autoName = AutomationProperties.GetName(badv);
                    bool hasCmd = badv.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(label)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("RouteAssign Audit — ButtonAdv missing label and AutomationProperties.Name: {Name}", (badv as FrameworkElement)?.Name ?? "(unnamed)");
                }
                else if (d is Button btn)
                {
                    total++;
                    var content = btn.Content?.ToString(); var autoName = AutomationProperties.GetName(btn);
                    bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("RouteAssign Audit — Button missing Content and AutomationProperties.Name: {Name}", btn.Name ?? "(unnamed)");
                }
            }
            Logger.Information("RouteAssign Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Traverse(DependencyObject root)
        {
            if (root == null) yield break;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child == null) continue;
                yield return child;
                foreach (var g in Traverse(child)) yield return g;
            }
        }
    }
}
