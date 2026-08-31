using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DriverScheduleViewTests
{
    [Test]
    public void DriverScheduleViewXaml_WiresRefreshAndScheduler()
    {
        var xaml = XamlViewFile.Read("Views/Driver/DriverScheduleView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("SfScheduler"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Appointments}\""));
    }
}
