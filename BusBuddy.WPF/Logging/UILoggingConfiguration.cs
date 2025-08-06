using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.Logging
{
    public static class UILoggingConfiguration
    {
        public static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", "BusBuddy.WPF")
                .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File(
                    path: "logs/ui-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{Application}] [{MachineName}] [{ProcessId}] [{ThreadId}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture,
                    retainedFileCountLimit: 30)
                .CreateLogger();

            // Add startup context
            using (LogContext.PushProperty("Operation", "ApplicationStartup"))
            {
                Log.Information("UI Logging configuration completed with enrichments");
            }
        }

        /// <summary>
        /// Extension method to configure UI logging for BusBuddy application
        /// </summary>
        public static LoggerConfiguration ConfigureUILogging(this LoggerConfiguration loggerConfiguration, string logsDirectory)
        {
            return loggerConfiguration
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", "BusBuddy.WPF")
                .Enrich.WithProperty("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File(
                    path: System.IO.Path.Combine(logsDirectory, "ui-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{Application}] [{MachineName}] [{ProcessId}] [{ThreadId}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture,
                    retainedFileCountLimit: 30);
        }

        public static IDisposable BeginOperation(string operationName)
        {
            return LogContext.PushProperty("Operation", operationName);
        }

        public static IDisposable BeginCorrelation(string correlationId)
        {
            return LogContext.PushProperty("CorrelationId", correlationId);
        }
    }
}
