using System.Windows;
using BusBuddy.WPF.Views.Route;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>Opens the route assignment UI in a modal window (students, stops, bus/driver).</summary>
public static class RouteAssignmentLauncher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteAssignmentLauncher));

    public static bool? ShowDialog(Window? owner, BusBuddy.Core.Models.Route? preselectedRoute = null)
    {
        Logger.Information(
            "Opening route assignment dialog RouteId={RouteId} RouteName={RouteName}",
            preselectedRoute?.RouteId,
            preselectedRoute?.RouteName);

        var content = preselectedRoute is not null
            ? new RouteAssignmentView(preselectedRoute)
            : new RouteAssignmentView();

        var effectiveOwner = DialogOwner.Resolve(owner);
        var window = new Window
        {
            Title = preselectedRoute is not null
                ? $"Route Assignment — {preselectedRoute.RouteName}"
                : "Route Assignment",
            Content = content,
            Owner = effectiveOwner,
            Width = 1200,
            Height = 800,
            ShowActivated = true,
            WindowStartupLocation = effectiveOwner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
        };
        return window.ShowDialog();
    }
}
