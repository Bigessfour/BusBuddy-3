using System.Diagnostics;

namespace BusBuddy.WPF.Utilities
{
#if DEBUG
    /// <summary>
    /// Centralized debug configuration for consistent DEBUG statement control
    /// </summary>
    public static class DebugConfig
    {
        public static bool EnableVerboseLogging { get; set; } = true;
        public static bool EnablePerformanceTracking { get; set; } = true;
        public static bool EnableDataStateLogging { get; set; } // Can be overwhelming
        public static bool EnableUITracking { get; set; }
        public static bool EnableStudentDebugLogging { get; set; } = true;
        public static bool EnableRouteAssignmentLogging { get; set; } = true;

        public static void WriteIf(bool condition, string category, string message)
        {
            if (condition)
            {
                Debug.WriteLine($"[{category}] {message}");
            }

        }

        public static void WriteStudent(string message)
        {
            WriteIf(EnableStudentDebugLogging, "STUDENT", message);
        }

        public static void WriteData(string message)
        {
            WriteIf(EnableDataStateLogging, "DATA", message);
        }

        public static void WritePerformance(string message)
        {
            WriteIf(EnablePerformanceTracking, "PERF", message);
        }

        public static void WriteUI(string message)
        {
            WriteIf(EnableUITracking, "UI", message);
        }
    }
#endif
}
