using Serilog;
using System;
using System.IO;
using System.Linq;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Manages the lifecycle of Serilog output logs with intelligent cleanup and archiving
/// Focuses on keeping actionable errors while cleaning up resolved issues
/// </summary>
public class LogLifecycleManager
{
    private static readonly ILogger Logger = Log.ForContext<LogLifecycleManager>();
    private readonly string _logsDirectory;

    public LogLifecycleManager(string logsDirectory = "logs")
    {
        _logsDirectory = logsDirectory;
    }

    /// <summary>
    /// Performs intelligent log cleanup based on content and age
    /// Keeps actionable errors longer, removes resolved/known issues
    /// </summary>
    public void PerformIntelligentCleanup()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Logger.Warning("📁 Logs directory not found: {LogsDirectory}", _logsDirectory);
                return;
            }

            var logFiles = Directory.GetFiles(_logsDirectory, "*.log", SearchOption.TopDirectoryOnly);
            var cleanupSummary = new
            {
                TotalFiles = logFiles.Length,
                FilesProcessed = 0,
                FilesArchived = 0,
                FilesDeleted = 0,
                SpaceFreed = 0L
            };

            Logger.Information("🧹 Starting intelligent log cleanup for {FileCount} files", logFiles.Length);

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                var fileName = Path.GetFileName(logFile);

                // Skip current day files
                if (fileInfo.LastWriteTime.Date == DateTime.Today)
                {
                    continue;
                }

                // Different retention policies based on log type
                var shouldDelete = fileName switch
                {
                    var name when name.Contains("errors-actionable") => ShouldDeleteActionableErrors(fileInfo),
                    var name when name.Contains("ui-interactions") => ShouldDeleteUILogs(fileInfo),
                    var name when name.Contains("application") => ShouldDeleteApplicationLogs(fileInfo),
                    var name when name.Contains("busbuddy-errors") => ShouldDeleteOldErrorLogs(fileInfo),
                    var name when name.Contains("busbuddy-consolidated") => ShouldDeleteConsolidatedLogs(fileInfo),
                    _ => ShouldDeleteOtherLogs(fileInfo)
                };

                if (shouldDelete)
                {
                    try
                    {
                        var fileSize = fileInfo.Length;
                        File.Delete(logFile);
                        cleanupSummary = cleanupSummary with
                        {
                            FilesDeleted = cleanupSummary.FilesDeleted + 1,
                            SpaceFreed = cleanupSummary.SpaceFreed + fileSize
                        };

                        Logger.Debug("🗑️ Deleted log file: {FileName} ({FileSize:N0} bytes)", fileName, fileSize);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "⚠️ Failed to delete log file: {FileName}", fileName);
                    }
                }

                cleanupSummary = cleanupSummary with { FilesProcessed = cleanupSummary.FilesProcessed + 1 };
            }

            Logger.Information("✅ Log cleanup completed: {ProcessedCount} processed, {DeletedCount} deleted, {SpaceFreed:N0} bytes freed",
                cleanupSummary.FilesProcessed, cleanupSummary.FilesDeleted, cleanupSummary.SpaceFreed);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ Failed to perform log cleanup: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Actionable errors are kept longer (30 days) as they need developer attention
    /// </summary>
    private static bool ShouldDeleteActionableErrors(FileInfo fileInfo)
    {
        return fileInfo.LastWriteTime < DateTime.Now.AddDays(-30);
    }

    /// <summary>
    /// UI interaction logs are kept for shorter period (3 days) as they're primarily for debugging
    /// </summary>
    private static bool ShouldDeleteUILogs(FileInfo fileInfo)
    {
        return fileInfo.LastWriteTime < DateTime.Now.AddDays(-3);
    }

    /// <summary>
    /// Application logs are kept for standard period (7 days)
    /// </summary>
    private static bool ShouldDeleteApplicationLogs(FileInfo fileInfo)
    {
        return fileInfo.LastWriteTime < DateTime.Now.AddDays(-7);
    }

    /// <summary>
    /// Old error logs from previous versions are cleaned up more aggressively
    /// but we check if they contain resolved issues (ButtonAdv conflicts)
    /// </summary>
    private static bool ShouldDeleteOldErrorLogs(FileInfo fileInfo)
    {
        // Clean up old error logs after 7 days, or immediately if they contain resolved ButtonAdv issues
        if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-7))
        {
            return true;
        }

        // Check if file contains mostly resolved ButtonAdv errors
        try
        {
            var content = File.ReadAllText(fileInfo.FullName);
            var totalLines = content.Split('\n').Length;
            var buttonAdvErrorLines = content.Split('\n').Count(line =>
                line.Contains("ButtonAdv") && line.Contains("TargetType does not match"));

            // If more than 80% of errors are ButtonAdv style conflicts, delete the file
            if (totalLines > 10 && (double)buttonAdvErrorLines / totalLines > 0.8)
            {
                return true;
            }
        }
        catch
        {
            // If we can't read the file, keep it to be safe
        }

        return false;
    }

    /// <summary>
    /// Consolidated logs are kept for moderate period (14 days)
    /// </summary>
    private static bool ShouldDeleteConsolidatedLogs(FileInfo fileInfo)
    {
        return fileInfo.LastWriteTime < DateTime.Now.AddDays(-14);
    }

    /// <summary>
    /// Other logs (build, diagnostic, etc.) are kept for short period (5 days)
    /// </summary>
    private static bool ShouldDeleteOtherLogs(FileInfo fileInfo)
    {
        return fileInfo.LastWriteTime < DateTime.Now.AddDays(-5);
    }

    /// <summary>
    /// Gets summary of current log files and their sizes
    /// </summary>
    public LogSummary GetLogSummary()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                return new LogSummary { TotalFiles = 0, TotalSize = 0, Categories = new() };
            }

            var logFiles = Directory.GetFiles(_logsDirectory, "*.log", SearchOption.TopDirectoryOnly);
            var summary = new LogSummary
            {
                TotalFiles = logFiles.Length,
                TotalSize = logFiles.Sum(f => new FileInfo(f).Length),
                Categories = logFiles
                    .GroupBy(f => GetLogCategory(Path.GetFileName(f)))
                    .ToDictionary(
                        g => g.Key,
                        g => new LogCategoryInfo
                        {
                            FileCount = g.Count(),
                            TotalSize = g.Sum(f => new FileInfo(f).Length),
                            OldestFile = g.Min(f => new FileInfo(f).LastWriteTime),
                            NewestFile = g.Max(f => new FileInfo(f).LastWriteTime)
                        })
            };

            return summary;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "❌ Failed to get log summary: {ErrorMessage}", ex.Message);
            return new LogSummary { TotalFiles = 0, TotalSize = 0, Categories = new() };
        }
    }

    private static string GetLogCategory(string fileName)
    {
        return fileName switch
        {
            var name when name.Contains("errors-actionable") => "Actionable Errors",
            var name when name.Contains("ui-interactions") => "UI Interactions",
            var name when name.Contains("application") => "Application",
            var name when name.Contains("busbuddy-errors") => "Legacy Errors",
            var name when name.Contains("busbuddy-consolidated") => "Consolidated",
            var name when name.Contains("build") => "Build",
            var name when name.Contains("diagnostic") => "Diagnostic",
            var name when name.Contains("performance") => "Performance",
            _ => "Other"
        };
    }
}

/// <summary>
/// Summary information about log files
/// </summary>
public record LogSummary
{
    public int TotalFiles { get; init; }
    public long TotalSize { get; init; }
    public Dictionary<string, LogCategoryInfo> Categories { get; init; } = new();
}

/// <summary>
/// Information about a category of log files
/// </summary>
public record LogCategoryInfo
{
    public int FileCount { get; init; }
    public long TotalSize { get; init; }
    public DateTime OldestFile { get; init; }
    public DateTime NewestFile { get; init; }
}
