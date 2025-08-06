using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Utility class for filtering debug output and extracting actionable errors/warnings
    /// Enhanced with real-time streaming capabilities using FileSystemWatcher
    /// </summary>
    public static class DebugOutputFilter
    {
        private static readonly ILogger _logger = Log.ForContext("SourceContext", "BusBuddy.WPF.Utilities.DebugOutputFilter");
        private static FileSystemWatcher? _logFileWatcher;
        private static readonly object _lockObject = new object();
        private static bool _isStreamingEnabled;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static readonly List<FilteredDebugEntry> _recentEntries = new List<FilteredDebugEntry>();
        private static readonly int _maxRecentEntries = 100;

        /// <summary>
        /// Event triggered when priority 1-2 issues are detected
        /// </summary>
        public static event EventHandler<FilteredDebugEntry>? HighPriorityIssueDetected;

        /// <summary>
        /// Event triggered when new filtered entries are available
        /// </summary>
        public static event EventHandler<List<FilteredDebugEntry>>? NewEntriesFiltered;

        /// <summary>
        /// Filter categories for debug output analysis
        /// </summary>
        public enum FilterCategory
        {
            ActionableErrors,
            ActionableWarnings,
            CompilationErrors,
            RuntimeExceptions,
            XamlErrors,
            SyncfusionIssues,
            DatabaseErrors,
            NetworkErrors,
            PerformanceWarnings,
            SecurityWarnings,
            StartupErrors,
            NavigationErrors,
            SportsSchedulingErrors,
            UIThemeIssues,
            ResourceDictionaryErrors,
            StyleApplicationErrors,
            ColorSchemeProblems,
            FluentDarkThemeViolations,
            All
        }

        /// <summary>
        /// Represents a filtered debug entry with context
        /// </summary>
        public class FilteredDebugEntry
        {
            public string Timestamp { get; set; } = string.Empty;
            public FilterCategory Category { get; set; }
            public string Level { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string StackTrace { get; set; } = string.Empty;
            public string ActionableRecommendation { get; set; } = string.Empty;
            public string Context { get; set; } = string.Empty;
            public int Priority { get; set; } // 1 = Critical, 2 = High, 3 = Medium, 4 = Low
            public bool IsResolved { get; set; }
            public string Id { get; set; } = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
            public DateTime DetectedAt { get; set; } = DateTime.Now;
            public string ThreadId { get; set; } = string.Empty;
            public string ProcessId { get; set; } = string.Empty;
            public Dictionary<string, string> ExtendedProperties { get; set; } = new Dictionary<string, string>();
        }

        /// <summary>
        /// Predefined filter patterns for different error types
        /// </summary>
        private static readonly Dictionary<FilterCategory, List<Regex>> FilterPatterns = new()
        {
            [FilterCategory.ActionableErrors] = new List<Regex>
            {
                new Regex(@"🚨 ACTIONABLE.*EXCEPTION", RegexOptions.IgnoreCase),
                new Regex(@"ERROR.*\[.*\].*", RegexOptions.IgnoreCase),
                new Regex(@"CRITICAL.*ERROR", RegexOptions.IgnoreCase),
                new Regex(@"FATAL.*", RegexOptions.IgnoreCase)
            },
            [FilterCategory.ActionableWarnings] = new List<Regex>
            {
                new Regex(@"WARNING.*actionable", RegexOptions.IgnoreCase),
                new Regex(@"⚠️.*", RegexOptions.IgnoreCase),
                new Regex(@"WARN.*\[.*\]", RegexOptions.IgnoreCase)
            },
            [FilterCategory.CompilationErrors] = new List<Regex>
            {
                new Regex(@"error CS\d+", RegexOptions.IgnoreCase),
                new Regex(@"Build FAILED", RegexOptions.IgnoreCase),
                new Regex(@"compilation error", RegexOptions.IgnoreCase)
            },
            [FilterCategory.RuntimeExceptions] = new List<Regex>
            {
                new Regex(@"Exception.*at.*line", RegexOptions.IgnoreCase),
                new Regex(@"System\..*Exception", RegexOptions.IgnoreCase),
                new Regex(@"Unhandled exception", RegexOptions.IgnoreCase)
            },
            [FilterCategory.XamlErrors] = new List<Regex>
            {
                new Regex(@"XamlParseException", RegexOptions.IgnoreCase),
                new Regex(@"XAML.*error", RegexOptions.IgnoreCase),
                new Regex(@"StaticResource.*not found", RegexOptions.IgnoreCase),
                new Regex(@"binding.*error", RegexOptions.IgnoreCase),
                new Regex(@"BindingExpression path error", RegexOptions.IgnoreCase),
                new Regex(@"Cannot find resource named", RegexOptions.IgnoreCase),
                new Regex(@"System\.Windows\.Markup\.XamlParseException", RegexOptions.IgnoreCase),
                new Regex(@"Cannot resolve all property references", RegexOptions.IgnoreCase),
                new Regex(@"The name .* does not exist in the namespace", RegexOptions.IgnoreCase),
                new Regex(@"Property .* was not found in type", RegexOptions.IgnoreCase),
                new Regex(@"The property .* was not found on", RegexOptions.IgnoreCase),
                new Regex(@"Cannot convert string .* to type", RegexOptions.IgnoreCase),
                new Regex(@"TargetParameterCountException", RegexOptions.IgnoreCase),
                new Regex(@"InvalidOperationException.*binding", RegexOptions.IgnoreCase),
                new Regex(@"Binding.*failed", RegexOptions.IgnoreCase),
                new Regex(@"Two-way binding requires Path or XPath", RegexOptions.IgnoreCase),
                new Regex(@"Cannot find governing FrameworkElement", RegexOptions.IgnoreCase),
                new Regex(@"DataContext.*null", RegexOptions.IgnoreCase),
                new Regex(@"RelativeSource.*not found", RegexOptions.IgnoreCase),
                new Regex(@"ElementName.*not found", RegexOptions.IgnoreCase)
            },
            [FilterCategory.SyncfusionIssues] = new List<Regex>
            {
                new Regex(@"Syncfusion.*error", RegexOptions.IgnoreCase),
                new Regex(@"ButtonAdv.*TargetType", RegexOptions.IgnoreCase),
                new Regex(@"FluentDark.*theme", RegexOptions.IgnoreCase),
                new Regex(@"SfSkinManager", RegexOptions.IgnoreCase)
            },
            [FilterCategory.DatabaseErrors] = new List<Regex>
            {
                new Regex(@"Database.*connection", RegexOptions.IgnoreCase),
                new Regex(@"SQL.*error", RegexOptions.IgnoreCase),
                new Regex(@"DbContext.*disposed", RegexOptions.IgnoreCase),
                new Regex(@"Entity Framework", RegexOptions.IgnoreCase)
            },
            [FilterCategory.NetworkErrors] = new List<Regex>
            {
                new Regex(@"Network.*error", RegexOptions.IgnoreCase),
                new Regex(@"Connection.*timeout", RegexOptions.IgnoreCase),
                new Regex(@"HTTP.*error", RegexOptions.IgnoreCase)
            },
            [FilterCategory.PerformanceWarnings] = new List<Regex>
            {
                new Regex(@"Performance.*warning", RegexOptions.IgnoreCase),
                new Regex(@"slow.*operation", RegexOptions.IgnoreCase),
                new Regex(@"timeout.*exceeded", RegexOptions.IgnoreCase),
                new Regex(@"memory.*usage", RegexOptions.IgnoreCase)
            },
            [FilterCategory.SecurityWarnings] = new List<Regex>
            {
                new Regex(@"Security.*warning", RegexOptions.IgnoreCase),
                new Regex(@"sensitive.*data", RegexOptions.IgnoreCase),
                new Regex(@"authentication.*failed", RegexOptions.IgnoreCase)
            },
            [FilterCategory.StartupErrors] = new List<Regex>
            {
                new Regex(@"STARTUP.*error", RegexOptions.IgnoreCase),
                new Regex(@"initialization.*failed", RegexOptions.IgnoreCase),
                new Regex(@"startup.*exception", RegexOptions.IgnoreCase)
            },
            [FilterCategory.NavigationErrors] = new List<Regex>
            {
                new Regex(@"Navigation.*error", RegexOptions.IgnoreCase),
                new Regex(@"ViewModel.*not found", RegexOptions.IgnoreCase),
                new Regex(@"View.*binding.*error", RegexOptions.IgnoreCase)
            },
            [FilterCategory.SportsSchedulingErrors] = new List<Regex>
            {
                new Regex(@"Schedules Log.*error", RegexOptions.IgnoreCase),
                new Regex(@"ScheduleView.*error", RegexOptions.IgnoreCase),
                new Regex(@"Sports.*scheduling.*error", RegexOptions.IgnoreCase),
                new Regex(@"Schedule.*Category.*error", RegexOptions.IgnoreCase)
            },
            [FilterCategory.UIThemeIssues] = new List<Regex>
            {
                new Regex(@"SfSkinManager\.ApplyThemeAsDefaultStyle", RegexOptions.IgnoreCase),
                new Regex(@"SfSkinManager\.SetTheme", RegexOptions.IgnoreCase),
                new Regex(@"theme.*not.*applied", RegexOptions.IgnoreCase),
                new Regex(@"FluentDark.*theme.*error", RegexOptions.IgnoreCase),
                new Regex(@"Office2019Colorful.*theme.*error", RegexOptions.IgnoreCase),
                new Regex(@"SfSkinManager.*error", RegexOptions.IgnoreCase),
                new Regex(@"ApplyStylesOnApplication.*error", RegexOptions.IgnoreCase),
                new Regex(@"theme.*resource.*not.*found", RegexOptions.IgnoreCase)
            },
            [FilterCategory.ResourceDictionaryErrors] = new List<Regex>
            {
                new Regex(@"ResourceDictionary.*error", RegexOptions.IgnoreCase),
                new Regex(@"DeferrableContent.*threw.*exception", RegexOptions.IgnoreCase),
                new Regex(@"Item has already been added.*Key in dictionary", RegexOptions.IgnoreCase),
                new Regex(@"resource.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"StaticResource.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"DynamicResource.*not.*found", RegexOptions.IgnoreCase)
            },
            [FilterCategory.StyleApplicationErrors] = new List<Regex>
            {
                new Regex(@"Style.*not.*applied", RegexOptions.IgnoreCase),
                new Regex(@"TargetType.*mismatch", RegexOptions.IgnoreCase),
                new Regex(@"Style.*resource.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"Setter.*Property.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"Template.*not.*found", RegexOptions.IgnoreCase)
            },
            [FilterCategory.ColorSchemeProblems] = new List<Regex>
            {
                new Regex(@"color.*scheme.*error", RegexOptions.IgnoreCase),
                new Regex(@"SolidColorBrush.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"Color.*resource.*not.*found", RegexOptions.IgnoreCase),
                new Regex(@"foreground.*background.*contrast", RegexOptions.IgnoreCase),
                new Regex(@"accessibility.*color.*contrast", RegexOptions.IgnoreCase)
            },
            [FilterCategory.FluentDarkThemeViolations] = new List<Regex>
            {
                new Regex(@"FluentDark.*violation", RegexOptions.IgnoreCase),
                new Regex(@"theme.*consistency.*error", RegexOptions.IgnoreCase),
                new Regex(@"Surface.*color.*incorrect", RegexOptions.IgnoreCase),
                new Regex(@"Primary.*color.*incorrect", RegexOptions.IgnoreCase),
                new Regex(@"Accent.*color.*incorrect", RegexOptions.IgnoreCase),
                new Regex(@"Text.*color.*incorrect", RegexOptions.IgnoreCase),
                new Regex(@"Border.*color.*incorrect", RegexOptions.IgnoreCase)
            }
        };

        /// <summary>
        /// Starts real-time streaming of log files using FileSystemWatcher
        /// </summary>
        public static void StartRealTimeStreaming()
        {
            if (_isStreamingEnabled)
            {
                return;
            }

            try
            {
                string solutionRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName
                                   ?? Directory.GetCurrentDirectory();
                string logsDirectory = Path.Combine(solutionRoot, "logs");

                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                _cancellationTokenSource = new CancellationTokenSource();
                _logFileWatcher = new FileSystemWatcher(logsDirectory, "*.log")
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
                };

                _logFileWatcher.Changed += OnLogFileChanged;
                _logFileWatcher.Created += OnLogFileCreated;
                _logFileWatcher.Error += OnLogFileWatcherError;

                _isStreamingEnabled = true;
                _logger.Information("Real-time log streaming started for directory: {LogsDirectory}", logsDirectory);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start real-time log streaming");
            }
        }

        /// <summary>
        /// Stops real-time streaming of log files
        /// </summary>
        public static void StopRealTimeStreaming()
        {
            if (!_isStreamingEnabled)
            {
                return;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                _logFileWatcher?.Dispose();
                _logFileWatcher = null;
                _isStreamingEnabled = false;
                _logger.Information("Real-time log streaming stopped");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping real-time log streaming");
            }
        }

        /// <summary>
        /// Event handler for log file changes
        /// </summary>
        private static async void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true)
            {
                return;
            }

            try
            {
                await ProcessLogFileChangesAsync(e.FullPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing log file changes for {FilePath}", e.FullPath);
            }
        }

        /// <summary>
        /// Event handler for new log file creation
        /// </summary>
        private static async void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true)
            {
                return;
            }

            try
            {
                await ProcessLogFileChangesAsync(e.FullPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing new log file {FilePath}", e.FullPath);
            }
        }

        /// <summary>
        /// Event handler for file watcher errors
        /// </summary>
        private static void OnLogFileWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.Error(e.GetException(), "FileSystemWatcher error occurred");
        }

        /// <summary>
        /// Processes changes in log files and filters for actionable items
        /// </summary>
        private static async Task ProcessLogFileChangesAsync(string filePath)
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true)
            {
                return;
            }

            try
            {
                // Small delay to ensure file is fully written
                await Task.Delay(100, _cancellationTokenSource?.Token ?? CancellationToken.None);

                var newEntries = await FilterLogFileAsync(filePath, FilterCategory.All, true, true);

                if (newEntries.Count > 0)
                {
                    lock (_lockObject)
                    {
                        // Add new entries to recent entries collection
                        _recentEntries.AddRange(newEntries);

                        // Keep only the most recent entries
                        if (_recentEntries.Count > _maxRecentEntries)
                        {
                            _recentEntries.RemoveRange(0, _recentEntries.Count - _maxRecentEntries);
                        }
                    }

                    // Check for high priority issues
                    var highPriorityIssues = newEntries.Where(e => e.Priority <= 2).ToList();
                    foreach (var issue in highPriorityIssues)
                    {
                        HighPriorityIssueDetected?.Invoke(null, issue);
                        _logger.Warning("High priority issue detected: {Message}", issue.Message);
                    }

                    // Notify about new entries
                    NewEntriesFiltered?.Invoke(null, newEntries);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing log file changes for {FilePath}", filePath);
            }
        }

        /// <summary>
        /// Gets recent entries from the streaming buffer
        /// </summary>
        public static List<FilteredDebugEntry> GetRecentStreamingEntries(int maxEntries = 50)
        {
            lock (_lockObject)
            {
                return _recentEntries.TakeLast(maxEntries).ToList();
            }
        }

        /// <summary>
        /// Exports filtered results to JSON format for VS Code integration
        /// </summary>
        public static async Task<string> ExportToJsonAsync(List<FilteredDebugEntry> entries, string? filePath = null)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var exportData = new
                {
                    Metadata = new
                    {
                        GeneratedAt = DateTime.Now,
                        TotalEntries = entries.Count,
                        CriticalIssues = entries.Count(e => e.Priority == 1),
                        HighPriorityIssues = entries.Count(e => e.Priority == 2),
                        MediumPriorityIssues = entries.Count(e => e.Priority == 3),
                        LowPriorityIssues = entries.Count(e => e.Priority == 4)
                    },
                    Entries = entries.OrderBy(e => e.Priority).ThenByDescending(e => e.DetectedAt)
                };

                var jsonString = JsonSerializer.Serialize(exportData, jsonOptions);

                if (!string.IsNullOrEmpty(filePath))
                {
                    await File.WriteAllTextAsync(filePath, jsonString);
                    _logger.Information("Debug entries exported to JSON file: {FilePath}", filePath);
                }

                return jsonString;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error exporting to JSON");
                return JsonSerializer.Serialize(new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Exports current actionable items to JSON for VS Code integration
        /// </summary>
        public static async Task<string> ExportActionableItemsToJsonAsync(string? filePath = null)
        {
            try
            {
                var actionableItems = await GetActionableItemsForJsonExportAsync();

                if (string.IsNullOrEmpty(filePath))
                {
                    string solutionRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName
                                       ?? Directory.GetCurrentDirectory();
                    filePath = Path.Combine(solutionRoot, "logs", $"actionable-items-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                }

                return await ExportToJsonAsync(actionableItems, filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error exporting actionable items to JSON");
                return JsonSerializer.Serialize(new { Error = ex.Message });
            }
        }

        /// <summary>
        /// Gets actionable items formatted for JSON export
        /// </summary>
        private static async Task<List<FilteredDebugEntry>> GetActionableItemsForJsonExportAsync()
        {
            var actionableErrors = await FilterDebugOutputAsync(FilterCategory.ActionableErrors, true, true, 20);
            var actionableWarnings = await FilterDebugOutputAsync(FilterCategory.ActionableWarnings, true, true, 20);
            var runtimeExceptions = await FilterDebugOutputAsync(FilterCategory.RuntimeExceptions, true, true, 10);
            var xamlErrors = await FilterDebugOutputAsync(FilterCategory.XamlErrors, true, true, 15);
            var sportsSchedulingErrors = await FilterDebugOutputAsync(FilterCategory.SportsSchedulingErrors, true, true, 10);
            var uiThemeIssues = await FilterDebugOutputAsync(FilterCategory.UIThemeIssues, true, true, 15);
            var resourceDictionaryErrors = await FilterDebugOutputAsync(FilterCategory.ResourceDictionaryErrors, true, true, 15);
            var styleApplicationErrors = await FilterDebugOutputAsync(FilterCategory.StyleApplicationErrors, true, true, 15);

            var allActionableItems = new List<FilteredDebugEntry>();
            allActionableItems.AddRange(actionableErrors);
            allActionableItems.AddRange(actionableWarnings);
            allActionableItems.AddRange(runtimeExceptions);
            allActionableItems.AddRange(xamlErrors);
            allActionableItems.AddRange(sportsSchedulingErrors);
            allActionableItems.AddRange(uiThemeIssues);
            allActionableItems.AddRange(resourceDictionaryErrors);
            allActionableItems.AddRange(styleApplicationErrors);

            // Remove duplicates and sort by priority
            return allActionableItems
                .GroupBy(x => x.Message)
                .Select(g => g.First())
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.DetectedAt)
                .Take(50)
                .ToList();
        }

        /// <summary>
        /// Quick debug filter - call this in debug mode to get only actionable items
        /// Usage: var actionableItems = await DebugOutputFilter.GetActionableItemsAsync();
        /// </summary>
        public static async Task<string> GetActionableItemsAsync()
        {
            var actionableErrors = await FilterDebugOutputAsync(FilterCategory.ActionableErrors, true, true, 20);
            var actionableWarnings = await FilterDebugOutputAsync(FilterCategory.ActionableWarnings, true, true, 20);
            var runtimeExceptions = await FilterDebugOutputAsync(FilterCategory.RuntimeExceptions, true, true, 10);
            var xamlErrors = await FilterDebugOutputAsync(FilterCategory.XamlErrors, true, true, 10);
            var sportsSchedulingErrors = await FilterDebugOutputAsync(FilterCategory.SportsSchedulingErrors, true, true, 10);
            var uiThemeIssues = await FilterDebugOutputAsync(FilterCategory.UIThemeIssues, true, true, 15);
            var resourceDictionaryErrors = await FilterDebugOutputAsync(FilterCategory.ResourceDictionaryErrors, true, true, 15);
            var styleApplicationErrors = await FilterDebugOutputAsync(FilterCategory.StyleApplicationErrors, true, true, 15);
            var colorSchemeProblems = await FilterDebugOutputAsync(FilterCategory.ColorSchemeProblems, true, true, 15);
            var fluentDarkThemeViolations = await FilterDebugOutputAsync(FilterCategory.FluentDarkThemeViolations, true, true, 15);

            var allActionableItems = new List<FilteredDebugEntry>();
            allActionableItems.AddRange(actionableErrors);
            allActionableItems.AddRange(actionableWarnings);
            allActionableItems.AddRange(runtimeExceptions);
            allActionableItems.AddRange(xamlErrors);
            allActionableItems.AddRange(sportsSchedulingErrors);
            allActionableItems.AddRange(uiThemeIssues);
            allActionableItems.AddRange(resourceDictionaryErrors);
            allActionableItems.AddRange(styleApplicationErrors);
            allActionableItems.AddRange(colorSchemeProblems);
            allActionableItems.AddRange(fluentDarkThemeViolations);

            // Remove duplicates and sort by priority
            var uniqueItems = allActionableItems
                .GroupBy(x => x.Message)
                .Select(g => g.First())
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.Timestamp)
                .Take(30)
                .ToList();

            if (uniqueItems.Count == 0)
            {
                return "✅ No actionable items found in debug output - application is running cleanly!";
            }

            var output = new System.Text.StringBuilder();
            output.AppendLine(CultureInfo.InvariantCulture, $"🔍 ACTIONABLE DEBUG ITEMS ({uniqueItems.Count} items found)");
            output.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTime.Now:HH:mm:ss}");
            output.AppendLine(new string('=', 80));

            foreach (var item in uniqueItems)
            {
                var priorityIcon = item.Priority switch
                {
                    1 => "🚨 CRITICAL",
                    2 => "⚠️  HIGH",
                    3 => "🔶 MEDIUM",
                    _ => "ℹ️  LOW"
                };

                output.AppendLine(CultureInfo.InvariantCulture, $"{priorityIcon} [{item.Category}] {item.Level}");
                output.AppendLine(CultureInfo.InvariantCulture, $"📝 {item.Message}");

                if (!string.IsNullOrEmpty(item.ActionableRecommendation))
                {
                    output.AppendLine(CultureInfo.InvariantCulture, $"🎯 ACTION: {item.ActionableRecommendation}");
                }

                if (!string.IsNullOrEmpty(item.StackTrace))
                {
                    output.AppendLine(CultureInfo.InvariantCulture, $"📍 LOCATION: {item.StackTrace}");
                }

                output.AppendLine(new string('-', 40));
            }

            return output.ToString();
        }

        /// <summary>
        /// Quick console output for debugging - prints actionable items to console
        /// Usage: DebugOutputFilter.PrintActionableItems();
        /// </summary>
        public static void PrintActionableItems()
        {
            try
            {
                var task = GetActionableItemsAsync();
                var result = task.GetAwaiter().GetResult();

                Console.WriteLine(result);
                System.Diagnostics.Debug.WriteLine(result);

                // Also log to Serilog if available
                _logger.Information("Debug output filter results:\n{FilterResults}", result);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error filtering debug output: {ex.Message}";
                Console.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine(errorMsg);
                _logger.Error(ex, "Error in PrintActionableItems");
            }
        }

        /// <summary>
        /// Real-time debug filter for current debug session
        /// Monitors debug output and shows only actionable items
        /// </summary>
        public static async Task<List<string>> GetCurrentSessionActionableItemsAsync()
        {
            var actionableItems = new List<string>();

            try
            {
                // Check recent log entries (last 5 minutes)
                var recentTime = DateTime.Now.AddMinutes(-5);
                var allEntries = await FilterDebugOutputAsync(FilterCategory.All, true, true, 50);

                var recentEntries = allEntries.Where(e =>
                {
                    if (DateTime.TryParse(e.Timestamp, out var timestamp))
                    {
                        return timestamp >= recentTime;
                    }
                    return true; // Include if we can't parse timestamp
                }).ToList();

                foreach (var entry in recentEntries.OrderBy(e => e.Priority))
                {
                    var priorityIcon = entry.Priority switch
                    {
                        1 => "🚨",
                        2 => "⚠️",
                        3 => "🔶",
                        _ => "ℹ️"
                    };

                    var actionableItem = $"{priorityIcon} [{entry.Category}] {entry.Message}";

                    if (!string.IsNullOrEmpty(entry.ActionableRecommendation))
                    {
                        actionableItem += $"\n   🎯 ACTION: {entry.ActionableRecommendation}";
                    }

                    actionableItems.Add(actionableItem);
                }
            }
            catch (Exception ex)
            {
                actionableItems.Add($"Error filtering current session: {ex.Message}");
            }

            return actionableItems;
        }

        /// <summary>
        /// Filters debug output from multiple sources (console, files, etc.)
        /// </summary>
        public static async Task<List<FilteredDebugEntry>> FilterDebugOutputAsync(
            FilterCategory category = FilterCategory.ActionableErrors,
            bool includeContext = true,
            bool includeRecommendations = true,
            int maxEntries = 100)
        {
            using (LogContext.PushProperty("LogCategory", "Debug Filter"))
            {
                _logger.Information("Starting debug output filtering for category: {Category}", category);

                var results = new List<FilteredDebugEntry>();

                try
                {
                    // Get logs directory
                    string solutionRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName
                                       ?? Directory.GetCurrentDirectory();
                    string logsDirectory = Path.Combine(solutionRoot, "logs");

                    if (!Directory.Exists(logsDirectory))
                    {
                        _logger.Warning("Logs directory not found: {LogsDirectory}", logsDirectory);
                        return results;
                    }

                    // Get all log files
                    var logFiles = Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly)
                                            .OrderByDescending(f => File.GetLastWriteTime(f))
                                            .Take(5); // Only check recent log files

                    foreach (var logFile in logFiles)
                    {
                        var fileEntries = await FilterLogFileAsync(logFile, category, includeContext, includeRecommendations);
                        results.AddRange(fileEntries);
                    }

                    // Also check console output if available
                    var consoleEntries = await FilterConsoleOutputAsync(category, includeContext, includeRecommendations);
                    results.AddRange(consoleEntries);

                    // Sort by priority and timestamp
                    results = results.OrderBy(e => e.Priority)
                                   .ThenByDescending(e => e.Timestamp)
                                   .Take(maxEntries)
                                   .ToList();

                    _logger.Information("Debug output filtering completed. Found {Count} entries for category: {Category}",
                        results.Count, category);

                    return results;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error filtering debug output");
                    return results;
                }
            }
        }

        /// <summary>
        /// Filters a specific log file
        /// </summary>
        private static async Task<List<FilteredDebugEntry>> FilterLogFileAsync(
            string logFilePath,
            FilterCategory category,
            bool includeContext,
            bool includeRecommendations)
        {
            var entries = new List<FilteredDebugEntry>();

            try
            {
                if (!File.Exists(logFilePath))
                {
                    return entries;
                }

                // Use FileShare.ReadWrite to allow reading while file is being written to
                string[] lines;
                using (var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream))
                {
                    var content = await reader.ReadToEndAsync();
                    lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }

                var patterns = GetPatternsForCategory(category);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    foreach (var pattern in patterns)
                    {
                        if (pattern.IsMatch(line))
                        {
                            var entry = ParseLogEntry(line, i, lines, includeContext, includeRecommendations);
                            entry.Category = category;
                            entry.Source = Path.GetFileName(logFilePath);
                            entries.Add(entry);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error filtering log file: {LogFile}", logFilePath);
            }

            return entries;
        }

        /// <summary>
        /// Filters console output (if available)
        /// </summary>
        private static async Task<List<FilteredDebugEntry>> FilterConsoleOutputAsync(
            FilterCategory category,
            bool includeContext,
            bool includeRecommendations)
        {
            var entries = new List<FilteredDebugEntry>();

            try
            {
                // This would capture console output if we have a way to access it
                // For now, we'll focus on log files
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error filtering console output");
            }

            return entries;
        }

        /// <summary>
        /// Parses a log entry and extracts actionable information
        /// </summary>
        private static FilteredDebugEntry ParseLogEntry(
            string line,
            int lineIndex,
            string[] allLines,
            bool includeContext,
            bool includeRecommendations)
        {
            var entry = new FilteredDebugEntry
            {
                DetectedAt = DateTime.Now,
                ThreadId = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture),
                ProcessId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            };

            // Extract timestamp
            var timestampMatch = Regex.Match(line, @"\[(.*?)\]");
            if (timestampMatch.Success)
            {
                entry.Timestamp = timestampMatch.Groups[1].Value;
            }

            // Extract level
            var levelMatch = Regex.Match(line, @"\[(ERROR|WARN|INFO|DEBUG|FATAL)\]", RegexOptions.IgnoreCase);
            if (levelMatch.Success)
            {
                entry.Level = levelMatch.Groups[1].Value.ToUpper(CultureInfo.InvariantCulture);
            }

            // Extract message
            entry.Message = line;

            // Set priority based on keywords
            entry.Priority = GetPriorityFromMessage(line);

            // Extract context (surrounding lines)
            if (includeContext)
            {
                var contextLines = new List<string>();
                int start = Math.Max(0, lineIndex - 2);
                int end = Math.Min(allLines.Length - 1, lineIndex + 2);

                for (int i = start; i <= end; i++)
                {
                    contextLines.Add($"{i + 1}: {allLines[i]}");
                }

                entry.Context = string.Join("\n", contextLines);
            }

            // Extract actionable recommendation
            if (includeRecommendations)
            {
                entry.ActionableRecommendation = GetActionableRecommendationFromMessage(line);
            }

            // Extract stack trace if available
            if (lineIndex + 1 < allLines.Length && allLines[lineIndex + 1].Contains("at "))
            {
                entry.StackTrace = allLines[lineIndex + 1];
            }

            // Extract extended properties
            ExtractExtendedProperties(line, entry);

            return entry;
        }

        /// <summary>
        /// Extracts extended properties from log entry
        /// </summary>
        private static void ExtractExtendedProperties(string line, FilteredDebugEntry entry)
        {
            // Extract correlation ID if present
            var correlationMatch = Regex.Match(line, @"CorrelationId[:\s]+([a-fA-F0-9\-]+)", RegexOptions.IgnoreCase);
            if (correlationMatch.Success)
            {
                entry.ExtendedProperties["CorrelationId"] = correlationMatch.Groups[1].Value;
            }

            // Extract operation name if present
            var operationMatch = Regex.Match(line, @"Operation[:\s]+([^\s\]]+)", RegexOptions.IgnoreCase);
            if (operationMatch.Success)
            {
                entry.ExtendedProperties["Operation"] = operationMatch.Groups[1].Value;
            }

            // Extract component name if present
            var componentMatch = Regex.Match(line, @"\[([A-Z][a-zA-Z]+)\]", RegexOptions.None);
            if (componentMatch.Success)
            {
                entry.ExtendedProperties["Component"] = componentMatch.Groups[1].Value;
            }

            // Extract exception type if present
            var exceptionMatch = Regex.Match(line, @"(System\.[a-zA-Z\.]+Exception)", RegexOptions.IgnoreCase);
            if (exceptionMatch.Success)
            {
                entry.ExtendedProperties["ExceptionType"] = exceptionMatch.Groups[1].Value;
            }
        }

        /// <summary>
        /// Gets filter patterns for a specific category
        /// </summary>
        private static List<Regex> GetPatternsForCategory(FilterCategory category)
        {
            if (category == FilterCategory.All)
            {
                return FilterPatterns.Values.SelectMany(p => p).ToList();
            }

            return FilterPatterns.ContainsKey(category) ? FilterPatterns[category] : new List<Regex>();
        }

        /// <summary>
        /// Determines priority based on message content
        /// </summary>
        private static int GetPriorityFromMessage(string message)
        {
            if (message.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase) || message.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
            {
                return 1; // Critical
            }

            if (message.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || message.Contains("🚨", StringComparison.Ordinal))
            {
                return 2; // High
            }

            if (message.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || message.Contains("⚠️", StringComparison.Ordinal))
            {
                return 3; // Medium
            }

            return 4; // Low
        }

        /// <summary>
        /// Extracts actionable recommendation from message
        /// </summary>
        private static string GetActionableRecommendationFromMessage(string message)
        {
            // Look for existing recommendations in the message
            var recommendationMatch = Regex.Match(message, @"ActionableRecommendation.*?:(.*?)(?:\||$)", RegexOptions.IgnoreCase);
            if (recommendationMatch.Success)
            {
                return recommendationMatch.Groups[1].Value.Trim();
            }

            // Generate recommendations based on message content
            if (message.Contains("XAML", StringComparison.OrdinalIgnoreCase))
            {
                return "Check XAML syntax, resource references, and binding expressions";
            }

            if (message.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
            {
                return "Verify Syncfusion license, theme settings, and control properties";
            }

            if (message.Contains("Database", StringComparison.OrdinalIgnoreCase) || message.Contains("SQL", StringComparison.OrdinalIgnoreCase))
            {
                return "Check database connection, query syntax, and DbContext lifecycle";
            }

            if (message.Contains("Navigation", StringComparison.OrdinalIgnoreCase))
            {
                return "Verify ViewModel registration, view mapping, and navigation parameters";
            }

            if (message.Contains("Sports", StringComparison.OrdinalIgnoreCase) || message.Contains("Schedule", StringComparison.OrdinalIgnoreCase))
            {
                return "Check sports scheduling data, category filters, and CRUD operations";
            }

            return "Review stack trace and implement appropriate error handling";
        }

        /// <summary>
        /// Exports filtered results to a readable format
        /// </summary>
        public static async Task<string> ExportFilteredResultsAsync(
            List<FilteredDebugEntry> entries,
            string format = "text")
        {
            using (LogContext.PushProperty("LogCategory", "Debug Filter"))
            {
                try
                {
                    if (string.Equals(format.ToLower(CultureInfo.InvariantCulture), "markdown", StringComparison.OrdinalIgnoreCase))
                    {
                        return await ExportToMarkdownAsync(entries);
                    }
                    else
                    {
                        return await ExportToTextAsync(entries);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error exporting filtered results");
                    return $"Error exporting results: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// Exports results to markdown format
        /// </summary>
        private static async Task<string> ExportToMarkdownAsync(List<FilteredDebugEntry> entries)
        {
            var markdown = new System.Text.StringBuilder();

            markdown.AppendLine("# 🔍 Debug Output Analysis Report");
            markdown.AppendLine(CultureInfo.InvariantCulture, $"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine(CultureInfo.InvariantCulture, $"**Total Entries:** {entries.Count}");
            markdown.AppendLine();

            var groupedEntries = entries.GroupBy(e => e.Category);

            foreach (var group in groupedEntries)
            {
                markdown.AppendLine(CultureInfo.InvariantCulture, $"## {group.Key}");
                markdown.AppendLine();

                foreach (var entry in group.OrderBy(e => e.Priority))
                {
                    var priorityIcon = entry.Priority switch
                    {
                        1 => "🚨",
                        2 => "⚠️",
                        3 => "🔶",
                        _ => "ℹ️"
                    };

                    markdown.AppendLine(CultureInfo.InvariantCulture, $"### {priorityIcon} {entry.Level} - {entry.Timestamp}");
                    markdown.AppendLine(CultureInfo.InvariantCulture, $"**Source:** {entry.Source}");
                    markdown.AppendLine(CultureInfo.InvariantCulture, $"**Message:** {entry.Message}");

                    if (!string.IsNullOrEmpty(entry.ActionableRecommendation))
                    {
                        markdown.AppendLine(CultureInfo.InvariantCulture, $"**🎯 Action Required:** {entry.ActionableRecommendation}");
                    }

                    if (!string.IsNullOrEmpty(entry.StackTrace))
                    {
                        markdown.AppendLine(CultureInfo.InvariantCulture, $"**Stack Trace:** `{entry.StackTrace}`");
                    }

                    markdown.AppendLine();
                }
            }

            return await Task.FromResult(markdown.ToString());
        }

        /// <summary>
        /// Exports results to text format
        /// </summary>
        private static async Task<string> ExportToTextAsync(List<FilteredDebugEntry> entries)
        {
            var text = new System.Text.StringBuilder();

            text.AppendLine("=== DEBUG OUTPUT ANALYSIS REPORT ===");
            text.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine(CultureInfo.InvariantCulture, $"Total Entries: {entries.Count}");
            text.AppendLine();

            foreach (var entry in entries.OrderBy(e => e.Priority))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"[{entry.Priority}] {entry.Level} - {entry.Timestamp}");
                text.AppendLine(CultureInfo.InvariantCulture, $"Source: {entry.Source}");
                text.AppendLine(CultureInfo.InvariantCulture, $"Category: {entry.Category}");
                text.AppendLine(CultureInfo.InvariantCulture, $"Message: {entry.Message}");

                if (!string.IsNullOrEmpty(entry.ActionableRecommendation))
                {
                    text.AppendLine(CultureInfo.InvariantCulture, $"Action Required: {entry.ActionableRecommendation}");
                }

                if (!string.IsNullOrEmpty(entry.StackTrace))
                {
                    text.AppendLine(CultureInfo.InvariantCulture, $"Stack Trace: {entry.StackTrace}");
                }

                text.AppendLine(new string('-', 80));
            }

            return await Task.FromResult(text.ToString());
        }

        /// <summary>
        /// Debug-friendly method to quickly show only what needs attention
        /// Add this to your debug code: var issues = DebugOutputFilter.ShowOnlyIssues();
        /// </summary>
        public static string ShowOnlyIssues()
        {
            try
            {
                var task = GetActionableItemsAsync();
                var result = task.GetAwaiter().GetResult();

                // Also write to debug output for immediate visibility
                System.Diagnostics.Debug.WriteLine("=== FILTERED DEBUG OUTPUT ===");
                System.Diagnostics.Debug.WriteLine(result);

                return result;
            }
            catch (Exception ex)
            {
                var error = $"Filter error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(error);
                return error;
            }
        }

        /// <summary>
        /// Quick method to check if there are any critical issues
        /// Returns true if critical issues found, false otherwise
        /// </summary>
        public static bool HasCriticalIssues()
        {
            try
            {
                var task = FilterDebugOutputAsync(FilterCategory.ActionableErrors, false, false, 5);
                var criticalErrors = task.GetAwaiter().GetResult();

                var hasCritical = criticalErrors.Any(e => e.Priority <= 2);

                if (hasCritical)
                {
                    System.Diagnostics.Debug.WriteLine("🚨 CRITICAL ISSUES DETECTED - Run DebugOutputFilter.ShowOnlyIssues() for details");
                }

                return hasCritical;
            }
            catch
            {
                return false;
            }
        }
    }
}
