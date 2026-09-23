using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class AnalyticsDashboardViewTests
{
    [Test]
    public void AnalyticsDashboardViewXaml_WiresDocumentedChartAndBusyIndicatorApis()
    {
        var xaml = XamlViewFile.Read("Views/Analytics/AnalyticsDashboardView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("ShowTooltip=\"True\""));
        Assert.That(xaml, Does.Contain("ChartAdornmentInfo"));
        Assert.That(xaml, Does.Contain("LabelPosition=\"OutsideExtended\""));
        Assert.That(xaml, Does.Contain("ChartLegend"));
        Assert.That(xaml, Does.Contain("OpposedPosition=\"True\""));
        Assert.That(xaml, Does.Contain("IsBusy=\"{Binding IsLoading}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FuelGallons}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FuelRecords}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding StudentAssignments}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Fleet performance chart\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Student assignments chart\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Maintenance metrics chart\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Fuel analytics chart\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Analytics loading\""));
        Assert.That(xaml, Does.Contain("BusBuddy.Brush.Overlay.Dim"));
        Assert.That(xaml, Does.Not.Contain("#80000000"));
    }
}
