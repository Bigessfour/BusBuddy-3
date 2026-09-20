using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteManagementViewTests
{
    [Test]
    public void RouteManagementViewXaml_WiresVehicleAssignmentPanel()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteManagementView.xaml");
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding DataContext.AvailableSchools, RelativeSource={RelativeSource AncestorType=UserControl}}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding DataContext.AvailableBuses, RelativeSource={RelativeSource AncestorType=UserControl}}\""));
        Assert.That(xaml, Does.Not.Contain("Source={x:Reference RoutesDataGrid}"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding AvailableBuses}\""));
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"BusId\""));
        Assert.That(xaml, Does.Contain("SelectedValue=\"{Binding SelectedBusId, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedTimeSlot, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("MappingName=\"Session\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignVehicleCommand}\""));
        Assert.That(xaml, Does.Contain("AllowEditing=\"True\""));
        Assert.That(xaml, Does.Contain("MappingName=\"AMVehicleId\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding AvailableDrivers}\""));
        Assert.That(xaml, Does.Contain("SelectedValue=\"{Binding SelectedDriverId, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Label=\"Save Route\""));
        Assert.That(xaml, Does.Contain("Pattern=\"ShortDate\""));
        Assert.That(xaml, Does.Contain("MappingName=\"School\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding OpenRouteAssignmentCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDrivePathCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding OptimizeStopOrderCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintScheduleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateScheduleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateTransferRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("BusBuddy.Brush.SafetyOrange"));
        Assert.That(xaml, Does.Contain("WrapPanel"));
        Assert.That(xaml, Does.Contain("ButtonAdvTextOnly.xaml"));
        Assert.That(xaml, Does.Not.Contain("CurrentCellEndEdit"));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\""));
        Assert.That(xaml, Does.Not.Contain("ViewMapCommand"));
        Assert.That(xaml, Does.Not.Contain("AssignStudentsButton"));
    }

    [Test]
    public void PrintSchedule_OpensInAppPdfPreview_NotShellExecute()
    {
        var assignment = XamlViewFile.Read("ViewModels/Route/RouteManagementViewModel.Assignment.cs");
        var start = assignment.IndexOf("private async Task PrintScheduleAsync()", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        var print = assignment[start..];
        Assert.That(print, Does.Contain("new PdfPreviewWindow("));
        Assert.That(print, Does.Contain("preview.Show()"));
        Assert.That(print, Does.Contain("Grid=PdfGrid"));
        Assert.That(print, Does.Contain("Verb=none"));
        Assert.That(print, Does.Not.Contain("WriteSchedulePdfAsync"));
        Assert.That(print, Does.Not.Contain("RevealOrOpen"));
        var helper = XamlViewFile.Read("ViewModels/Route/RouteManagementExportHelper.cs");
        Assert.That(helper, Does.Contain("catch (Exception ex)"));
        Assert.That(helper, Does.Contain("Could not open {Path}"));
    }

    [Test]
    public void RouteManagementView_ResolvesViewModelFromDi()
    {
        var source = XamlViewFile.Read("Views/Route/RouteManagementView.xaml.cs");
        Assert.That(source, Does.Contain("GetRequiredService<RouteManagementViewModel>()"));
        Assert.That(source, Does.Contain("InitializeAsync"));
        Assert.That(source, Does.Not.Contain("RoutesDataGrid_CurrentCellEndEdit"));
        Assert.That(source, Does.Not.Contain("GetProperty(\"RefreshCommand\")"));
    }
}
