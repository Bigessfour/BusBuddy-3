using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteAssignmentViewTests
{
    [Test]
    public void RouteAssignmentViewXaml_WiresGenerateCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateTransferRoutesCommand}\""));
    }
}
