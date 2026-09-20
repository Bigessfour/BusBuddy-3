using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class ButtonAccessibilityAuditTests
{
    [Test]
    public void IsVendorChromeName_SkipsSyncfusionTemplateParts()
    {
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName("PART_NewTab"), Is.True);
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName("PART_CloseButton"), Is.True);
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName("InternalAutoGenerateName2"), Is.True);
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName("StudentsButton"), Is.False);
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName(null), Is.False);
        Assert.That(ButtonAccessibilityAudit.IsVendorChromeName(string.Empty), Is.False);
    }

    [Test]
    public void AuditSource_SkipsVendorChromeBeforeWarning()
    {
        var src = XamlViewFile.Read("Utilities/ButtonAccessibilityAudit.cs");
        Assert.That(src, Does.Contain("IsVendorChromeName"));
        Assert.That(src, Does.Contain("PART_"));
        Assert.That(src, Does.Contain("InternalAutoGenerate"));
    }

    [Test]
    public void StudentDeletionLog_IsInformationNotWarning()
    {
        var service = CoreSourceFile.Read("Services/StudentService.cs");
        Assert.That(service, Does.Contain("Logger.Information("));
        Assert.That(service, Does.Contain("Student deleted StudentId={StudentId}"));
        Assert.That(
            service,
            Does.Not.Contain("Logger.Warning(\n            \"Student deleted StudentId={StudentId}"));

        var archive = XamlViewFile.Read("ViewModels/Student/StudentsArchiveCoordinator.cs");
        Assert.That(archive, Does.Contain("Logger.Information("));
        Assert.That(archive, Does.Contain("Deleting student record StudentId={StudentId}"));
        Assert.That(archive, Does.Not.Contain("Logger.Warning(\n                    \"Deleting student record"));
    }
}
