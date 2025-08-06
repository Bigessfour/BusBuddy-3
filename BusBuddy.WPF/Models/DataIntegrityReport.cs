using System;
using System.Collections.Generic;
using System.Linq;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Comprehensive data integrity validation report
    /// </summary>
    public class DataIntegrityReport
    {
        public DataIntegrityReport()
        {
            RouteIssues = new List<DataIntegrityIssue>();
            ActivityIssues = new List<DataIntegrityIssue>();
            StudentIssues = new List<DataIntegrityIssue>();
            DriverIssues = new List<DataIntegrityIssue>();
            VehicleIssues = new List<DataIntegrityIssue>();
            CrossEntityIssues = new List<DataIntegrityIssue>();
            GeneratedAt = DateTime.Now;
        }

        /// <summary>
        /// Issues found in route data
        /// </summary>
        public List<DataIntegrityIssue> RouteIssues { get; set; }

        /// <summary>
        /// Issues found in activity data
        /// </summary>
        public List<DataIntegrityIssue> ActivityIssues { get; set; }

        /// <summary>
        /// Issues found in student data
        /// </summary>
        public List<DataIntegrityIssue> StudentIssues { get; set; }

        /// <summary>
        /// Issues found in driver data
        /// </summary>
        public List<DataIntegrityIssue> DriverIssues { get; set; }

        /// <summary>
        /// Issues found in vehicle data
        /// </summary>
        public List<DataIntegrityIssue> VehicleIssues { get; set; }

        /// <summary>
        /// Issues found in cross-entity relationships
        /// </summary>
        public List<DataIntegrityIssue> CrossEntityIssues { get; set; }

        /// <summary>
        /// Total number of issues found across all entities
        /// </summary>
        public int TotalIssuesFound { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ValidationError { get; set; } = string.Empty;

        /// <summary>
        /// When the report was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Get all issues in a single collection
        /// </summary>
        public List<DataIntegrityIssue> AllIssues
        {
            get
            {
                var allIssues = new List<DataIntegrityIssue>();
                allIssues.AddRange(RouteIssues);
                allIssues.AddRange(ActivityIssues);
                allIssues.AddRange(StudentIssues);
                allIssues.AddRange(DriverIssues);
                allIssues.AddRange(VehicleIssues);
                allIssues.AddRange(CrossEntityIssues);
                return allIssues;
            }
        }

        /// <summary>
        /// Get critical issues only
        /// </summary>
        public List<DataIntegrityIssue> CriticalIssues =>
            AllIssues.Where(i => i.Severity == "Critical").ToList();

        /// <summary>
        /// Get high priority issues
        /// </summary>
        public List<DataIntegrityIssue> HighPriorityIssues =>
            AllIssues.Where(i => i.Severity == "High").ToList();

        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string Summary
        {
            get
            {
                var critical = CriticalIssues.Count;
                var high = HighPriorityIssues.Count;
                var medium = AllIssues.Count(i => i.Severity == "Medium");
                var low = AllIssues.Count(i => i.Severity == "Low");

                return $"Critical: {critical}, High: {high}, Medium: {medium}, Low: {low}";
            }
        }
    }
}
