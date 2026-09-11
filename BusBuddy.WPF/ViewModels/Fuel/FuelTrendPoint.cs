using System;

namespace BusBuddy.WPF.ViewModels.Fuel
{
    /// <summary>
    /// Chart point for monthly fuel trends.
    /// Bound by SfChart: XBindingPath=Period, YBindingPath=TotalGallons | AvgMPG.
    /// </summary>
    public class FuelTrendPoint
    {
        /// <summary>First day of the calendar month (DateTimeAxis).</summary>
        public DateTime Period { get; set; }

        /// <summary>Average trip MPG for the month (<see cref="double.NaN"/> when no consecutive odometer pairs).</summary>
        public double AvgMPG { get; set; }

        /// <summary>Sum of gallons pumped in the month.</summary>
        public double TotalGallons { get; set; }

        /// <summary>Sum of total cost in the month.</summary>
        public double TotalCost { get; set; }

        /// <summary>Number of fill-up records in the month.</summary>
        public int FillCount { get; set; }

        /// <summary>How many consecutive-odometer trip MPG samples contributed to <see cref="AvgMPG"/>.</summary>
        public int TripMpgSampleCount { get; set; }

        /// <summary>Compact label for legends/tooltips (e.g. Mar 26).</summary>
        public string PeriodLabel => Period.ToString("MMM yy");

        /// <summary>True when AvgMPG is meaningful for the chart line.</summary>
        public bool HasTripMpg => TripMpgSampleCount > 0 && !double.IsNaN(AvgMPG) && AvgMPG > 0;
    }
}
