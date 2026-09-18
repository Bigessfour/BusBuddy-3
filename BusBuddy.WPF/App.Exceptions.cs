using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace BusBuddy.WPF
{
    public partial class App
    {
        // Global error handler for UI thread exceptions
        private DateTime _lastUiErrorPopupUtc;
        private string? _lastUiErrorPopupMessage;
        private int _layoutTransientCount;

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Log.Logger;
            e.Handled = true;

            // Layout retries the same Syncfusion/WPF visual-tree failure dozens of times per click
            // (VM 2026-09-17 District Map: 1139 TransformToVisual lines in ~40s). Log once, never
            // AppendAllText the flood into runtime-errors.log.
            if (IsLayoutTransientException(e.Exception))
            {
                _layoutTransientCount++;
                if (_layoutTransientCount == 1)
                {
                    logger.Information(
                        e.Exception,
                        "Layout transient swallowed Count={Count} Message={Message}",
                        _layoutTransientCount,
                        e.Exception.Message);
                }
                else if (_layoutTransientCount % 100 == 0)
                {
                    logger.Information("Layout transients swallowed Count={Count}", _layoutTransientCount);
                }

                return;
            }

            // Capture comprehensive UI context
            string uiContext = Current?.MainWindow?.Content?.GetType().Name ?? "Unknown";
            string currentView = Current?.MainWindow?.Title ?? "MainWindow";

            // Enhanced error logging with UI state
            logger.Error(e.Exception, "UI Runtime Error: {Message} | Context: {UIContext} | View: {CurrentView} | Thread: {ThreadId}",
                e.Exception.Message, uiContext, currentView, Environment.CurrentManagedThreadId);

            // Append to runtime errors log with timestamp and context
            var errorEntry = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] UI Error in {uiContext} ({currentView}): {e.Exception.Message}\n" +
                           $"Stack Trace: {e.Exception.StackTrace}\n" +
                           $"Inner Exception: {e.Exception.InnerException?.Message ?? "None"}\n" +
                           $"---\n";

            // Ensure logs directory exists and write to logs/runtime-errors.log
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            var runtimeErrorsPath = Path.Combine(logsDir, "runtime-errors.log");
            System.IO.File.AppendAllText(runtimeErrorsPath, errorEntry);

            if (IsRepeatedUiError(e.Exception.Message))
            {
                return;
            }

            _lastUiErrorPopupMessage = e.Exception.Message;
            _lastUiErrorPopupUtc = DateTime.UtcNow;

            var result = System.Windows.MessageBox.Show(
                $"An error occurred in {uiContext}.\n\nError: {e.Exception.Message}\n\nDetails have been logged. Continue?",
                "BusBuddy Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                logger.Information("User chose to exit after error");
                Current.Shutdown();
            }
        }

        private bool IsRepeatedUiError(string message)
        {
            return string.Equals(message, _lastUiErrorPopupMessage, StringComparison.Ordinal)
                   && DateTime.UtcNow - _lastUiErrorPopupUtc < TimeSpan.FromSeconds(4);
        }

        /// <summary>
        /// WPF/Syncfusion throws these while SfMap marker templates or mouse-hit test run before
        /// the imagery layer is parented. First hit is Information only; runtime-errors.log is not
        /// flooded (VM 2026-09-17).
        /// </summary>
        internal static bool IsLayoutTransientException(Exception ex)
        {
            if (ex is NullReferenceException &&
                ex.StackTrace?.Contains("Syncfusion.UI.Xaml.Maps", StringComparison.Ordinal) == true)
            {
                return true;
            }

            var text = ex.Message ?? string.Empty;
            if (ex.InnerException is not null)
            {
                text += " " + ex.InnerException.Message;
            }

            return text.Contains("do not share a common ancestor", StringComparison.OrdinalIgnoreCase);
        }

        // Global error handler for non-UI thread exceptions
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved task exception — {Message}", e.Exception.Message);
            e.SetObserved();
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var logger = Log.Logger;
            var exception = e.ExceptionObject as System.Exception;

            // Enhanced non-UI error logging
            logger.Error(exception, "Non-UI Runtime Error: {Message} | IsTerminating: {IsTerminating} | Thread: {ThreadId}",
                exception?.Message ?? "Unknown error", e.IsTerminating, Environment.CurrentManagedThreadId);

            // Append to runtime errors log
            var errorEntry = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] Non-UI Error (Terminating: {e.IsTerminating}): {exception?.Message ?? "Unknown"}\n" +
                           $"Stack Trace: {exception?.StackTrace ?? "None"}\n" +
                           $"---\n";

            // Ensure logs directory exists and write to logs/runtime-errors.log
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            var runtimeErrorsPath = Path.Combine(logsDir, "runtime-errors.log");
            System.IO.File.AppendAllText(runtimeErrorsPath, errorEntry);

            // If terminating, attempt graceful shutdown
            if (e.IsTerminating)
            {
                logger.Fatal("Application terminating due to unhandled exception");
                try
                {
                    // Attempt to save any critical data before shutdown
                    Current?.Dispatcher?.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("A critical error occurred. The application will close.",
                            "BusBuddy Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                catch
                {
                    // If we can't show UI, just log and exit
                    logger.Error("Could not display termination message to user");
                }
            }
        }
    }
}
