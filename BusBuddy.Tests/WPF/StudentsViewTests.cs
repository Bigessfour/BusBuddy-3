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
    }

    [Test]
    public void StudentDeletionReasonDialogXaml_RequiresAReasonChoice()
    {
        var xaml = File.ReadAllText(FindView("Views/Student/StudentDeletionReasonDialog.xaml"));

        Assert.That(xaml, Does.Contain("Command=\"{Binding ConfirmCommand}\""));
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
