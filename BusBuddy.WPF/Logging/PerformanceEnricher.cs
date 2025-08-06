using Serilog.Core;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace BusBuddy.WPF.Logging
{
    /// <summary>
    /// Enriches log events with UI performance and threading information
    /// </summary>
    public class PerformanceEnricher : ILogEventEnricher
    {
        private readonly Stopwatch _stopwatch;
        private readonly DateTime _startTime;

        public PerformanceEnricher()
        {
            _stopwatch = Stopwatch.StartNew();
            _startTime = DateTime.Now;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Add elapsed time since the enricher was created
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "ElapsedMs", _stopwatch.ElapsedMilliseconds));

            // Add elapsed time in ticks for more precise calculations
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "ElapsedTicks", _stopwatch.ElapsedTicks));

            // Add timestamp of the log event
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "LoggedAt", DateTime.Now.ToString("o")));

            // Add session duration
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "SessionDurationMs", (DateTime.Now - _startTime).TotalMilliseconds));

            // Add UI thread information
            try
            {
                var isUIThread = System.Windows.Application.Current?.Dispatcher?.CheckAccess() ?? false;
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IsUIThread", isUIThread));

                // Add dispatcher information if available
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    var dispatcher = System.Windows.Application.Current.Dispatcher;
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherThreadId", dispatcher.Thread.ManagedThreadId));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherHasShutdownStarted", dispatcher.HasShutdownStarted));
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DispatcherHasShutdownFinished", dispatcher.HasShutdownFinished));
                }
            }
            catch (Exception ex)
            {
                // Silently handle any exceptions to prevent disrupting logging
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UIThreadInfoError", ex.Message));
            }

            // Add memory usage information
            try
            {
                var workingSet = Environment.WorkingSet;
                var gcMemory = GC.GetTotalMemory(false);

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("WorkingSetMB", workingSet / (1024 * 1024)));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GCMemoryMB", gcMemory / (1024 * 1024)));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Gen0Collections", GC.CollectionCount(0)));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Gen1Collections", GC.CollectionCount(1)));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Gen2Collections", GC.CollectionCount(2)));
            }
            catch (Exception ex)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryInfoError", ex.Message));
            }

            // Add process information
            try
            {
                var process = Process.GetCurrentProcess();
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessId", process.Id));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadCount", process.Threads.Count));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("HandleCount", process.HandleCount));
            }
            catch (Exception ex)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessInfoError", ex.Message));
            }
        }
    }
}
