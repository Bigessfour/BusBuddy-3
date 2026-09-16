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

    [Test]
    public void NormalizeForPersistence_strips_placeholder_home_coordinates()
    {
        var zero = new Student { StudentName = "TEST_STUDENT_ZERO", Latitude = 0m, Longitude = 0m };
        var centroid = new Student
        {
            StudentName = "TEST_STUDENT_CENTROID",
            Latitude = (decimal)LocationCoordinate.UsCentroidLatitude,
            Longitude = (decimal)LocationCoordinate.UsCentroidLongitude,
        };
        var valid = new Student
        {
            StudentName = "TEST_STUDENT_VALID",
            Latitude = 38.0872m,
            Longitude = -102.6208m,
            RidesAm = true,
            RidesPm = false,
        };

        StudentRecordNormalizer.NormalizeForPersistence(zero);
        StudentRecordNormalizer.NormalizeForPersistence(centroid);
        StudentRecordNormalizer.NormalizeForPersistence(valid);

        Assert.That(zero.Latitude, Is.Null);
        Assert.That(zero.Longitude, Is.Null);
        Assert.That(centroid.Latitude, Is.Null);
        Assert.That(centroid.Longitude, Is.Null);
        Assert.That(valid.Latitude, Is.EqualTo(38.0872m));
        Assert.That(valid.RidesAm, Is.True);
        Assert.That(valid.RidesPm, Is.False, "AM eligibility must not copy onto PM");
    }
}
