using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Thin XAML/command contract for <c>Views/Route/RouteStopsEditor.xaml</c> (assignment child shell).
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteStopsEditorTests
{
    [Test]
    public void RouteStopsEditor_Xaml_BindsStopCommandsToSfDataGrid()
    {
        // Inventory stem is RouteStopsEditor.xaml — keep that path in the first 4k of content.
        var xaml = XamlViewFile.Read("Views/Route/RouteStopsEditor.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddStopCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding EditStopCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RemoveStopCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding MoveStopUpCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding MoveStopDownCommand}\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding AddStopToolTip}\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding EditStopToolTip}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding RouteStops}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedRouteStop, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("MappingName=\"StopAddress\""));
        Assert.That(xaml, Does.Contain("MappingName=\"PublishedArrivalText\""));
        Assert.That(xaml, Does.Contain("MappingName=\"PublishedDepartureText\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route Stops Grid\""));
        Assert.That(xaml, Does.Contain("MouseDoubleClick=\"RouteStopsGrid_MouseDoubleClick\""));
        Assert.That(xaml, Does.Contain("syncfusion:SfDataGrid"));
        Assert.That(xaml, Does.Contain("ColumnSizer=\"Star\""));
        Assert.That(xaml, Does.Contain("ShowGroupDropArea=\"False\""));
        Assert.That(xaml, Does.Not.Contain("<DataGrid"));
        Assert.That(xaml, Does.Not.Contain("System.Windows.Controls.DataGrid"));
    }

    [Test]
    public void RouteStopsEditor_CodeBehind_DoubleClickExecutesEditStopCommand()
    {
        var code = XamlViewFile.Read("Views/Route/RouteStopsEditor.xaml.cs");
        Assert.That(code, Does.Contain("RouteStopsGrid_MouseDoubleClick"));
        Assert.That(code, Does.Contain("EditStopCommand"));
        Assert.That(code, Does.Contain("command.CanExecute(null)"));
        Assert.That(code, Does.Contain("command.Execute(null)"));
        Assert.That(code, Does.Contain("RouteAssignmentViewModel"));
    }
}
