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
    public void InsertIntoMaskText_NumpadFillsPromptsLeftToRight()
    {
        var (first, _) = NumpadInputHelper.InsertIntoMaskText("(___) ___-____", 0, 0, "7");
        Assert.That(first, Is.EqualTo("(7__) ___-____"));

        var (second, _) = NumpadInputHelper.InsertIntoMaskText(first, 0, 0, "1");
        Assert.That(second, Is.EqualTo("(71_) ___-____"));

        var (third, _) = NumpadInputHelper.InsertIntoMaskText(second, 0, 0, "9");
        Assert.That(third, Is.EqualTo("(719) ___-____"));
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
