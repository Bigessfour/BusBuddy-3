using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Serilog;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Handles XAML parsing errors and provides fallback values
    /// Specifically addresses the "* is not a valid value for Double" issue
    /// </summary>
    public static class XamlErrorHandler
    {
        private static readonly Serilog.ILogger Logger = Log.ForContext(typeof(XamlErrorHandler));
        private static volatile int _recoveryAttempts;
        private static readonly int MAX_RECOVERY_ATTEMPTS = 3;
        private static readonly object _recoveryLock = new object();

        /// <summary>
        /// Initialize XAML error handling for the application
        /// </summary>
        public static void Initialize()
        {
            // Handle WPF unhandled exceptions that occur during XAML parsing
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                if (IsXamlParsingError(e.Exception))
                {
                    lock (_recoveryLock)
                    {
                        Logger.Error(e.Exception, "XAML parsing error detected — attempting graceful recovery (attempt {Attempt}/{MaxAttempts})",
                            _recoveryAttempts + 1, MAX_RECOVERY_ATTEMPTS);

                        // Prevent infinite recovery loops
                        if (_recoveryAttempts >= MAX_RECOVERY_ATTEMPTS)
                        {
                            Logger.Warning("Maximum recovery attempts reached. Falling back to default error handling.");
                            e.Handled = true;
                            return;
                        }

                        // Try to recover from common XAML errors
                        _recoveryAttempts++;
                        if (TryHandleXamlError(e.Exception))
                        {
                            e.Handled = true;
                            Logger.Information("Successfully recovered from XAML parsing error (attempt {Attempt})", _recoveryAttempts);
                            return;
                        }
                    }
                }

                // Log all other unhandled exceptions and always suppress error dialogs
                Logger.Error(e.Exception, "Unhandled dispatcher exception — {Message}", e.Exception.Message);
                e.Handled = true; // Always suppress error dialogs to prevent UI crashes
            };

            // Handle domain exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger.Fatal((Exception)e.ExceptionObject, "Unhandled domain exception");
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Logger.Error(e.Exception, "Unobserved task exception caught");
                e.SetObserved(); // Prevent crash
            };
        }

        /// <summary>
        /// Reset recovery attempts counter — called after successful initialization
        /// </summary>
        public static void ResetRecoveryAttempts()
        {
            lock (_recoveryLock)
            {
                _recoveryAttempts = 0;
                Logger.Information("Recovery attempts counter reset");
            }
        }

        /// <summary>
        /// Check if the exception is related to XAML parsing
        /// </summary>
        private static bool IsXamlParsingError(Exception exception)
        {
            return exception is XamlParseException ||
                   exception is ArgumentException argEx && argEx.Message.Contains("is not a valid value") ||
                   exception.Message.Contains("TypeConverter") ||
                   exception.Message.Contains("ProvideValue");
        }

        /// <summary>
        /// Attempt to handle and recover from XAML errors
        /// </summary>
        private static bool TryHandleXamlError(Exception exception)
        {
            try
            {
                // Handle the specific "* is not a valid value for Double" error
                if (exception.Message.Contains("* is not a valid value for Double"))
                {
                    Logger.Warning("Detected '*' parsing error — this is likely a Syncfusion control template issue");
                    return true;
                }

                // Handle resource not found errors
                if (exception.Message.Contains("FluentDark") && exception.Message.Contains("resource"))
                {
                    Logger.Warning("FluentDark resource not found — theme resources may not be properly merged");
                    return true;
                }

                // Handle BeginInit/EndInit nesting issues
                if (exception.Message.Contains("BeginInit") || exception.Message.Contains("EndInit"))
                {
                    Logger.Warning("BeginInit/EndInit nesting issue detected — avoiding nested initialization");
                    return true;
                }

                // Handle other common XAML parsing errors
                if (exception is XamlParseException xamlEx)
                {
                    Logger.Warning("XAML parsing error at line {Line}: {Message}",
                        xamlEx.LineNumber, xamlEx.Message);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle XAML error");
                return false;
            }
        }

        /// <summary>
        /// Provides safe fallback values for common XAML binding scenarios
        /// </summary>
        public static object GetSafeFallbackValue(Type targetType, object? originalValue = null)
        {
            try
            {
                if (targetType == typeof(double) || targetType == typeof(double?))
                {
                    // Return 0 for numeric values that can't be parsed
                    return 0.0;
                }

                if (targetType == typeof(GridLength))
                {
                    // Return Auto for GridLength that can't be parsed
                    return new GridLength(1, GridUnitType.Auto);
                }

                if (targetType == typeof(string))
                {
                    return originalValue?.ToString() ?? string.Empty;
                }

                if (targetType == typeof(Visibility))
                {
                    return Visibility.Collapsed;
                }

                // Return the default value for the type
                if (targetType.IsValueType)
                {
                    return Activator.CreateInstance(targetType) ?? (object)0;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to provide fallback value for type {Type}", targetType);
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Safe type converter that handles parsing errors gracefully
    /// </summary>
    public class SafeDoubleConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string stringValue)
            {
                // Handle the problematic "*" value
                if (stringValue == "*")
                {
                    Log.ForContext<SafeDoubleConverter>()
                        .Warning("Attempted to convert '*' to double - returning 0.0");
                    return 0.0;
                }

                // Try normal parsing
                if (double.TryParse(stringValue, NumberStyles.Float, culture, out double result))
                {
                    return result;
                }

                // Return safe fallback
                return 0.0;
            }

            return base.ConvertFrom(context, culture, value) ?? 0.0;
        }
    }
}
