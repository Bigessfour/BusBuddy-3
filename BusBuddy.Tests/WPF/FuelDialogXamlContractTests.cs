using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
public class FuelDialogXamlContractTests
{
    [Test]
    public void FuelDialog_UsesFreeTextNumerics_AndEditableLocationTextBinding()
    {
        var xaml = XamlViewFile.Read("Views/Fuel/FuelDialog.xaml");

        Assert.That(xaml, Does.Contain("GallonsText"));
        Assert.That(xaml, Does.Contain("PricePerGallonText"));
        Assert.That(xaml, Does.Contain("TotalCostText"));
        Assert.That(xaml, Does.Contain("OdometerText"));
        Assert.That(xaml, Does.Contain("FuelLocationText"));
        Assert.That(xaml, Does.Contain("IsEditable=\"True\""));
        Assert.That(xaml, Does.Contain("DisplayMemberPath=\"FleetLabel\""));
        Assert.That(xaml, Does.Contain("Click=\"SaveButton_Click\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CancelCommand}\""));
        Assert.That(xaml, Does.Contain("GallonsError"));
        Assert.That(xaml, Does.Contain("SfTextBoxExt"));
        Assert.That(xaml, Does.Contain("FuelConstraints.MaxNotesLength"));
        Assert.That(xaml, Does.Not.Contain("NumberDecimalDigits="));
        Assert.That(xaml, Does.Not.Contain("<syncfusion:DoubleTextBox"));
        Assert.That(xaml, Does.Not.Contain("<syncfusion:IntegerTextBox"));
        Assert.That(xaml, Does.Not.Contain("PreviewKeyDown"));
    }

    [Test]
    public void FuelManagementView_ChartAndGridBindingsPresent()
    {
        var xaml = XamlViewFile.Read("Views/Fuel/FuelManagementView.xaml");
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FuelRecords}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FuelTrends}\""));
        Assert.That(xaml, Does.Contain("YBindingPath=\"TotalGallons\""));
        Assert.That(xaml, Does.Contain("YBindingPath=\"AvgMPG\""));
        Assert.That(xaml, Does.Contain("ShowEmptyPoints=\"False\""));
        Assert.That(xaml, Does.Contain("Symbol=\"Ellipse\""));
        Assert.That(xaml, Does.Not.Contain("Symbol=\"Circle\""));
        Assert.That(xaml, Does.Contain("MappingName=\"Vehicle.FleetLabel\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddCommand}\""));
        Assert.That(xaml, Does.Contain("BusBuddy.Brush.Overlay.Dim"));
        Assert.That(xaml, Does.Contain("Label=\"Reports\""));
        Assert.That(xaml, Does.Not.Contain("IsEnabled=\"{Binding CanEdit}\""));
        Assert.That(xaml, Does.Not.Contain("IsEnabled=\"{Binding CanDelete}\""));
        Assert.That(xaml, Does.Not.Contain("SelectionChanged=\"FuelDataGrid_SelectionChanged\""));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"VehicleFueledId\""));
        Assert.That(xaml, Does.Not.Contain("#80000000"));
    }

    [Test]
    public void FuelReconciliationDialog_UsesClerkBulkInput_NotPrintStub()
    {
        var xaml = XamlViewFile.Read("Views/Fuel/FuelReconciliationDialog.xaml");
        Assert.That(xaml, Does.Contain("BulkStationGallonsText"));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CloseCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ExportCommand}\""));
        Assert.That(xaml, Does.Contain("YBindingPath=\"VehicleUsageGallons\""));
        Assert.That(xaml, Does.Not.Contain("PrintCommand"));
        Assert.That(xaml, Does.Not.Contain("YBindingPath=\"BulkStationGallons\""));
    }
}
