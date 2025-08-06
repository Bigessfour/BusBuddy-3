using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Monitors and tracks startup performance metrics for the application
    /// </summary>
    public class StartupPerformanceMonitor
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _overallStopwatch;
        private readonly Dictionary<string, Stopwatch> _stepStopwatches;
        private readonly Dictionary<string, TimeSpan> _completedSteps;
        private bool _isCompleted;

        /// <summary>
        /// Initializes a new instance of the StartupPerformanceMonitor
        /// </summary>
        /// <param name="logger">Logger instance for recording performance metrics</param>
        public StartupPerformanceMonitor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _overallStopwatch = new Stopwatch();
            _stepStopwatches = new Dictionary<string, Stopwatch>();
            _completedSteps = new Dictionary<string, TimeSpan>();
            _isCompleted = false;
        }

        /// <summary>
        /// Starts the overall startup performance monitoring
        /// </summary>
        public void Start()
        {
            _overallStopwatch.Start();
            _logger.Information("[STARTUP_PERF] Startup performance monitoring started");
        }

        /// <summary>
        /// Begins timing a specific startup step
        /// </summary>
        /// <param name="stepName">Name of the startup step</param>
        public void BeginStep(string stepName)
        {
            if (_isCompleted)
            {
                _logger.Warning("[STARTUP_PERF] Cannot begin step '{StepName}' — monitoring already completed", stepName);
                return;
            }

            if (_stepStopwatches.ContainsKey(stepName))
            {
                _logger.Warning("[STARTUP_PERF] Step '{StepName}' is already in progress", stepName);
                return;
            }

            var stepStopwatch = Stopwatch.StartNew();
            _stepStopwatches[stepName] = stepStopwatch;

            _logger.Debug("[STARTUP_PERF] Started step: {StepName}", stepName);
        }

        /// <summary>
        /// Ends timing for a specific startup step
        /// </summary>
        /// <param name="stepName">Name of the startup step to end</param>
        public void EndStep(string stepName = "")
        {
            if (_isCompleted)
            {
                _logger.Warning("[STARTUP_PERF] Cannot end step '{StepName}' — monitoring already completed", stepName);
                return;
            }

            // If no step name provided, end the most recently started step
            if (string.IsNullOrEmpty(stepName))
            {
                if (_stepStopwatches.Count == 0)
                {
                    _logger.Warning("[STARTUP_PERF] No active steps to end");
                    return;
                }

                // Get the most recently added step (assumes Dictionary maintains insertion order in .NET Core)
                stepName = GetMostRecentStep();
            }

            if (!_stepStopwatches.TryGetValue(stepName, out var stepStopwatch))
            {
                _logger.Warning("[STARTUP_PERF] Step '{StepName}' was not found or already ended", stepName);
                return;
            }

            stepStopwatch.Stop();
            var elapsed = stepStopwatch.Elapsed;

            _completedSteps[stepName] = elapsed;
            _stepStopwatches.Remove(stepName);

            _logger.Information("[STARTUP_PERF] Completed step '{StepName}' in {ElapsedMs}ms",
                stepName, elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Completes the startup performance monitoring and logs final metrics
        /// </summary>
        public void Complete()
        {
            if (_isCompleted)
            {
                _logger.Warning("[STARTUP_PERF] Performance monitoring already completed");
                return;
            }

            // End any remaining active steps
            var activeSteps = new List<string>(_stepStopwatches.Keys);
            foreach (var stepName in activeSteps)
            {
                EndStep(stepName);
            }

            _overallStopwatch.Stop();
            _isCompleted = true;

            // Log summary
            var totalTime = _overallStopwatch.Elapsed;
            _logger.Information("[STARTUP_PERF] 🎯 STARTUP COMPLETE — Total time: {TotalMs}ms", totalTime.TotalMilliseconds);

            // Log detailed breakdown
            LogDetailedBreakdown(totalTime);
        }

        /// <summary>
        /// Gets the current overall elapsed time
        /// </summary>
        public TimeSpan GetCurrentElapsed()
        {
            return _overallStopwatch.Elapsed;
        }

        /// <summary>
        /// Gets the elapsed time for a specific completed step
        /// </summary>
        /// <param name="stepName">Name of the step</param>
        /// <returns>Elapsed time for the step, or null if step not found</returns>
        public TimeSpan? GetStepElapsed(string stepName)
        {
            return _completedSteps.TryGetValue(stepName, out var elapsed) ? elapsed : null;
        }

        /// <summary>
        /// Gets all completed step metrics
        /// </summary>
        /// <returns>Dictionary of step names and their elapsed times</returns>
        public IReadOnlyDictionary<string, TimeSpan> GetAllStepMetrics()
        {
            return _completedSteps;
        }

        private string GetMostRecentStep()
        {
            string? mostRecent = null;
            foreach (var key in _stepStopwatches.Keys)
            {
                mostRecent = key;
            }
            return mostRecent ?? "";
        }

        private void LogDetailedBreakdown(TimeSpan totalTime)
        {
            if (_completedSteps.Count == 0)
            {
                _logger.Information("[STARTUP_PERF] No individual steps were tracked");
                return;
            }

            _logger.Information("[STARTUP_PERF] === DETAILED BREAKDOWN ===");

            var totalStepTime = TimeSpan.Zero;
            foreach (var step in _completedSteps)
            {
                var percentage = (step.Value.TotalMilliseconds / totalTime.TotalMilliseconds) * 100;
                _logger.Information("[STARTUP_PERF] • {StepName}: {ElapsedMs}ms ({Percentage:F1}%)",
                    step.Key, step.Value.TotalMilliseconds, percentage);
                totalStepTime += step.Value;
            }

            // Calculate unaccounted time
            var unaccountedTime = totalTime - totalStepTime;
            if (unaccountedTime.TotalMilliseconds > 0)
            {
                var unaccountedPercentage = (unaccountedTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100;
                _logger.Information("[STARTUP_PERF] • Unaccounted time: {UnaccountedMs}ms ({UnaccountedPercentage:F1}%)",
                    unaccountedTime.TotalMilliseconds, unaccountedPercentage);
            }

            _logger.Information("[STARTUP_PERF] === END BREAKDOWN ===");
        }
    }
}
