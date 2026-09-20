using System.Collections.Generic;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class ClerkSessionLayoutTests
{
    [SetUp]
    public void Reset() => ClerkSessionLayout.Clear();

    [TearDown]
    public void TearDown() => ClerkSessionLayout.Clear();

    [Test]
    public void RememberDeletionSize_IgnoresTinyWindows_ThenStoresClerkDrag()
    {
        ClerkSessionLayout.RememberDeletionSize(120, 80);
        ClerkSessionLayout.TryGetDeletionSize(out _, out _).Should().BeFalse();

        ClerkSessionLayout.RememberDeletionSize(640, 720);
        ClerkSessionLayout.TryGetDeletionSize(out var width, out var height).Should().BeTrue();
        width.Should().Be(640);
        height.Should().Be(720);
    }

    [Test]
    public void RememberStudentGridColumnHidden_RoundTripsClerkPreference()
    {
        ClerkSessionLayout.HasStudentGridColumnPrefs.Should().BeFalse();
        ClerkSessionLayout.RememberStudentGridColumnHidden(new[]
        {
            new KeyValuePair<string, bool>("HomeAddress", true),
            new KeyValuePair<string, bool>("City", false),
        });

        ClerkSessionLayout.HasStudentGridColumnPrefs.Should().BeTrue();
        ClerkSessionLayout.TryGetStudentGridColumnHidden("HomeAddress", out var addressHidden).Should().BeTrue();
        addressHidden.Should().BeTrue();
        ClerkSessionLayout.TryGetStudentGridColumnHidden("City", out var cityHidden).Should().BeTrue();
        cityHidden.Should().BeFalse();
    }

    [Test]
    public void ColumnChooser_ApplyMarksApplied()
    {
        var vm = new StudentGridColumnChooserViewModel(new[]
        {
            new StudentGridColumnChoice("City", "City", isVisible: true, canHide: true),
        });

        vm.Applied.Should().BeFalse();
        vm.ApplyCommand.Execute(null);
        vm.Applied.Should().BeTrue();
        vm.Columns[0].IsVisible.Should().BeTrue();
    }
}
