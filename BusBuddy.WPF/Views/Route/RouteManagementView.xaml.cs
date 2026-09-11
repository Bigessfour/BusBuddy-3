using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.WPF.ViewModels.Route;

namespace BusBuddy.WPF.Views.Route;

/// <summary>
/// Route Management chrome — DataContext, Loaded init, double-click open assignment.
/// </summary>
public partial class RouteManagementView : UserControl
{
    private static readonly ILogger Logger = Log.ForContext<RouteManagementView>();
    private DateTime _loadStartedUtc;
    private bool _auditRun;

    public RouteManagementView()
    {
        Logger.Debug("RouteManagementView ctor start");
        try
        {
            InitializeComponent();

            try
            {
                if (DataContext is null)
                {
                    DataContext = App.ServiceProvider.GetRequiredService<RouteManagementViewModel>();
                    Logger.Information("RouteManagementView DataContext resolved from DI");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to set DataContext for RouteManagementView");
            }

            Loaded += OnLoadedAsync;
            Logger.Information("RouteManagementView initialized");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "RouteManagementView initialization failed");
            throw;
        }
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (!_auditRun)
        {
            AuditButtonsAccessibility();
            _auditRun = true;
        }

        _loadStartedUtc = DateTime.UtcNow;

        try
        {
            if (DataContext is RouteManagementViewModel routeVm)
            {
                await routeVm.InitializeAsync().ConfigureAwait(true);
            }

            var elapsedMs = (DateTime.UtcNow - _loadStartedUtc).TotalMilliseconds;
            Logger.Information("RouteManagementView data ready — time-to-modal-ready {ElapsedMs} ms", elapsedMs);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed during RouteManagementView data preload");
        }
    }

    private void RoutesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (DataContext is RouteManagementViewModel vm && vm.OpenRouteAssignmentCommand.CanExecute(null))
            {
                vm.OpenRouteAssignmentCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Double-click manage route failed");
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
                if (child != null)
                {
                    queue.Enqueue(child);
                }
            }

            if (d is ButtonAdv badv)
            {
                total++;
                adv++;
                var label = badv.Label;
                var autoName = AutomationProperties.GetName(badv);
                if (badv.Command is null)
                {
                    noCmd++;
                }

                if (string.IsNullOrWhiteSpace(label))
                {
                    missingLabel++;
                }

                if (string.IsNullOrWhiteSpace(autoName))
                {
                    missingAuto++;
                }
            }
            else if (d is Button btn)
            {
                total++;
                if (btn.Command is null)
                {
                    noCmd++;
                }

                if (string.IsNullOrWhiteSpace(btn.Content?.ToString()))
                {
                    missingLabel++;
                }

                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(btn)))
                {
                    missingAuto++;
                }
            }
        }

        Logger.Information(
            "RouteMgmt Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}",
            total, adv, missingLabel, missingAuto, noCmd);
    }
}
