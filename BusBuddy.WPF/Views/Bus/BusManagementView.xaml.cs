using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Bus;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusManagementView.xaml
    /// Code-behind with ViewModel binding
    /// </summary>
    public partial class BusManagementView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<BusManagementView>();
        private readonly BusManagementStartup _startup;

        public BusManagementView() : this(BusManagementStartup.None)
        {
        }

        public BusManagementView(BusManagementStartup startup)
        {
            _startup = startup;
            Logger.Debug("BusManagementView ctor start Startup={Startup}", startup);
            try
            {
                InitializeComponent();
                // Ensure DataContext is set so Command bindings work
                try
                {
                    var busService = App.ServiceProvider.GetRequiredService<IBusService>();
                    this.DataContext = new BusManagementViewModel(busService);
                    Logger.Information("BusManagementView DataContext set to BusManagementViewModel");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to resolve IBusService for BusManagementViewModel. Commands may be disabled.");
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
                    Logger.Warning(ex, "BusManagementView: failed to attach handlers");
                }

                Logger.Information("BusManagementView initialized");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BusManagementView initialization failed");
                throw;
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("BusManagementView Loaded");
            if (_startup != BusManagementStartup.None && DataContext is BusManagementViewModel vm)
            {
                _ = vm.ApplyStartupAsync(_startup);
            }

            try { Dispatcher.BeginInvoke(new Action(() => ButtonAccessibilityAudit.Run(this, Logger, "BusMgmt")), DispatcherPriority.Loaded); } catch (Exception ex) { Logger.Warning(ex, "BusManagementView: audit scheduling failed"); }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("BusManagementView Unloaded — cleaning up");
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
                    Logger.Information("BusMgmt ButtonAdv: Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("BusMgmt Button: Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("BusMgmt Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "BusManagementView: button logging failed");
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
                Logger.Information("BusMgmt SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "BusManagementView: selection logging failed");
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
                Logger.Information("BusMgmt TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "BusManagementView: text logging failed");
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
                Logger.Warning("BusMgmt Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "BusManagementView: validation logging failed");
            }
        }

    }
}
