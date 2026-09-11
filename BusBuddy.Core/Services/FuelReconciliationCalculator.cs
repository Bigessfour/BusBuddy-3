using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>One day of vehicle fuel usage for reconciliation charts.</summary>
public sealed record FuelDailyUsage(DateTime Date, decimal VehicleGallons, int FillCount);

/// <summary>A fill-up to verify when period bulk vs vehicle variance exceeds the threshold.</summary>
public sealed record FuelReconciliationDetail(
    DateTime Date,
    string BusNumber,
    double GallonsReported,
    int OdometerReading,
    string DiscrepancyType,
    string PotentialIssue,
    string RecommendedAction);

/// <summary>Period totals for bulk meter (clerk-entered) vs vehicle fuel logs.</summary>
public sealed record FuelReconciliationSnapshot(
    decimal VehicleUsageGallons,
    decimal? BulkStationGallons,
    decimal DiscrepancyGallons,
    double DiscrepancyRatio,
    bool HasBulkReading,
    IReadOnlyList<FuelDailyUsage> Daily,
    IReadOnlyList<FuelReconciliationDetail> Details);

/// <summary>
/// Honest fuel reconciliation: vehicle gallons from records; bulk gallons only when the clerk enters a meter reading.
/// Never invents bulk meter values.
/// </summary>
public static class FuelReconciliationCalculator
{
    public const double DefaultDiscrepancyThreshold = 0.03;

    public static FuelReconciliationSnapshot Build(
        IEnumerable<Fuel> records,
        decimal? bulkStationGallons,
        IReadOnlyDictionary<int, string>? busNumbers = null,
        double discrepancyThreshold = DefaultDiscrepancyThreshold)
    {
        ArgumentNullException.ThrowIfNull(records);

        var list = records.ToList();
        var daily = list
            .GroupBy(r => r.FuelDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new FuelDailyUsage(
                g.Key,
                g.Sum(r => r.Gallons ?? 0m),
                g.Count()))
            .ToList();

        var vehicleTotal = daily.Sum(d => d.VehicleGallons);
        var hasBulk = bulkStationGallons.HasValue;
        var bulk = hasBulk ? bulkStationGallons!.Value : 0m;
        var discrepancy = hasBulk ? bulk - vehicleTotal : 0m;
        var ratio = hasBulk && vehicleTotal > 0
            ? (double)(discrepancy / vehicleTotal)
            : 0d;

        var details = new List<FuelReconciliationDetail>();
        if (hasBulk && vehicleTotal > 0 && Math.Abs(ratio) > discrepancyThreshold)
        {
            var bulkSurplus = discrepancy > 0;
            foreach (var record in list.OrderBy(r => r.FuelDate).ThenBy(r => r.FuelId))
            {
                var busNumber = busNumbers != null &&
                                busNumbers.TryGetValue(record.VehicleFueledId, out var num)
                    ? num
                    : record.Vehicle?.BusNumber ?? $"Bus #{record.VehicleFueledId}";

                details.Add(new FuelReconciliationDetail(
                    Date: record.FuelDate,
                    BusNumber: busNumber,
                    GallonsReported: (double)(record.Gallons ?? 0m),
                    OdometerReading: record.VehicleOdometerReading,
                    DiscrepancyType: bulkSurplus ? "Bulk surplus" : "Vehicle surplus",
                    PotentialIssue: bulkSurplus
                        ? "Station meter higher than logged fill-ups — check missing logs or meter calibration"
                        : "Logged fill-ups exceed station meter — check overstated gallons or wrong location filter",
                    RecommendedAction: "Match this fill-up to the station ticket for the period"));
            }
        }

        return new FuelReconciliationSnapshot(
            VehicleUsageGallons: vehicleTotal,
            BulkStationGallons: bulkStationGallons,
            DiscrepancyGallons: discrepancy,
            DiscrepancyRatio: ratio,
            HasBulkReading: hasBulk,
            Daily: daily,
            Details: details);
    }
}
