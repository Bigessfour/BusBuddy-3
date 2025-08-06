using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Handles UI debugging features and responds to PowerShell debug commands
    /// Implements IPC with PowerShell via file-based communication
    /// </summary>
    public static class UIDebugManager
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(UIDebugManager));
        private static readonly string CommandFilePath = string.Empty;
        private static readonly string ResponseFilePath = string.Empty;
        private static readonly FileSystemWatcher? FileWatcher;

        // Track current debug state
        private static UIDebugMode CurrentMode = UIDebugMode.Normal;
        private static string CurrentTheme = SyncfusionThemeManager.PRIMARY_THEME;
        private static bool IsDebugOverlayVisible;

        // Debug visualization elements
        private static Dictionary<string, UIElement> DebugOverlays = new Dictionary<string, UIElement>();

        /// <summary>
        /// UI Debug modes available
        /// </summary>
        public enum UIDebugMode
        {
            Normal,
            Layout,
            Performance,
            Verbose
        }

        /// <summary>
        /// Static constructor to initialize paths and file watcher
        /// </summary>
        static UIDebugManager()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                // Try to find project root
                string projectRoot = basePath;
                while (!Directory.Exists(Path.Combine(projectRoot, ".git")) &&
                       Directory.GetParent(projectRoot) != null)
                {
                    projectRoot = Directory.GetParent(projectRoot)?.FullName ?? string.Empty;
                }

                // If we can't find project root, use temp directory
                if (!Directory.Exists(Path.Combine(projectRoot, ".git")))
                {
                    projectRoot = Path.GetTempPath();
                }

                CommandFilePath = Path.Combine(projectRoot, "bb_ui_debug_command.json");
                ResponseFilePath = Path.Combine(projectRoot, "bb_ui_debug_response.json");

                // Create and configure file system watcher
                FileWatcher = new FileSystemWatcher(projectRoot, "bb_ui_debug_command.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                FileWatcher.Changed += OnDebugCommandFileChanged;
                FileWatcher.Created += OnDebugCommandFileChanged;

                Logger.Information("[UIDebug] Initialized UI Debug Manager with command path: {CommandPath}", CommandFilePath);

#if DEBUG
                // Enable debug config tracking in DEBUG builds
                DebugConfig.EnableUITracking = true;
#endif
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[UIDebug] Failed to initialize UI Debug Manager");
            }
        }

        private static void OnDebugCommandFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Wait a moment to make sure the file is completely written
                Task.Delay(200).Wait();

                if (!File.Exists(CommandFilePath))
                {
                    return;
                }

                string jsonContent = File.ReadAllText(CommandFilePath);
                var debugCommand = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (debugCommand == null)
                {
                    return;
                }

                Logger.Debug("[UIDebug] Received debug command: {@DebugCommand}", debugCommand);

                string mode = debugCommand.ContainsKey("Mode") ? debugCommand["Mode"].ToString() ?? "Toggle" : "Toggle";
                string theme = debugCommand.ContainsKey("Theme") ? debugCommand["Theme"].ToString() ?? CurrentTheme : CurrentTheme;
                bool enableLogging = debugCommand.ContainsKey("EnableLogging") &&
                    bool.TryParse(debugCommand["EnableLogging"].ToString(), out bool logging) && logging;

                // Process the command on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessDebugCommand(mode, theme, enableLogging);
                });

                // Send response
                SendDebugResponse(true);

                // Clean up command file after processing
                try
                {
                    File.Delete(CommandFilePath);
                }
                catch (Exception)
                {
                    /* Ignore deletion errors */
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[UIDebug] Error processing debug command file");
                SendDebugResponse(false, ex.Message);
            }
        }

        private static void ProcessDebugCommand(string mode, string theme, bool enableLogging)
        {
            try
            {
                UIDebugMode newMode;

                if (mode == "Toggle")
                {
                    // Cycle through modes
                    int nextMode = ((int)CurrentMode + 1) % 4; // 4 = number of enum values
                    newMode = (UIDebugMode)nextMode;
                }
                else
                {
                    newMode = Enum.Parse<UIDebugMode>(mode);
                }

                // Apply new mode
                CurrentMode = newMode;

                // Apply theme if different
                if (theme != CurrentTheme)
                {
                    CurrentTheme = theme;
                    ApplyTheme();
                }

#if DEBUG
                // Update debug config
                DebugConfig.EnableUITracking = enableLogging;
                DebugConfig.WriteUI($"UI Debug mode changed to {newMode} with theme {theme}");
#endif

                // Update visualization based on new mode
                UpdateDebugVisualization();

                Logger.Information("[UIDebug] Applied debug mode: {Mode} with theme: {Theme}", newMode, theme);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[UIDebug] Failed to process debug command");
                throw;
            }
        }

        private static void SendDebugResponse(bool success, string? error = null)
        {
            try
            {
                var response = new Dictionary<string, object>
                {
                    { "Success", success },
                    { "ActiveMode", CurrentMode.ToString() },
                    { "ActiveTheme", CurrentTheme },
                    { "DebugOverlayVisible", IsDebugOverlayVisible },
                    { "Timestamp", DateTime.Now },
                    { "ProcessId", Environment.ProcessId }
                };

                if (!success && error != null)
                {
                    response.Add("Error", error);
                }

                string jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ResponseFilePath, jsonResponse);

                Logger.Debug("[UIDebug] Sent debug response: {Success}", success);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[UIDebug] Failed to send debug response");
            }
        }

        private static void ApplyTheme()
        {
            try
            {
                if (Application.Current.MainWindow == null)
                {
                    return;
                }

                using (var theme = new Theme(CurrentTheme))
                {
                    SfSkinManager.SetTheme(Application.Current.MainWindow, theme);
                }

                Logger.Debug("[UIDebug] Applied theme: {Theme}", CurrentTheme);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[UIDebug] Failed to apply theme: {Theme}", CurrentTheme);
            }
        }

        private static void UpdateDebugVisualization()
        {
            if (Application.Current.MainWindow == null)
            {
                return;
            }

            // Remove existing overlays first
            RemoveDebugVisualizations();

            switch (CurrentMode)
            {
                case UIDebugMode.Normal:
                    // No debug visualization
                    IsDebugOverlayVisible = false;
                    break;

                case UIDebugMode.Layout:
                    AddLayoutVisualization();
                    IsDebugOverlayVisible = true;
                    break;

                case UIDebugMode.Performance:
                    AddPerformanceVisualization();
                    IsDebugOverlayVisible = true;
                    break;

                case UIDebugMode.Verbose:
                    AddVerboseVisualization();
                    IsDebugOverlayVisible = true;
                    break;
            }
        }

        private static void RemoveDebugVisualizations()
        {
            if (Application.Current.MainWindow == null)
            {
                return;
            }

            foreach (var overlay in DebugOverlays.Values)
            {
                if (VisualTreeHelper.GetParent(overlay) is Panel panel)
                {
                    panel.Children.Remove(overlay);
                }
            }

            DebugOverlays.Clear();
        }

        private static void AddLayoutVisualization()
        {
            if (Application.Current.MainWindow == null)
            {
                return;
            }

            // Find main content area
            if (Application.Current.MainWindow.Content is Panel mainPanel)
            {
                var layoutOverlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
                    BorderBrush = new SolidColorBrush(Colors.LimeGreen),
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false
                };

                // Add to visual tree
                mainPanel.Children.Add(layoutOverlay);
                DebugOverlays["Layout"] = layoutOverlay;

                // Also highlight grids with different colors
                HighlightAllGrids(mainPanel);
            }
        }

        private static void HighlightAllGrids(Panel panel)
        {
            foreach (UIElement element in panel.Children)
            {
                if (element is Grid grid)
                {
                    // Visualize grid lines
                    grid.ShowGridLines = true;

                    // Add grid borders
                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
                        BorderThickness = new Thickness(1),
                        IsHitTestVisible = false
                    };

                    Grid.SetRowSpan(border, grid.RowDefinitions.Count > 0 ? grid.RowDefinitions.Count : 1);
                    Grid.SetColumnSpan(border, grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1);

                    grid.Children.Add(border);

                    string overlayKey = $"Grid_{grid.GetHashCode()}";
                    DebugOverlays[overlayKey] = border;

                    // Recursively process nested panels
                    HighlightAllGrids(grid);
                }
                else if (element is Panel nestedPanel)
                {
                    HighlightAllGrids(nestedPanel);
                }
            }
        }

        private static void AddPerformanceVisualization()
        {
            if (Application.Current.MainWindow == null)
            {
                return;
            }

            // Create performance overlay
            var performanceText = new TextBlock
            {
                Text = "PERFORMANCE MONITORING ACTIVE",
                Foreground = new SolidColorBrush(Colors.Yellow),
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false
            };

            // Find main content area
            if (Application.Current.MainWindow.Content is Panel mainPanel)
            {
                mainPanel.Children.Add(performanceText);
                DebugOverlays["Performance"] = performanceText;
            }
        }

        private static void AddVerboseVisualization()
        {
            // Add both layout and performance visualizations
            AddLayoutVisualization();
            AddPerformanceVisualization();

            // Add additional verbose information overlay
            if (Application.Current.MainWindow?.Content is Panel mainPanel)
            {
                var verboseInfo = new TextBlock
                {
                    Text = $"DEBUG MODE: VERBOSE\nTheme: {CurrentTheme}\nProcess ID: {Environment.ProcessId}",
                    Foreground = new SolidColorBrush(Colors.White),
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(10),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontFamily = new FontFamily("Consolas"),
                    IsHitTestVisible = false
                };

                mainPanel.Children.Add(verboseInfo);
                DebugOverlays["VerboseInfo"] = verboseInfo;
            }
        }

        /// <summary>
        /// Manually toggle UI debug mode (for use from application code)
        /// </summary>
        public static void ToggleDebugMode()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessDebugCommand("Toggle", CurrentTheme, true);
            });
        }
    }
}
    