using System.Globalization;
using System.Windows.Input;
using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class NumpadInputHelperTests
{
    [Test]
    public void TryMapNumpadInsert_MapsNumPadDigits()
    {
        Assert.That(NumpadInputHelper.TryMapNumpadInsert(Key.NumPad7, out var insert), Is.True);
        Assert.That(insert, Is.EqualTo("7"));
    }

    [Test]
    public void TryMapNumpadInsert_IgnoresTopRowDigits()
    {
        Assert.That(NumpadInputHelper.TryMapNumpadInsert(Key.D5, out _), Is.False);
    }

    [Test]
    public void TryMapNumpadInsert_MapsDecimalToCultureSeparator()
    {
        Assert.That(NumpadInputHelper.TryMapNumpadInsert(Key.Decimal, out var insert), Is.True);
        Assert.That(insert, Is.EqualTo(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));
    }

    [Test]
    public void ResolveEffectiveKey_UnwrapsImeProcessedNumPad()
    {
        var key = NumpadInputHelper.ResolveEffectiveKey(
            Key.ImeProcessed,
            imeProcessedKey: Key.NumPad3,
            systemKey: Key.None,
            deadCharProcessedKey: Key.None);

        Assert.That(key, Is.EqualTo(Key.NumPad3));
        Assert.That(NumpadInputHelper.TryMapNumpadInsert(key, out var insert), Is.True);
        Assert.That(insert, Is.EqualTo("3"));
    }
}
