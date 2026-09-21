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
        Assert.That(xaml, Does.Not.Contain("MVP"));
    }

    [Test]
    public void RouteAssignmentViewXaml_WiresAssignAndRefreshCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignVehicleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDataCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Refresh Route Data\""));

        var codeBehind = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml.cs");
        Assert.That(codeBehind, Does.Contain("FindAncestor<Syncfusion.Windows.Tools.Controls.ButtonAdv>"));
        Assert.That(codeBehind, Does.Contain("RouteAssign ButtonAdv"));
        Assert.That(xaml, Does.Contain("Command=\"{Binding MarkNotRidingTodayCommand}\""));
        Assert.That(xaml, Does.Contain("SelectedRouteBusDisplay"));
        Assert.That(xaml, Does.Contain("SelectedRouteDriverDisplay"));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\""));
    }

    [Test]
    public void RouteAssignmentViewXaml_WiresPdfGridSheetCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding ViewScheduleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDrivePathCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintRouteSheetCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ExportRouteSheetCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding ViewRouteTimetableCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding PrintMapCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding GenerateReportCommand}\""));
    }

    [Test]
    public void ViewSchedule_LogsPublishedClocksAndOpensPdfGridPreview()
    {
        var schedule = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Schedule.cs");
        Assert.That(schedule, Does.Contain("Opened route schedule"));
        Assert.That(schedule, Does.Contain("Clocks={Clocks}"));
        Assert.That(schedule, Does.Contain("PrintSelectedRouteSheetPreview"));
        Assert.That(schedule, Does.Contain("preview: true"));
        Assert.That(schedule, Does.Contain("new RouteScheduleWindow"));
        Assert.That(schedule, Does.Not.Contain("new DriverScheduleView"));

        var reports = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Reports.cs");
        Assert.That(reports, Does.Contain("new PdfPreviewWindow("));
        Assert.That(reports, Does.Contain("Grid=PdfGrid"));
        Assert.That(reports, Does.Contain("Verb=none"));
        Assert.That(reports, Does.Not.Contain("UseShellExecute"));
    }

    [Test]
    public void RouteAssignmentViewXaml_SeparatesPickersFromLabeledButtonRibbon()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");

        Assert.That(xaml, Does.Contain("Style=\"{StaticResource RouteToolbarCaption}\""));
        Assert.That(xaml, Does.Contain("Text=\"Fleet\""));
        Assert.That(xaml, Does.Contain("Text=\"Publish\""));
        Assert.That(xaml, Does.Contain("MinWidth=\"200\""));
        Assert.That(xaml, Does.Contain("x:Name=\"RoutePicker\""));
        Assert.That(xaml, Does.Contain("x:Name=\"StartTimeInput\""));
        Assert.That(xaml, Does.Not.Contain("Grid.Column=\"5\"\n                           Orientation=\"Horizontal\""));
    }

    [Test]
    public void RouteAssignmentViewXaml_StudentGridsFillHostAndShareStarRows()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("ButtonAdvTextOnly.xaml"));
        Assert.That(xaml, Does.Contain("ToolTipService.ShowOnDisabled=\"True\""));
        Assert.That(xaml, Does.Contain("VerticalScrollBarVisibility=\"Auto\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding TimeRouteToolTip}\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding NotRidingTodayToolTip}\""));
        Assert.That(xaml, Does.Contain("MinHeight=\"180\""));
        Assert.That(xaml, Does.Contain("Header=\"Unassigned Students\""));
        Assert.That(xaml, Does.Contain("Header=\"Assigned to Route\""));
        Assert.That(xaml, Does.Contain("Header=\"Route Stops\""));
        Assert.That(xaml, Does.Not.Contain("📚"));
        Assert.That(xaml, Does.Not.Contain("✅"));
        Assert.That(xaml, Does.Contain("ShowGroupDropArea=\"False\""));
        Assert.That(xaml, Does.Contain("MinimumWidth=\"120\""));
        Assert.That(xaml, Does.Contain("MinimumWidth=\"140\""));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"StudentName\"\n                                                       Width=\"150\""));
        Assert.That(xaml, Does.Contain("IsEditable=\"False\""));
        var stops = XamlViewFile.Read("Views/Route/RouteStopsEditor.xaml");
        Assert.That(stops, Does.Contain("ColumnSizer=\"Star\""));
        Assert.That(stops, Does.Contain("ShowGroupDropArea=\"False\""));
        Assert.That(stops, Does.Contain("MinimumWidth=\"140\""));
        Assert.That(stops, Does.Not.Contain("Width=\"180\""));
        var code = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml.cs");
        Assert.That(code, Does.Contain("IsVisibleChanged"));
        Assert.That(code, Does.Contain("RelayoutHostedGrids"));
        Assert.That(code, Does.Contain("RouteAssignmentView visible"));
        Assert.That(code, Does.Contain("UiDiagnosticsLog.Write"));
        var vm = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.cs");
        Assert.That(vm, Does.Contain("GenerateReportCommand = ExportRouteSheetCommand"));
        Assert.That(vm, Does.Contain("PrintMapCommand = PrintRouteSheetCommand"));

        var hints = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.CommandHints.cs");
        Assert.That(hints, Does.Contain("NotRidingTodayToolTip"));
        var commands = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Commands.cs");
        Assert.That(commands, Does.Contain("MessageBox.Show"));
        Assert.That(commands, Does.Contain("Assigned to Route list"));
    }
}
