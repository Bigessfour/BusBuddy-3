using BusBuddy.Core.Mapping;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class TimeSpanParserTests
{
    [TestCase("8:00", 8, 0)]
    [TestCase("08:00", 8, 0)]
    [TestCase("8:00:00", 8, 0)]
    [TestCase("14:30", 14, 30)]
    public void TryParse_AcceptsCommonClockForms(string text, int hours, int minutes)
    {
        Assert.That(TimeSpanParser.TryParse(text, out var value), Is.True);
        Assert.That(value, Is.EqualTo(new TimeSpan(hours, minutes, 0)));
    }

    [Test]
    public void TryParse_RejectsEmptyAndGarbage()
    {
        Assert.That(TimeSpanParser.TryParse("", out _), Is.False);
        Assert.That(TimeSpanParser.TryParse("noon", out _), Is.False);
    }

    [Test]
    public void Format_UsesHourMinute()
    {
        Assert.That(TimeSpanParser.Format(new TimeSpan(8, 5, 0)), Is.EqualTo("08:05"));
    }
}
