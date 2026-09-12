using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteOptimizationSurfaceTests
{
    [Test]
    public void RouteManagement_WiresOptimizeStopOrder()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteManagementView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding OptimizeStopOrderCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Optimize stop order\""));

        var vm = XamlViewFile.Read("ViewModels/Route/RouteManagementViewModel.cs");
        Assert.That(vm, Does.Contain("IRouteOptimizationService"));
        Assert.That(vm, Does.Contain("OptimizeStopOrderAsync"));
        Assert.That(vm, Does.Contain("ForPinnedEnds"));
    }

    [Test]
    public void TripBoard_WiresOptimizeDayAndStaysOffDailyRoutes()
    {
        var xaml = XamlViewFile.Read("Views/Activity/ActivityManagementView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding OptimizeDayCommand}\""));
        Assert.That(xaml, Does.Contain("Trip Board"));
        Assert.That(xaml, Does.Not.Contain("Regular Route"));

        var vm = XamlViewFile.Read("ViewModels/Activity/ActivityManagementViewModel.cs");
        Assert.That(vm, Does.Contain("SuggestSameDayFleetAsync"));
        Assert.That(vm, Does.Contain("Route != Trip"));
    }

    [Test]
    public void CoreClient_UsesOptimizeToursEndpoint()
    {
        var client = CoreSourceFile.Read("Services/GoogleMaps/GoogleRouteOptimizationService.cs");
        Assert.That(client, Does.Contain("routeoptimization.googleapis.com/v1/projects/"));
        Assert.That(client, Does.Contain("optimizeTours"));
        Assert.That(client, Does.Contain("costPerHour"));
        Assert.That(client, Does.Not.Contain("IsTrip"));
    }

    [Test]
    public void GenerateRoutes_UsesPmDropoffTopology_AndOptimizeDayChecksConflicts()
    {
        var gen = CoreSourceFile.Read("Services/RouteDetermination/RouteDeterminationService.cs");
        Assert.That(gen, Does.Contain("ForDropoffRun"));
        Assert.That(gen, Does.Contain("ForPickupRun"));
        Assert.That(gen, Does.Contain("RouteTimeSlotKind.PM"));

        var trips = CoreSourceFile.Read("Services/TripEventService.cs");
        Assert.That(trips, Does.Contain("HasConflictsAsync"));
        Assert.That(trips, Does.Contain("skippedConflicts"));
        Assert.That(trips, Does.Contain("unassigned"));
        Assert.That(trips, Does.Contain("Pickup times were not changed"));
    }
}
