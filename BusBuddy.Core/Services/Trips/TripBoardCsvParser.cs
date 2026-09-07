using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Parses the clerk "Activity Schedule - Trip Schedule" board.
/// One spreadsheet row is one Trip. Does not create students or mutate PickupTime from notes.
/// </summary>
public static class TripBoardCsvParser
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly Regex MonthYear = new(
        @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{4}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DestinationInNotes = new(
        @"Destination:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static TripBoardParseResult Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var lines = SplitLines(csv);
        var oos = ParseOutOfServiceBuses(lines);

        var headerIndex = lines.FindIndex(l =>
            l.Contains("Ticket #", StringComparison.OrdinalIgnoreCase)
            && l.Contains("Date", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
        {
            return new TripBoardParseResult { OutOfServiceBusNumbers = oos };
        }

        var header = SplitCsvLine(lines[headerIndex]);
        var col = MapColumns(header);

        var rows = new List<TripBoardRow>();
        string? lastTicket = null;
        var lastYear = 2026;
        string? lastMonthLabel = null;

        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = SplitCsvLine(line);
            var monthLabel = ReadMonthYear(fields, col, lastMonthLabel);
            if (!string.IsNullOrEmpty(monthLabel))
            {
                lastMonthLabel = monthLabel;
                lastYear = ParseMonthYear(monthLabel)?.Year ?? lastYear;
            }

            var ticket = Get(fields, col.Ticket)?.Trim();
            var team = Get(fields, col.Team)?.Trim();
            var notes = Get(fields, col.Notes)?.Trim();
            var dateText = Get(fields, col.Date)?.Trim();
            var isDay2 = IsDay2Row(notes, ticket, team);

            var hasPayload = !string.IsNullOrEmpty(ticket)
                || !string.IsNullOrEmpty(team)
                || isDay2;
            if (!hasPayload)
            {
                continue;
            }

            var year = ParseMonthYear(monthLabel)?.Year ?? lastYear;
            var tripDate = ParseBoardDate(dateText, year);
            var departs = ParseTime(Get(fields, col.Departs));
            var returns = ParseTime(Get(fields, col.Returns));
            var returnNextDay = departs.HasValue && returns.HasValue && returns.Value < departs.Value;

            var location = Get(fields, col.Location)?.Trim();
            var destinationName = string.IsNullOrEmpty(location)
                ? ExtractDestinationFromNotes(notes)
                : StripSeeTripNotes(location);

            var busNumber = Get(fields, col.Bus)?.Trim();
            var isMultiAsset = IsMultiAsset(notes, busNumber);
            var overnight = IsOvernightPending(notes);

            string? linkedTicket = null;
            if (isDay2 && !string.IsNullOrEmpty(lastTicket))
            {
                linkedTicket = lastTicket;
            }

            var school = NormalizeSchool(Get(fields, col.School));
            var origin = string.IsNullOrEmpty(school) ? "Bus barn" : school;

            rows.Add(new TripBoardRow
            {
                ExternalTicketNo = string.IsNullOrEmpty(ticket) && isDay2 && linkedTicket is not null
                    ? $"{linkedTicket}-DAY2"
                    : EmptyToNull(ticket),
                TripDate = tripDate,
                RequestingSchool = EmptyToNull(school),
                GroupOrActivity = EmptyToNull(team),
                DestinationName = EmptyToNull(destinationName),
                PickupTime = departs,
                ReturnClockTime = returns,
                ReturnIsNextDay = returnNextDay,
                PlannedHeadcount = ParseInt(Get(fields, col.Pax)),
                ActualHeadcount = ParseInt(Get(fields, col.Rode)),
                PlannedMiles = ParseDecimal(Get(fields, col.Miles)),
                DriverName = EmptyToNull(Get(fields, col.Driver)?.Trim()),
                BusNumber = isMultiAsset ? null : EmptyToNull(busNumber),
                Notes = EmptyToNull(notes),
                MonthYearLabel = monthLabel ?? lastMonthLabel,
                LinkedTicketNo = linkedTicket,
                IsDay2WithoutTicket = isDay2 && string.IsNullOrEmpty(ticket),
                IsMultiAsset = isMultiAsset,
                IsOvernightPending = overnight,
                LinkBothTeams = ContainsBothTeams(notes),
                OriginName = origin
            });

            if (!string.IsNullOrEmpty(ticket))
            {
                lastTicket = ticket;
            }
        }

        return new TripBoardParseResult
        {
            OutOfServiceBusNumbers = oos,
            Rows = rows
        };
    }

    internal static IReadOnlyList<string> ParseOutOfServiceBuses(IReadOnlyList<string> lines)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (line.Contains("Ticket #", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var fields = SplitCsvLine(line);
            for (var i = 0; i < fields.Count; i++)
            {
                if (!fields[i].Trim().Equals("Out of Service", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Fleet header layout: Bus, Seats, Status — bus number is two cells left.
                if (i >= 2)
                {
                    var bus = fields[i - 2].Trim();
                    if (!string.IsNullOrEmpty(bus)
                        && !bus.Equals("Bus", StringComparison.OrdinalIgnoreCase)
                        && !bus.Equals("Seats", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(bus);
                    }
                }
            }
        }

        return found.ToList();
    }

    internal static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    private static List<string> SplitLines(string csv)
    {
        return csv.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }

    private sealed class ColumnMap
    {
        public int Date { get; init; }
        public int School { get; init; } = 1;
        public int Team { get; init; } = 2;
        public int Location { get; init; } = 3;
        public int Departs { get; init; } = 4;
        public int Returns { get; init; } = 5;
        public int Ticket { get; init; } = 6;
        public int Pax { get; init; } = 7;
        public int Rode { get; init; } = 8;
        public int Miles { get; init; } = 9;
        public int Driver { get; init; } = 10;
        public int Bus { get; init; } = 11;
        public int Notes { get; init; } = 12;
    }

    private static ColumnMap MapColumns(IReadOnlyList<string> header)
    {
        int Find(string name)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (header[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        var bus = Find("Bus #");
        var notes = Find("Notes");
        if (notes < 0 && bus >= 0)
        {
            notes = bus + 1;
        }

        return new ColumnMap
        {
            Date = OrDefault(Find("Date"), 0),
            School = OrDefault(Find("School"), 1),
            Team = OrDefault(Find("Team / School"), 2),
            Location = OrDefault(Find("Location"), 3),
            Departs = OrDefault(Find("Departs"), 4),
            Returns = OrDefault(Find("Returns"), 5),
            Ticket = OrDefault(Find("Ticket #"), 6),
            Pax = OrDefault(Find("PAX"), 7),
            Rode = OrDefault(Find("Rode"), 8),
            Miles = OrDefault(Find("Miles"), 9),
            Driver = OrDefault(Find("Driver"), 10),
            Bus = OrDefault(bus, 11),
            Notes = OrDefault(notes, 12)
        };
    }

    private static int OrDefault(int value, int fallback) => value >= 0 ? value : fallback;

    private static string? Get(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index] : null;

    private static string? ReadMonthYear(IReadOnlyList<string> fields, ColumnMap col, string? inherited)
    {
        for (var i = fields.Count - 1; i >= 0; i--)
        {
            var cell = fields[i].Trim();
            if (MonthYear.IsMatch(cell))
            {
                return cell;
            }
        }

        return inherited;
    }

    private static DateTime? ParseMonthYear(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return DateTime.TryParseExact(
            label.Trim(),
            "MMM yyyy",
            EnUs,
            DateTimeStyles.None,
            out var dt)
            ? dt
            : null;
    }

    private static DateTime? ParseBoardDate(string? dateText, int year)
    {
        if (string.IsNullOrWhiteSpace(dateText))
        {
            return null;
        }

        var trimmed = dateText.Trim().TrimEnd(',');
        var formats = new[] { "ddd, d MMM", "ddd, dd MMM", "d MMM", "dd MMM", "M/d/yyyy", "M/d/yy" };
        if (DateTime.TryParseExact(trimmed, formats, EnUs, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return new DateTime(year, parsed.Month, parsed.Day);
        }

        if (DateTime.TryParse(trimmed, EnUs, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return new DateTime(year, parsed.Month, parsed.Day);
        }

        return null;
    }

    private static TimeSpan? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var formats = new[] { "h:mm tt", "hh:mm tt", "h:mmtt", "H:mm", "HH:mm" };
        if (DateTime.TryParseExact(text.Trim(), formats, EnUs, DateTimeStyles.AllowWhiteSpaces, out var dt))
        {
            return dt.TimeOfDay;
        }

        return TimeSpan.TryParse(text.Trim(), EnUs, out var ts) ? ts : null;
    }

    private static int? ParseInt(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, EnUs, out var n) ? n : null;

    private static decimal? ParseDecimal(string? text) =>
        decimal.TryParse(text?.Trim(), NumberStyles.Number, EnUs, out var n) ? n : null;

    private static bool IsDay2Row(string? notes, string? ticket, string? team) =>
        string.IsNullOrEmpty(ticket)
        && string.IsNullOrEmpty(team)
        && !string.IsNullOrEmpty(notes)
        && notes.Contains("Day 2", StringComparison.OrdinalIgnoreCase);

    private static bool IsMultiAsset(string? notes, string? busNumber)
    {
        var text = $"{notes} {busNumber}";
        return text.Contains("all buses", StringComparison.OrdinalIgnoreCase)
            && text.Contains("SPED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOvernightPending(string? notes) =>
        !string.IsNullOrEmpty(notes)
        && notes.Contains("overnight", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsBothTeams(string? notes) =>
        !string.IsNullOrEmpty(notes)
        && notes.Contains("Both Teams Ride Together", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractDestinationFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var match = DestinationInNotes.Match(notes);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim().TrimStart('-').Trim();
        value = Regex.Replace(value, @"\s+\.\d+$", string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? StripSeeTripNotes(string location)
    {
        if (location.Contains("See Trip Notes", StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        return location;
    }

    private static string? NormalizeSchool(string? school)
    {
        var value = school?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "HS" or "MS" or "AV" or "WA" => value.ToUpperInvariant(),
            _ => value
        };
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
