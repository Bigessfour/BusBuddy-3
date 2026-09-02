using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DashboardViewTests
{
    [Test]
    public void DashboardViewXaml_WiresRefreshAndReportCommands()
    {
        var xaml = XamlViewFile.Read("Views/Dashboard/DashboardView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateReportCommand}\""));
        Assert.That(xaml, Does.Contain("ShowTooltip=\"True\""));
        Assert.That(xaml, Does.Contain("ChartAdornmentInfo"));
        Assert.That(xaml, Does.Contain("ChartLegend"));
        Assert.That(xaml, Does.Contain("LabelPosition=\"OutsideExtended\""));
        Assert.That(xaml, Does.Contain("IsBusy=\"{Binding IsLoading}\""));
    }
}
