using System;
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
        Assert.That(xaml, Does.Contain("ZoomLevel=\"{Binding MapZoomLevel, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding MapLayers}\""));
        Assert.That(xaml, Does.Not.Contain("MapLayerComboBox_SelectionChanged"));
        Assert.That(xaml, Does.Not.Contain("ZoomLevel=\"13\""));
        Assert.That(xaml, Does.Contain("IsChecked=\"{Binding IsLiveTrackingEnabled, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Live tracking\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Tracking update frequency\""));
        Assert.That(CountOccurrences(xaml, "IsEnabled=\"False\""), Is.GreaterThanOrEqualTo(2),
            "Live tracking ButtonAdv and interval ComboBox must stay disabled until fleet GPS is wired.");
        Assert.That(xaml, Does.Not.Contain("FluentDarkTheme.xaml"));
        Assert.That(xaml, Does.Not.Contain("#AA2B2B2B"));
    }

    [Test]
    public void MapViewLauncher_PrefersActiveWindowSoModalStudentsCannotHideTheMap()
    {
        var launcher = XamlViewFile.Read("Utilities/MapViewLauncher.cs");
        Assert.That(launcher, Does.Contain("ResolveOwner"));
        Assert.That(launcher, Does.Contain("DialogOwner.Resolve"));
        Assert.That(launcher, Does.Contain("BringToFront"));
        Assert.That(launcher, Does.Contain("ShowActivated = true"));
        var owner = XamlViewFile.Read("Utilities/DialogOwner.cs");
        Assert.That(owner, Does.Contain("IsActive"));
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

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = source.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            start = index + value.Length;
        }
    }
}
