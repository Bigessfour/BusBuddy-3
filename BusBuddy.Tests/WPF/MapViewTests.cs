using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapViewTests
{
    [Test]
    public void MapViewXaml_UsesSfMapAndPlotsStudentsInSystem()
    {
        var xaml = XamlViewFile.Read("Views/Map/MapView.xaml");
        Assert.That(xaml, Does.Contain("x:Class=\"BusBuddy.WPF.Views.Map.MapView\""));
        Assert.That(xaml, Does.Contain("maps:SfMap"));
        Assert.That(xaml, Does.Contain("Command=\"{Binding BulkPlotEligibleStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ShowSchoolsCommand}\""));
        Assert.That(xaml, Does.Not.Contain("GoogleEarth"));
        Assert.That(xaml, Does.Not.Contain("Wiley"));
        Assert.That(xaml, Does.Not.Contain("Add Stop (Demo)"));
        Assert.That(xaml, Does.Not.Contain("Demo Stop"));
        Assert.That(xaml, Does.Not.Contain("MVP"));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"CurrentLocation\""));
        Assert.That(xaml, Does.Contain("Content=\"Zoom In\""));
        Assert.That(xaml, Does.Contain("Content=\"Zoom Out\""));
        Assert.That(xaml, Does.Contain("EnableZoom=\"True\""));
        Assert.That(xaml, Does.Contain("EnablePan=\"True\""));
        Assert.That(xaml, Does.Contain("IsHitTestVisible=\"True\""));
        Assert.That(xaml, Does.Contain("Center=\"{Binding MapCenter, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Markers=\"{Binding MapMarkers}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedRoute, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Routes}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedMapLayer, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Not.Contain("FluentDarkTheme.xaml"));
        Assert.That(xaml, Does.Not.Contain("#AA2B2B2B"));
    }

    [Test]
    public void MapViewLauncher_PrefersActiveWindowSoModalStudentsCannotHideTheMap()
    {
        var launcher = XamlViewFile.Read("Utilities/MapViewLauncher.cs");
        Assert.That(launcher, Does.Contain("ResolveOwner"));
        Assert.That(launcher, Does.Contain("IsActive"));
        Assert.That(launcher, Does.Contain("BringToFront"));
        Assert.That(launcher, Does.Contain("ShowActivated = true"));
    }

    [Test]
    public void StudentsViewMapCommands_OpenDistrictMapWindow()
    {
        var xaml = XamlViewFile.Read("Views/Student/StudentsView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding ViewMapCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DataContext.ViewOnMapCommand"));

        var vm = XamlViewFile.Read("ViewModels/Student/StudentsViewModel.cs");
        Assert.That(vm, Does.Contain("MapViewLauncher.Show"));
        Assert.That(vm, Does.Contain("BulkPlotEligibleStudentsCommand"));
        Assert.That(vm, Does.Contain("District Map opened"));
    }
}
