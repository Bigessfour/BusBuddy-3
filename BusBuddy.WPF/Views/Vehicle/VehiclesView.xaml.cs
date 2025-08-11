using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using BusBuddy.WPF.ViewModels.Vehicle;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehiclesView.xaml
    /// Phase 1: Simple ViewModel binding
    /// </summary>
    public partial class VehiclesView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<VehiclesView>();
        public VehiclesView()
        {
            Logger.Debug("VehiclesView ctor start");
            try
            {
                InitializeComponent();
                DataContext = new VehiclesViewModel();

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;

                try
                {
                    AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                    AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true);
                    AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                    AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "VehiclesView: failed to attach handlers");
                }

                Logger.Information("VehiclesView initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "VehiclesView initialization failed");
                throw;
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("VehiclesView Loaded — DataContext={DC}", DataContext?.GetType().Name ?? "(null)");
            try { Dispatcher.BeginInvoke(new Action(AuditButtonsAccessibility), DispatcherPriority.Loaded); } catch (Exception ex) { Logger.Warning(ex, "VehiclesView: audit scheduling failed"); }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("VehiclesView Unloaded — cleaning up");
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
                    Logger.Information("Vehicles ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("Vehicles Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("Vehicles Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehiclesView: button logging failed");
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
                Logger.Information("Vehicles SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehiclesView: selection logging failed");
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
                Logger.Information("Vehicles TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehiclesView: text logging failed");
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
                Logger.Warning("Vehicles Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehiclesView: validation logging failed");
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
                        Logger.Warning("Vehicles Audit — ButtonAdv missing label and AutomationProperties.Name: {Name}", (badv as FrameworkElement)?.Name ?? "(unnamed)");
                }
                else if (d is Button btn)
                {
                    total++;
                    var content = btn.Content?.ToString(); var autoName = AutomationProperties.GetName(btn);
                    bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(autoName))
                        Logger.Warning("Vehicles Audit — Button missing Content and AutomationProperties.Name: {Name}", btn.Name ?? "(unnamed)");
                }
            }
            Logger.Information("Vehicles Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
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
