using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using Serilog; // Serilog per project standards
using BusBuddy.WPF.ViewModels.Driver;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Enhanced Drivers View with proper ViewModel integration
    /// </summary>
    public partial class DriversView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<DriversView>();

        public DriversView()
        {
            Logger.Debug("DriversView ctor start");
            try
            {
                InitializeComponent();

                // Set the ViewModel for data binding (simple instantiation Phase 1)
                DataContext = new DriversViewModel();

                // Apply Syncfusion theme
                SyncfusionThemeManager.ApplyTheme(this);

                // Attach bubbling interaction diagnostics (#documentation-first pattern based on WPF routed events)
                try
                {
                    AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                    AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true);
                    AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                    AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "DriversView: failed to attach global handlers");
                }

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;

                Logger.Information("DriversView initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "DriversView initialization failed");
                throw;
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("DriversView Loaded");
            try { AuditButtonsAccessibility(); } catch (Exception ex) { Logger.Warning(ex, "DriversView: accessibility audit failed"); }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("DriversView Unloaded — cleaning up");
            try
            {
                Loaded -= OnLoaded; Unloaded -= OnUnloaded;
                RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));
                RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged));
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
                    Logger.Information("Drivers ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("Drivers Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("Drivers Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "DriversView: button logging failed");
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
                Logger.Information("Drivers SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "DriversView: selection logging failed");
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
                Logger.Information("Drivers TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "DriversView: text logging failed");
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
                Logger.Warning("Drivers Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "DriversView: validation logging failed");
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
                        Logger.Warning("Drivers Audit — ButtonAdv missing label and AutomationProperties.Name: {Name}", (badv as FrameworkElement)?.Name ?? "(unnamed)");
                }
                else if (d is Button btn)
                {
                    total++;
                    var content = btn.Content?.ToString(); var autoName = AutomationProperties.GetName(btn);
                    bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("Drivers Audit — Button missing Content and AutomationProperties.Name: {Name}", btn.Name ?? "(unnamed)");
                }
            }
            Logger.Information("Drivers Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
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
