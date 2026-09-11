using System.Globalization;

namespace BusBuddy.Core.Services;

/// <summary>
/// Free-text numeric parsing for fuel amounts (SfTextBoxExt pattern: no mid-keystroke masks).
/// Intermediate forms like "6." must not wipe or reformat while the clerk is typing.
/// </summary>
public static class FuelNumericText
{
    public static bool IsIntermediateNumber(string? text, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        culture ??= CultureInfo.CurrentCulture;
        var t = text.Trim();
        var sep = culture.NumberFormat.NumberDecimalSeparator;
        return t is "." or "-"
               || t.EndsWith('.')
               || t.EndsWith(sep, StringComparison.Ordinal);
    }

    public static bool TryParseDecimal(string? text, out decimal value, CultureInfo? culture = null)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        culture ??= CultureInfo.CurrentCulture;
        return decimal.TryParse(text, NumberStyles.Number, culture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static string FormatOptionalDecimal(decimal? value, string format, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return value.Value != 0
            ? value.Value.ToString(format, culture)
            : value.Value.ToString(culture);
    }

    /// <summary>
    /// Parse free text into a rounded decimal. Returns false when blank or intermediate (caller should leave model alone).
    /// </summary>
    public static bool TryParseRounded(
        string? text,
        int decimalPlaces,
        bool allowBlankAsNull,
        out decimal? value,
        CultureInfo? culture = null)
    {
        value = null;
        culture ??= CultureInfo.CurrentCulture;

        if (string.IsNullOrWhiteSpace(text))
        {
            return allowBlankAsNull;
        }

        if (IsIntermediateNumber(text, culture))
        {
            return false;
        }

        if (!TryParseDecimal(text, out var parsed, culture))
        {
            return false;
        }

        value = Math.Round(parsed, decimalPlaces);
        return true;
    }
}
