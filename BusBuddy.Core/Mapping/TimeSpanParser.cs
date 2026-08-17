using System.Globalization;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Parses clock times from common hh:mm forms used in activity dialogs.
/// </summary>
public static class TimeSpanParser
{
    private static readonly string[] ExactFormats =
    {
        @"h\:mm",
        @"hh\:mm",
        @"h\:mm\:ss",
        @"hh\:mm\:ss"
    };

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (TimeSpan.TryParseExact(trimmed, ExactFormats, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out value)
               || TimeSpan.TryParse(trimmed, CultureInfo.CurrentCulture, out value);
    }

    public static string Format(TimeSpan value) =>
        value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}
