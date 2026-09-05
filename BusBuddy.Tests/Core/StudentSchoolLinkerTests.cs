using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class StudentSchoolLinkerTests
{
    private static readonly List<Destination> Catalog =
    [
        new Destination { DestinationId = 10, Name = "Oakridge Elementary" },
        new Destination { DestinationId = 20, Name = "Central High" },
    ];

    [Test]
    public void SyncDestinationFromSchoolName_MatchesByName_SetsDestinationId()
    {
        var student = new Student { School = "oakridge elementary" };

        StudentSchoolLinker.SyncDestinationFromSchoolName(student, Catalog);

        student.DestinationId.Should().Be(10);
        student.School.Should().Be("Oakridge Elementary");
    }

    [Test]
    public void SyncDestinationFromSchoolName_OnlyDestinationId_BackfillsSchool()
    {
        var student = new Student { DestinationId = 20, School = string.Empty };

        StudentSchoolLinker.SyncDestinationFromSchoolName(student, Catalog);

        student.School.Should().Be("Central High");
        student.DestinationId.Should().Be(20);
    }

    [Test]
    public void SyncDestinationFromSchoolName_UnknownSchool_LeavesDestinationIdUnset()
    {
        var student = new Student { School = "Unknown School", DestinationId = null };

        StudentSchoolLinker.SyncDestinationFromSchoolName(student, Catalog);

        student.DestinationId.Should().BeNull();
        student.School.Should().Be("Unknown School");
    }
}
