using BusBuddy.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class StudentGradeCatalogTests
{
    [Test]
    public void All_ContainsCanonicalDistrictGrades()
    {
        StudentGradeCatalog.All.Should().Contain(new[] { "Pre-K", "K", "1", "12" });
        StudentGradeCatalog.All.Should().NotContain("1st");
    }

    [Test]
    public void IsValid_AcceptsCanonicalLabels()
    {
        StudentGradeCatalog.IsValid("K").Should().BeTrue();
        StudentGradeCatalog.IsValid("5").Should().BeTrue();
        StudentGradeCatalog.IsValid("1st").Should().BeFalse();
    }
}
