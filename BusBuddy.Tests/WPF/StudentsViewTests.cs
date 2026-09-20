using System;
using System.IO;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class StudentsViewTests
{
    [Test]
    public void StudentsViewXaml_WiresClerkWriteCommands()
    {
        var xaml = File.ReadAllText(FindView("Views/Student/StudentsView.xaml"));

        Assert.That(xaml, Does.Contain("Command=\"{Binding ImportStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding OptimizeRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding SchoolTransferCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddStudentCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddSchoolCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding EditSchoolCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteSchoolCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ArchiveStudentCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteStudentCommand}\""));
        Assert.That(xaml, Does.Contain("Label=\"Delete Student\""));
        Assert.That(xaml, Does.Contain("Text=\"Record\""));
        Assert.That(xaml, Does.Contain("Text=\"Roster\""));
        Assert.That(xaml, Does.Not.Contain("PurgeStudentRecordCommand"));
        Assert.That(xaml, Does.Not.Contain("ShowQuickActionsCommand"));
        // School of record is DestinationId (FK); Name is display-only on the combo.
        Assert.That(xaml, Does.Contain("MappingName=\"DestinationId\""));
        Assert.That(xaml, Does.Contain("HeaderText=\"School\""));
        Assert.That(xaml, Does.Contain("DisplayMemberPath=\"Name\""));
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"DestinationId\""));
        Assert.That(xaml, Does.Not.Contain("DisplayMemberPath=\"RouteName\""));
        Assert.That(xaml, Does.Contain("MappingName=\"Latitude\""));
        Assert.That(xaml, Does.Contain("MappingName=\"Longitude\""));
        Assert.That(xaml, Does.Contain("MappingName=\"StudentId\""));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"StudentNumber\""));
        Assert.That(xaml, Does.Not.Contain("AddNewRowPosition"));
        Assert.That(xaml, Does.Contain("AllowEditing=\"False\""));
        Assert.That(xaml, Does.Not.Contain("EditTrigger=\"OnTap\""));
        Assert.That(xaml, Does.Contain("ColumnSizer=\"None\""));
        Assert.That(xaml, Does.Contain("FrozenColumnCount=\"2\""));
        Assert.That(xaml, Does.Contain("Name=\"ColumnsChooserButton\""));
        Assert.That(xaml, Does.Contain("Click=\"ColumnsChooserButton_Click\""));
        Assert.That(xaml, Does.Contain("Name=\"ActiveFilterCombo\""));
        var roster = xaml.IndexOf("Text=\"Roster\"", StringComparison.Ordinal);
        var filter = xaml.IndexOf("Name=\"ActiveFilterCombo\"", StringComparison.Ordinal);
        var record = xaml.IndexOf("Text=\"Record\"", StringComparison.Ordinal);
        Assert.That(roster, Is.GreaterThan(record));
        Assert.That(filter, Is.GreaterThan(roster), "Active filter sits on the Roster row so it is not clipped off Record");
    }

    [Test]
    public void StudentDeletionReasonDialogXaml_RequiresAReasonChoice()
    {
        var xaml = File.ReadAllText(FindView("Views/Student/StudentDeletionReasonDialog.xaml"));

        Assert.That(xaml, Does.Contain("Command=\"{Binding ConfirmCommand}\""));
        Assert.That(xaml, Does.Contain("ResizeMode=\"CanResizeWithGrip\""));
        Assert.That(xaml, Does.Contain("MinHeight=\"520\""));
        Assert.That(xaml, Does.Contain("Height=\"580\""));
        Assert.That(xaml, Does.Contain("SizeChanged=\"DeletionDialog_SizeChanged\""));
        Assert.That(xaml, Does.Contain("VerticalScrollBarVisibility=\"Auto\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Reasons}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedReason, Mode=TwoWay}\""));
    }

    [Test]
    public void SchoolCatalogPickerDialogXaml_BindsSchoolList()
    {
        var xaml = File.ReadAllText(FindView("Views/Student/SchoolCatalogPickerDialog.xaml"));

        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Schools}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedSchool, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("DisplayMemberPath=\"Name\""));
    }

    [Test]
    public void StudentGridColumnChooserDialogXaml_BindsVisibilityCheckboxes()
    {
        var xaml = File.ReadAllText(FindView("Views/Student/StudentGridColumnChooserDialog.xaml"));

        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Columns}\""));
        Assert.That(xaml, Does.Contain("IsChecked=\"{Binding IsVisible, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ApplyCommand}\""));
        Assert.That(xaml, Does.Contain("ResizeMode=\"CanResizeWithGrip\""));
    }

    [Test]
    public void AppComposition_RegistersStudentsViewModelWithExplicitFactory()
    {
        var src = File.ReadAllText(FindView("App.xaml.cs"));
        Assert.That(src, Does.Contain("new BusBuddy.WPF.ViewModels.Student.StudentsViewModel("));
        Assert.That(src, Does.Contain("GetRequiredService<IBusBuddyDbContextFactory>()"));
        Assert.That(src, Does.Not.Contain("services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>();"));
    }

    private static string FindView(string relative)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "BusBuddy.WPF", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException($"Missing BusBuddy.WPF/{relative}");
    }
}
