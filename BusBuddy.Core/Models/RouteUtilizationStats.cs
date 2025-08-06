using System;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents statistics related to route utilization.
    /// </summary>
    public class RouteUtilizationStats
    {
        /// <summary>
        /// Gets or sets the total number of routes.
        /// </summary>
        public int TotalRoutes { get; set; }

        /// <summary>
        /// Gets or sets the total number of assigned students.
        /// </summary>
        public int TotalAssignedStudents { get; set; }

        /// <summary>
        /// Gets or sets the total number of unassigned students.
        /// </summary>
        public int TotalUnassignedStudents { get; set; }

        /// <summary>
        /// Gets or sets the total capacity of all routes.
        /// </summary>
        public int TotalCapacity { get; set; }

        /// <summary>
        /// Gets or sets the average utilization rate across all routes (0.0 to 1.0).
        /// </summary>
        public double AverageUtilizationRate { get; set; }

        /// <summary>
        /// Gets or sets the number of routes operating at or over capacity.
        /// </summary>
        public int RoutesAtCapacity { get; set; }

        /// <summary>
        /// Gets or sets the number of routes that are underutilized (less than 50% capacity).
        /// </summary>
        public int UnderutilizedRoutes { get; set; }

        /// <summary>
        /// Gets or sets the total estimated distance covered by all routes.
        /// </summary>
        public double TotalEstimatedDistance { get; set; }

        /// <summary>
        /// Gets or sets the total estimated time spent on all routes.
        /// </summary>
        public TimeSpan TotalEstimatedTime { get; set; }

        /// <summary>
        /// Gets or sets an overall efficiency score for the route system (0.0 to 1.0).
        /// </summary>
        public double OverallEfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets the date and time when these statistics were calculated.
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the estimated monthly savings.
        /// </summary>
        public decimal EstimatedMonthlySavings { get; set; }

        /// <summary>
        /// Gets or sets the estimated annual savings.
        /// </summary>
        public decimal EstimatedAnnualSavings { get; set; }
    }
}
