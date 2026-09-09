using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BusBuddy.WPF.Logging;

/// <summary>
/// Dual-write helper for UI runtime evaluation: app Serilog pipeline plus
/// <c>logs/ui-diagnostics-.log</c> (same folder as map-interactions and appsettings sinks).
/// </summary>
internal static class UiDiagnosticsLog
{
    internal const string FileName = "ui-diagnostics-.log";

    private static readonly object Gate = new();
    private static Logger? _fileLogger;
    private static bool _fileAttempted;

    internal static string ResolveLogDirectory()
    {
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "logs"));
    }

    internal static string ResolveLogPath() => Path.Combine(ResolveLogDirectory(), FileName);

    internal static ILogger ForContext<T>() => Log.ForContext<T>();

    internal static ILogger FileOrNull()
    {
        EnsureFileLogger();
        return _fileLogger ?? Log.Logger;
    }

    internal static void Write(ILogger contextLogger, LogEventLevel level, string messageTemplate, params object[] propertyValues)
    {
        contextLogger.Write(level, messageTemplate, propertyValues);
        EnsureFileLogger();
        _fileLogger?.Write(level, messageTemplate, propertyValues);
    }

    internal static void Write(ILogger contextLogger, LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
    {
        contextLogger.Write(level, exception, messageTemplate, propertyValues);
        EnsureFileLogger();
        _fileLogger?.Write(level, exception, messageTemplate, propertyValues);
    }

    private static void EnsureFileLogger()
    {
        if (_fileAttempted)
        {
            return;
        }

        lock (Gate)
        {
            if (_fileAttempted)
            {
                return;
            }

            _fileAttempted = true;
            try
            {
                var path = ResolveLogPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _fileLogger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(
                        path,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "UI diagnostics file unavailable — using app log only");
            }
        }
    }
}
