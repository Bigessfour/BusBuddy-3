using System;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.Utilities;
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
            UiProofLog.Write(Logger, "Routes", "RouteManagementView", "initialized");
        }
        catch (Exception ex)
        {
            UiProofLog.Failed(Logger, ex, "Routes", "RouteManagementView");
            throw;
        }
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        if (!_auditRun)
        {
            ButtonAccessibilityAudit.Run(this, Logger, "RouteMgmt");
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

}
