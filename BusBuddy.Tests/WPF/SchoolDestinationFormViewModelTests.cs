using System;
using BusBuddy.WPF.ViewModels.Student;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
public class SchoolDestinationFormViewModelTests
{
    [Test]
    public void FormatTimeText_UsesTwentyFourHourClock()
    {
        var value = new DateTime(2026, 9, 20, 15, 30, 0);
        Assert.That(SchoolDestinationFormViewModel.FormatTimeText(value, "08:00"), Is.EqualTo("15:30"));
        Assert.That(SchoolDestinationFormViewModel.FormatTimeText(null, "08:00"), Is.EqualTo("08:00"));
    }

    [Test]
    public void ParseTimePickerValue_ParsesStoredBellTimes()
    {
        var start = SchoolDestinationFormViewModel.ParseTimePickerValue("07:45", new TimeSpan(8, 0, 0));
        Assert.That(start, Is.Not.Null);
        Assert.That(start!.Value.TimeOfDay, Is.EqualTo(new TimeSpan(7, 45, 0)));
    }
}
