using System;
using BusBuddy.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class DriverStudentRouteEligibilityTests
{
    [Test]
    public void ClassAAndB_Accepted_ClassCAndRegular_Rejected()
    {
        DriverStudentRouteEligibility.IsClassAOrB("Class A").Should().BeTrue();
        DriverStudentRouteEligibility.IsClassAOrB("B").Should().BeTrue();
        DriverStudentRouteEligibility.IsClassAOrB("CDL-A").Should().BeTrue();
        DriverStudentRouteEligibility.IsClassAOrB("Class C").Should().BeFalse();
        DriverStudentRouteEligibility.IsClassAOrB("Regular").Should().BeFalse();
        DriverStudentRouteEligibility.IsClassAOrB(null).Should().BeFalse();
    }

    [Test]
    public void SchoolBusEndorsementRequired()
    {
        var driver = QualifiedDriver();
        driver.SchoolBusEndorsement = false;

        DriverStudentRouteEligibility.TryGetAssignmentBlockReason(driver, out var reason)
            .Should().BeTrue();
        reason.Should().Be("missing school-bus endorsement");
    }

    [Test]
    public void ExpiredLicense_BlocksRegardlessOfEndorsement()
    {
        var driver = QualifiedDriver();
        driver.LicenseExpiryDate = DateTime.Today.AddDays(-1);

        DriverStudentRouteEligibility.TryGetAssignmentBlockReason(driver, out var reason)
            .Should().BeTrue();
        reason.Should().Be("expired license");
    }

    [Test]
    public void NormalizesClassAAndB_FromShortAndLongForms()
    {
        foreach (var licenseClass in new[] { "A", "a", "Class A", "CLASS B", "CDL B", "cdl-a" })
        {
            var driver = QualifiedDriver();
            driver.LicenseClass = licenseClass;
            DriverStudentRouteEligibility.MeetsAssignmentEligibility(driver).Should().BeTrue(licenseClass);
        }
    }

    [Test]
    public void QualifiedDriver_Passes()
    {
        DriverStudentRouteEligibility.MeetsAssignmentEligibility(QualifiedDriver()).Should().BeTrue();
    }

    [Test]
    public void LooksLikeSchoolBusEndorsementText_Conservative()
    {
        DriverStudentRouteEligibility.LooksLikeSchoolBusEndorsementText("S").Should().BeTrue();
        DriverStudentRouteEligibility.LooksLikeSchoolBusEndorsementText("P, S").Should().BeTrue();
        DriverStudentRouteEligibility.LooksLikeSchoolBusEndorsementText("school-bus").Should().BeTrue();
        DriverStudentRouteEligibility.LooksLikeSchoolBusEndorsementText("P").Should().BeFalse();
        DriverStudentRouteEligibility.LooksLikeSchoolBusEndorsementText("Special").Should().BeFalse();
    }

    private static Driver QualifiedDriver() => new()
    {
        DriverName = "Proof Driver",
        Status = "Active",
        TrainingComplete = true,
        LicenseClass = "Class B",
        SchoolBusEndorsement = true,
        LicenseExpiryDate = DateTime.Today.AddDays(90)
    };
}
