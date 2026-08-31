using System.IO;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class ReportsViewTests
{
    [Test]
    public void ReportsViewXaml_WiresLiveReportCommands()
    {
        var xaml = File.ReadAllText(FindView("Views/Reports/ReportsView.xaml"));

        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateStudentRosterCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateUnassignedStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateRouteSummaryCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ExportAllDataToCsvCommand}\""));
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
