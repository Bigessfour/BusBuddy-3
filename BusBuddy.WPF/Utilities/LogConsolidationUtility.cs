using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Utility for consolidating and managing log files in the root logs directory
    /// </summary>
    public class LogConsolidationUtility
    {
        private static readonly ILogger Logger = Log.ForContext<LogConsolidationUtility>();
        private readonly string _logsDirectory;

        public LogConsolidationUtility(string logsDirectory)
        {
            _logsDirectory = logsDirectory;
        }

        /// <summary>
        /// Consolidates all log files to the root logs directory and cleans up old separate log files
        /// </summary>
        public async Task ConsolidateLogsAsync()
        {
            try
            {
                Logger.Information("[LOG_CONSOLIDATION] Starting log consolidation process");

                // Ensure logs directory exists
                Directory.CreateDirectory(_logsDirectory);

                // Find and consolidate scattered log files
                ConsolidateScatteredLogs();

                // Clean up old log files that are no longer needed
                CleanupOldLogFiles();

                // Create consolidated log structure documentation
                await CreateLogStructureDocumentationAsync();

                Logger.Information("[LOG_CONSOLIDATION] Log consolidation completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LOG_CONSOLIDATION] Error during log consolidation: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Finds log files scattered throughout the solution and consolidates them
        /// </summary>
        private void ConsolidateScatteredLogs()
        {
            var solutionRoot = Directory.GetParent(_logsDirectory)?.FullName;
            if (solutionRoot == null)
            {
                return;
            }


            var logFiles = Directory.GetFiles(solutionRoot, "*.log", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(_logsDirectory)) // Exclude files already in logs directory
                .ToList();

            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(logFile);
                    var targetPath = Path.Combine(_logsDirectory, $"archived-{fileName}");

                    // Move the file to logs directory with archived prefix
                    if (File.Exists(logFile))
                    {
                        File.Move(logFile, targetPath);
                        Logger.Information("[LOG_CONSOLIDATION] Moved log file: {SourceFile} -> {TargetFile}", logFile, targetPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "[LOG_CONSOLIDATION] Could not move log file {LogFile}: {ErrorMessage}", logFile, ex.Message);
                }
            }
        }

        /// <summary>
        /// Cleans up old log files that are no longer needed due to consolidation
        /// </summary>
        private void CleanupOldLogFiles()
        {
            var oldLogPatterns = new[]
            {
                "ui-events-*.log",
                "syncfusion-*.log",
                "build.log",
                "*fallback*.log"
            };

            foreach (var pattern in oldLogPatterns)
            {
                try
                {
                    var files = Directory.GetFiles(_logsDirectory, pattern);
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);

                        // Only delete files older than 1 day to avoid losing recent data
                        if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-1))
                        {
                            File.Delete(file);
                            Logger.Information("[LOG_CONSOLIDATION] Deleted old log file: {FileName}", Path.GetFileName(file));
                        }
                        else
                        {
                            // Rename recent files to archived versions
                            var archivedName = Path.Combine(_logsDirectory, $"archived-{Path.GetFileName(file)}");
                            if (!File.Exists(archivedName))
                            {
                                File.Move(file, archivedName);
                                Logger.Information("[LOG_CONSOLIDATION] Archived recent log file: {FileName}", Path.GetFileName(file));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "[LOG_CONSOLIDATION] Error cleaning up pattern {Pattern}: {ErrorMessage}", pattern, ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates documentation about the consolidated log structure
        /// </summary>
        private async Task CreateLogStructureDocumentationAsync()
        {
            var docPath = Path.Combine(_logsDirectory, "LOG_STRUCTURE.md");
            var content = @"# BusBuddy Consolidated Log Structure

This directory contains all application logs in a consolidated structure to reduce file count while preserving all information.

## Active Log Files

### 1. busbuddy-YYYYMMDD.log
- **Purpose**: Main application log containing all events
- **Content**: All application events with UI, Syncfusion, and database operations clearly tagged
- **Retention**: 14 days
- **Format**: `[Timestamp] [Level] [ThreadId] [MachineName] [Operation] Message`

### 2. errors-YYYYMMDD.log
- **Purpose**: Consolidated error and warning log
- **Content**: All warnings and errors from any component (UI, Syncfusion, Database, etc.)
- **Retention**: 30 days (longer for troubleshooting)
- **Format**: `[Timestamp] [Level] [ThreadId] [MachineName] [Operation] Message`

### 3. performance-YYYYMMDD.log
- **Purpose**: Performance issues and slow operations
- **Content**: Operations taking >100ms, marked as SLOW, or performance warnings
- **Retention**: 7 days
- **Format**: `[Timestamp] [Level] [PERF] [Duration] [Operation] Message`

## Log Rotation
- Daily rotation with date suffix (YYYYMMDD)
- Automatic cleanup based on retention policies
- Shared file access for multi-process scenarios

## Operation Tags
- `[UIOperation]`: UI interactions (clicks, navigation, window operations)
- `[SyncfusionOperation]`: Syncfusion control events and theme changes
- `[DatabaseOperation]`: Database queries and transactions
- `[PERF]`: Performance-related events

## Benefits of Consolidation
- Reduced from 5+ separate log files to 3 focused files
- All information preserved with clear tagging
- Easier log management and analysis
- Consistent formatting across all log types
- Better performance (fewer file handles)

## Archived Files
Files with `archived-` prefix are old log files that have been consolidated but preserved for reference.

Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            await File.WriteAllTextAsync(docPath, content);
            Logger.Information("[LOG_CONSOLIDATION] Created log structure documentation: {DocPath}", docPath);
        }

        /// <summary>
        /// Gets statistics about current log files
        /// </summary>
        public LogConsolidationStats GetLogStats()
        {
            if (!Directory.Exists(_logsDirectory))
            {

                return new LogConsolidationStats();
            }


            var allLogFiles = Directory.GetFiles(_logsDirectory, "*.log");
            var activeLogFiles = allLogFiles.Where(f => !Path.GetFileName(f).StartsWith("archived-")).ToArray();
            var archivedLogFiles = allLogFiles.Where(f => Path.GetFileName(f).StartsWith("archived-")).ToArray();

            var totalSize = allLogFiles.Sum(f => new FileInfo(f).Length);

            return new LogConsolidationStats
            {
                TotalLogFiles = allLogFiles.Length,
                ActiveLogFiles = activeLogFiles.Length,
                ArchivedLogFiles = archivedLogFiles.Length,
                TotalSizeBytes = totalSize,
                TotalSizeMB = totalSize / (1024.0 * 1024.0),
                LogsDirectory = _logsDirectory
            };
        }
    }

    /// <summary>
    /// Statistics about the consolidated log structure
    /// </summary>
    public class LogConsolidationStats
    {
        public int TotalLogFiles { get; set; }
        public int ActiveLogFiles { get; set; }
        public int ArchivedLogFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public double TotalSizeMB { get; set; }
        public string LogsDirectory { get; set; } = string.Empty;
    }
}
