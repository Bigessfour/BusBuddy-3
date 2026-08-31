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
