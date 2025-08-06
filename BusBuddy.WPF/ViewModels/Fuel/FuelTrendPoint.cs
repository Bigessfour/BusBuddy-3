using System;

namespace BusBuddy.WPF.ViewModels.Fuel
{
    /// <summary>
    /// Represents a data point for fuel trend analysis.
    /// </summary>
    public class FuelTrendPoint
    {
        /// <summary>
        /// Gets or sets the time period for this trend point.
        /// </summary>
        public DateTime Period { get; set; }

        /// <summary>
        /// Gets or sets the average miles per gallon for this period.
        /// </summary>
        public double AvgMPG { get; set; }
    }
}
