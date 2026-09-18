using System.Globalization;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// One RFC-4180-ish field dialect for roster CSV exporters.
/// Quote only when a field contains comma, quote, or newline.
/// </summary>
public static class CsvLine
{
    public static string Join(params object?[] fields) =>
        string.Join(",", fields.Select(Format));

    public static string Format(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var text = value switch
        {
            string s => s,
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeSpan ts => ts.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            bool flag => flag ? "True" : "False",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        var needsQuotes = text.Contains(',', StringComparison.Ordinal)
                          || text.Contains('"', StringComparison.Ordinal)
                          || text.Contains('\n', StringComparison.Ordinal)
                          || text.Contains('\r', StringComparison.Ordinal);
        return needsQuotes
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
    }
}
