using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MaintenanceViewTests
{
    [Test]
    public void MaintenanceViewXaml_WiresCrudCommands()
    {
        var xaml = XamlViewFile.Read("Views/Maintenance/MaintenanceView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding SaveCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
    }
}
