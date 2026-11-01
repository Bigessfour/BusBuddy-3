using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Thin XAML/command contract for <c>Views/Route/RouteScheduleWindow.xaml</c>.
/// Behavior lives in <see cref="RouteScheduleViewModelTests"/>; Item 6 guest-proved the print path.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteScheduleWindowTests
{
    [Test]
    public void RouteScheduleWindow_Xaml_UsesSfDataGridCommandsAndAccessibilityNames()
    {
        // Inventory stem is RouteScheduleWindow.xaml — keep that path in the first 4k of content.
        var xaml = XamlViewFile.Read("Views/Route/RouteScheduleWindow.xaml");
        Assert.That(xaml, Does.Contain("syncfusion:ChromelessWindow"));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route Schedule\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route schedule stops\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route schedule roster\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ReTimeCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CloseCommand}\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding ReTimeToolTip}\""));
        Assert.That(xaml, Does.Contain("ToolTip=\"{Binding PrintToolTip}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Sheet.Stops}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Sheet.Students}\""));
        Assert.That(xaml, Does.Contain("syncfusion:SfDataGrid"));
        Assert.That(xaml, Does.Contain("ColumnSizer=\"Star\""));
        Assert.That(xaml, Does.Not.Contain("SfScheduler"));
        Assert.That(xaml, Does.Not.Contain("DriverSchedule"));
        Assert.That(xaml, Does.Not.Contain("<DataGrid"));
        Assert.That(xaml, Does.Not.Contain("System.Windows.Controls.DataGrid"));
    }

    [Test]
    public void RouteScheduleWindow_CodeBehind_BindsVmAndClosesOnRequest()
    {
        var code = XamlViewFile.Read("Views/Route/RouteScheduleWindow.xaml.cs");
        Assert.That(code, Does.Contain("RouteScheduleViewModel viewModel"));
        Assert.That(code, Does.Contain("ArgumentNullException.ThrowIfNull(viewModel)"));
        Assert.That(code, Does.Contain("SyncfusionThemeManager.ApplyTheme(this)"));
        Assert.That(code, Does.Contain("DataContext = viewModel"));
        Assert.That(code, Does.Contain("viewModel.CloseRequested += OnCloseRequested"));
        Assert.That(code, Does.Contain("viewModel.CloseRequested -= OnCloseRequested"));
        Assert.That(code, Does.Contain("Close()"));
    }
}
