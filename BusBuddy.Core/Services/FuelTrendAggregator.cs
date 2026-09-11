using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>One calendar month of aggregated fuel usage for charts.</summary>
public sealed record FuelMonthlyTrend(
    DateTime Period,
    double AvgMpg,
    decimal TotalGallons,
    decimal TotalCost,
    int FillCount,
    int TripMpgSampleCount);

/// <summary>
/// Builds monthly fuel chart points from raw fill-ups.
/// MPG uses consecutive odometer deltas per bus (not odometer/gallons on a single row).
/// </summary>
public static class FuelTrendAggregator
{
    /// <summary>School-bus MPG above this is treated as bad odometer data.</summary>
    public const double MaxPlausibleBusMpg = 25.0;

    public static IReadOnlyList<FuelMonthlyTrend> AggregateMonthly(IEnumerable<Fuel> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var list = records.ToList();
        if (list.Count == 0)
        {
            return Array.Empty<FuelMonthlyTrend>();
        }

        var tripMpgs = new List<(DateTime Period, double Mpg)>();
        foreach (var byBus in list.GroupBy(f => f.VehicleFueledId))
        {
            var ordered = byBus
                .OrderBy(f => f.FuelDate)
                .ThenBy(f => f.FuelId)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var cur = ordered[i];
                if (!cur.Gallons.HasValue || cur.Gallons.Value <= 0)
                {
                    continue;
                }

                var miles = cur.VehicleOdometerReading - prev.VehicleOdometerReading;
                if (miles <= 0)
                {
                    continue;
                }

                var mpg = miles / (double)cur.Gallons.Value;
                if (mpg is <= 0 or > MaxPlausibleBusMpg)
                {
                    continue;
                }

                tripMpgs.Add((MonthStart(cur.FuelDate), mpg));
            }
        }

        var mpgByMonth = tripMpgs
            .GroupBy(t => t.Period)
            .ToDictionary(g => g.Key, g => (Avg: g.Average(x => x.Mpg), Count: g.Count()));

        return list
            .GroupBy(f => MonthStart(f.FuelDate))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                mpgByMonth.TryGetValue(g.Key, out var mpg);
                // double.NaN → SfChart empty point (not plotted as 0 MPG).
                var avgMpg = mpg.Count > 0 ? Math.Round(mpg.Avg, 2) : double.NaN;
                return new FuelMonthlyTrend(
                    Period: g.Key,
                    AvgMpg: avgMpg,
                    TotalGallons: g.Sum(f => f.Gallons ?? 0m),
                    TotalCost: g.Sum(f => f.TotalCost ?? 0m),
                    FillCount: g.Count(),
                    TripMpgSampleCount: mpg.Count);
            })
            .ToList();
    }

    private static DateTime MonthStart(DateTime date) =>
        new(date.Year, date.Month, 1);
}
