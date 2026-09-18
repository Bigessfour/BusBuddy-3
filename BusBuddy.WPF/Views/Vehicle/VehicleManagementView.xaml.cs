using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Vehicle;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehicleManagementView.xaml
    /// Code-behind with ViewModel binding
    /// </summary>
    public partial class VehicleManagementView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<VehicleManagementView>();
        private readonly VehicleManagementStartup _startup;

        public VehicleManagementView() : this(VehicleManagementStartup.None)
        {
        }

        public VehicleManagementView(VehicleManagementStartup startup)
        {
            _startup = startup;
            Logger.Debug("VehicleManagementView ctor start Startup={Startup}", startup);
            try
            {
                InitializeComponent();
                // Ensure DataContext is set so Command bindings work
                try
                {
                    var busService = App.ServiceProvider.GetRequiredService<IBusService>();
                    this.DataContext = new VehicleManagementViewModel(busService);
                    Logger.Information("VehicleManagementView DataContext set to VehicleManagementViewModel");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to resolve IBusService for VehicleManagementViewModel. Commands may be disabled.");
                }
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
                    Logger.Warning(ex, "VehicleManagementView: failed to attach handlers");
                }

                Logger.Information("VehicleManagementView initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "VehicleManagementView initialization failed");
                throw;
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("VehicleManagementView Loaded");
            if (_startup != VehicleManagementStartup.None && DataContext is VehicleManagementViewModel vm)
            {
                _ = vm.ApplyStartupAsync(_startup);
            }

            try { Dispatcher.BeginInvoke(new Action(() => ButtonAccessibilityAudit.Run(this, Logger, "VehicleMgmt")), DispatcherPriority.Loaded); } catch (Exception ex) { Logger.Warning(ex, "VehicleManagementView: audit scheduling failed"); }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("VehicleManagementView Unloaded — cleaning up");
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
                    Logger.Information("VehicleMgmt ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("VehicleMgmt Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("VehicleMgmt Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehicleManagementView: button logging failed");
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
                Logger.Information("VehicleMgmt SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehicleManagementView: selection logging failed");
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
                Logger.Information("VehicleMgmt TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehicleManagementView: text logging failed");
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
                Logger.Warning("VehicleMgmt Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "VehicleManagementView: validation logging failed");
            }
        }

    }
}
