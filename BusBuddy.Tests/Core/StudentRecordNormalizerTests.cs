using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentRecordNormalizerTests
{
    [Test]
    public void NormalizeOptionalForeignKeys_clears_zero_family_id()
    {
        var student = new Student { StudentName = "Test", FamilyId = 0 };

        StudentRecordNormalizer.NormalizeOptionalForeignKeys(student);

        Assert.That(student.FamilyId, Is.Null);
    }

    [Test]
    public void NormalizeDateTimes_converts_local_audit_fields_to_utc()
    {
        var local = new DateTime(2026, 9, 2, 11, 30, 0, DateTimeKind.Local);
        var student = new Student
        {
            StudentName = "Test",
            CreatedDate = local,
            DateOfBirth = new DateTime(2010, 5, 1),
        };

        StudentRecordNormalizer.NormalizeDateTimes(student);

        Assert.That(student.CreatedDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(student.DateOfBirth!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
    }
}
