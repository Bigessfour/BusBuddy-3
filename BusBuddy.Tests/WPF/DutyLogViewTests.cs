using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DutyLogViewTests
{
    [Test]
    public void DutyLogViewXaml_WiresCommandsAndAddForm()
    {
        var xaml = XamlViewFile.Read("Views/Duty/DutyLogView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Duty driver\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Duty bus\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Duty session\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Duty event type\""));
        Assert.That(xaml, Does.Contain("SelectedValue=\"{Binding DraftDriverId, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedValue=\"{Binding DraftBusId, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding DraftSession, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding DraftEventType, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Events}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding MissingPreTrips}\""));
    }

    [Test]
    public void MainWindowXaml_ExposesDutyLogRibbon()
    {
        var xaml = XamlViewFile.Read("Views/Main/MainWindow.xaml");
        Assert.That(xaml, Does.Contain("Click=\"DutyLogButton_Click\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Duty Log\""));
    }
}
