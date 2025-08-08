using System.Windows;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using BusBuddy.WPF.Views.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Extensions;
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Serilog.Formatting.Json;
using Serilog.Settings.Configuration;
using System.Threading.Tasks;

namespace BusBuddy.WPF
{
    /// <summary>
    /// BusBuddy WPF Application startup with dual-mode operation
    /// - EF Migration Mode: Minimal services for database operations only
    /// - UI Mode: Full dependency injection with robust error handling
    /// Features: Pure Serilog logging, Syncfusion license management, comprehensive error capture
    /// Updated: Enhanced startup logic for MVP with full UI support
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }
        private static ILogger? _bootstrapLogger;
        private static bool _syncfusionLicenseChecked; // guard to ensure single execution

        public App()
        {
            // Initialize bootstrap logger first for early startup error capture
            InitializeBootstrapLogger();

            _bootstrapLogger?.Information("🚌 BusBuddy bootstrap starting...");

            // Register Syncfusion license before any UI initialization
            EnsureSyncfusionLicenseRegistered();

            // Load configuration from appsettings.json
            IConfiguration configuration;
            try
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Use app directory for config file
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile("appsettings.azure.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                _bootstrapLogger?.Information("✅ Configuration loaded successfully");
            }
            catch (Exception configEx)
            {
                _bootstrapLogger?.Error(configEx, "❌ Configuration loading failed: {ErrorMessage}", configEx.Message);
                Console.WriteLine($"Configuration Error: {configEx.Message}");
                throw new InvalidOperationException("Configuration loading failed. Application cannot start.", configEx);
            }

            // Initialize Serilog logger using configuration from appsettings.json
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                Log.Information("🚌 Serilog initialized using configuration from appsettings.json");
            }
            catch (Exception ex)
            {
                // Fallback to basic configuration if loading from appsettings.json fails
                Console.WriteLine($"Warning: Failed to initialize Serilog from config: {ex.Message}");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File("Logs/busbuddy-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }

            Log.Information("🚌 BusBuddy MVP starting...");
        }

        /// <summary>
        /// Initialize a basic bootstrap logger for early startup error capture
        /// </summary>
        private static void InitializeBootstrapLogger()
        {
            try
            {
                // Create a simple bootstrap logger for early startup errors
                _bootstrapLogger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/bootstrap-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                _bootstrapLogger.Information("🔧 Bootstrap logger initialized for early startup error capture");
            }
            catch (Exception ex)
            {
                // If bootstrap logger fails, fall back to console
                Console.WriteLine($"Warning: Failed to initialize bootstrap logger: {ex.Message}");
                Console.WriteLine("Continuing with console-only logging for bootstrap phase");
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Verify STA thread state (except for EF migrations)
            var threadState = Thread.CurrentThread.GetApartmentState();
            Log.Information("🚌 BusBuddy starting on thread {ThreadId} with apartment state: {ApartmentState}",
                Environment.CurrentManagedThreadId, threadState);

            // Check if this is an EF migration or design-time operation
            var commandLineArgs = Environment.GetCommandLineArgs();
            var isEfMigration = commandLineArgs.Any(arg => arg.Contains("ef") || arg.Contains("migration") || arg.Contains("dotnet-ef"));
            var isDesignTime = System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject());

            Log.Information("🚌 Command line args: {Args}", string.Join(" ", commandLineArgs));
            Log.Information("🚌 EF Migration mode: {IsEfMigration}", isEfMigration);
            Log.Information("🚌 Design-time mode: {IsDesignTime}", isDesignTime);

            if (isEfMigration)
            {
                Log.Information("🚌 Running in EF migration mode - configuring minimal services only");
                // For EF migrations, configure only essential services and exit without UI
                ConfigureServicesForMigration();
                Log.Information("🚌 EF migration configuration completed");
                return;
            }

            // Enforce STA thread state for normal WPF operation
            if (threadState != ApartmentState.STA)
            {
                Log.Error("❌ Thread is not STA! Current state: {ApartmentState} - WPF requires STA", threadState);
                MessageBox.Show("Application startup error: Thread must be STA for WPF", "Threading Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
                return;
            }

            // Add global error handlers for runtime error capture
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            try
            {
                Log.Information("🚌 Initializing BusBuddy MVP application");

                // Setup minimal DI for Students, Routes, Buses, Drivers (synchronous)
                ConfigureServices();

                // Seed Wiley School District data on startup (async, resilient)
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var studentService = scope.ServiceProvider.GetRequiredService<IStudentService>();
                    var seedResult = await BusBuddy.Core.Utilities.ResilientDbExecution.ExecuteWithResilienceAsync(
                        () => studentService.SeedWileySchoolDistrictDataAsync(),
                        "SeedWileySchoolDistrictData",
                        maxRetries: 3
                    );
                    Log.Information($"🚌 Wiley seeding result: Success={seedResult.Success}, RecordsSeeded={seedResult.RecordsSeeded}, Error={seedResult.ErrorMessage}");
                    File.AppendAllText("runtime-errors-fixed.log", $"Wiley seeding: Success={seedResult.Success}, RecordsSeeded={seedResult.RecordsSeeded}, Error={seedResult.ErrorMessage}\n");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "🚌 Error during Wiley School District seeding");
                    File.AppendAllText("runtime-errors-fixed.log", $"Wiley seeding error: {ex.Message}\n");
                }

                // Handle command line arguments for PowerShell integration
                if (e.Args.Length > 0 && HandleCommandLineArgs(e.Args))
                {
                    // Command line operation completed, exit gracefully
                    Environment.Exit(0);
                    return;
                }

                // Create and show the main window for normal GUI operation
                var mainWindow = CreateMainWindow();
                mainWindow.Show();

                Log.Information("🚌 BusBuddy MVP application started successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "🚌 Failed to start BusBuddy MVP application");
                MessageBox.Show($"Failed to start application: {ex.Message}", "BusBuddy Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void ConfigureServicesForMigration()
        {
            try
            {
                Log.Information("🔧 Setting up minimal services for EF migration...");

                var services = new ServiceCollection();

                // Add configuration to resolve appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.azure.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Only register the bare minimum for EF migrations - just the DbContext
                services.AddDataServices(configuration);

                ServiceProvider = services.BuildServiceProvider();
                Log.Information("✅ Minimal services configured for EF migration");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to configure services for EF migration");
                throw; // Re-throw for migration operations
            }

        }

        private void ConfigureServices()
        {
            try
            {
                Log.Information("🔧 Setting up full DI container for UI application...");

                var services = new ServiceCollection();

                // Add configuration to resolve appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                // Register configuration for DI
                services.AddSingleton<IConfiguration>(configuration);

                // Use the proper extension method that registers IBusBuddyDbContextFactory
                services.AddDataServices(configuration);

                // Register core business services for Students, Routes, Buses, Drivers
                services.AddScoped<IStudentService, StudentService>();
                services.AddScoped<IDriverService, DriverService>();
                services.AddScoped<IRouteService, RouteService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.IBusService, BusService>();

                // Register UI services (commented out for MVP - services don't exist yet)
                // services.AddTransient<BusBuddy.WPF.Services.DialogService>();
                // services.AddTransient<BusBuddy.WPF.Services.NavigationService>();

                // Register ViewModels for dependency injection
                services.AddTransient<BusBuddy.WPF.ViewModels.MainWindowViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.DashboardViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>();

                ServiceProvider = services.BuildServiceProvider();

                // Seed database with JSON data if empty
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = ServiceProvider.CreateScope();
                        var contextFactory = scope.ServiceProvider.GetRequiredService<IBusBuddyDbContextFactory>();
                        using var context = contextFactory.CreateDbContext();

                        // Ensure database is created and up to date with retry strategy
                        await BusBuddy.Core.Utilities.ResilientDbExecution.ExecuteWithResilienceAsync(
                            async () => { await context.Database.EnsureCreatedAsync(); return true; },
                            "Database EnsureCreated",
                            maxRetries: 3
                        );

                        // Import JSON data if database is empty with retry strategy
                        await BusBuddy.Core.Utilities.JsonDataImporter.SeedDatabaseIfEmptyAsync(context);
                    }
                    catch (Exception seedEx)
                    {
                        Log.Warning(seedEx, "⚠️ Failed to seed database with JSON data: {Error}", seedEx.Message);
                    }
                });                Log.Information("✅ Full DI container configured successfully for UI application");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Full DI setup failed, will use fallback approach for UI");
                // Create a minimal service provider for basic functionality
                try
                {
                    var fallbackServices = new ServiceCollection();
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.azure.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

                    fallbackServices.AddSingleton<IConfiguration>(configuration);
                    ServiceProvider = fallbackServices.BuildServiceProvider();
                    Log.Information("✅ Fallback service provider created");
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "❌ Even fallback service configuration failed");
                    ServiceProvider = null;
                }
            } // end outer catch for ConfigureServices
        } // end ConfigureServices method

        private MainWindow CreateMainWindow()
        {
            try
            {
                Log.Information("🏗️ Creating MainWindow for full UI application");

                // Try to create MainWindow with full DI first
                if (ServiceProvider != null)
                {
                    Log.Information("🎯 Creating MainWindow with dependency injection");

                    try
                    {
                        var viewModel = ServiceProvider.GetService<BusBuddy.WPF.ViewModels.MainWindowViewModel>();
                        if (viewModel != null)
                        {
                            Log.Information("✅ MainWindowViewModel created successfully via DI");
                            var mainWindow = new MainWindow();
                            mainWindow.DataContext = viewModel;
                            return mainWindow;
                        }
                        else
                        {
                            Log.Warning("⚠️ MainWindowViewModel not available from DI, creating without ViewModel");
                        }
                    }
                    catch (Exception diEx)
                    {
                        Log.Warning(diEx, "⚠️ Failed to create MainWindow with DI, falling back");
                    }
                }

                Log.Information("📦 Creating MainWindow with basic initialization");
                var fallbackWindow = new MainWindow();

                // Initialize with basic functionality if DI failed
                if (ServiceProvider == null)
                {
                    Log.Information("💡 Setting up MainWindow for standalone operation");
                    // Can add basic sample data or simplified ViewModels here if needed
                }

                return fallbackWindow;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to create MainWindow, creating emergency fallback");

                // Emergency fallback - create the most basic window possible
                try
                {
                    var emergencyWindow = new MainWindow();
                    return emergencyWindow;
                }
                catch (Exception criticalEx)
                {
                    Log.Fatal(criticalEx, "💀 Critical failure creating MainWindow");
                    throw; // This is truly critical, let the app fail
                }
            }
        }

        // Global error handler for UI thread exceptions
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Log.Logger;

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

            // User-friendly popup with option to continue
            var result = System.Windows.MessageBox.Show(
                $"An error occurred in {uiContext}.\n\nError: {e.Exception.Message}\n\nDetails have been logged. Continue?",
                "BusBuddy Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // Allow graceful shutdown if user chooses
            if (result == MessageBoxResult.No)
            {
                logger.Information("User chose to exit after error");
                Current.Shutdown();
            }

            e.Handled = true; // Prevent app crash
        }

        // Global error handler for non-UI thread exceptions
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
                    Current?.Dispatcher?.Invoke(() => {
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

        /// <summary>
        /// Handle command line arguments for PowerShell integration
        /// </summary>
        private bool HandleCommandLineArgs(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLower())
                    {
                        case "--optimize-route":
                            return HandleRouteOptimization(args, i);

                        case "--generate-report":
                            return HandleReportGeneration(args, i);

                        case "--help":
                        case "-h":
                            ShowCommandLineHelp();
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling command line arguments");
                Console.WriteLine($"Error: {ex.Message}");
                return true; // Exit to prevent GUI startup on error
            }
        }

        /// <summary>
        /// Handle route optimization command line operation
        /// </summary>
        private bool HandleRouteOptimization(string[] args, int startIndex)
        {
            try
            {
                // Parse route optimization arguments
                string routeId = null;
                string currentPerformance = "Standard performance metrics";
                string targetMetrics = "Improve efficiency and reduce travel time";
                var constraints = new List<string>();
                string outputPath = null;

                for (int i = startIndex + 1; i < args.Length; i += 2)
                {
                    if (i >= args.Length - 1)
                    {
                        break;
                    }

                    switch (args[i].ToLower())
                    {
                        case "--route-id":
                            routeId = args[i + 1];
                            break;
                        case "--current-performance":
                            currentPerformance = args[i + 1];
                            break;
                        case "--target-metrics":
                            targetMetrics = args[i + 1];
                            break;
                        case "--constraints":
                            constraints.AddRange(args[i + 1].Split(';'));
                            break;
                        case "--output":
                            outputPath = args[i + 1];
                            break;
                    }
                }

                if (string.IsNullOrEmpty(routeId))
                {
                    Console.WriteLine("Error: --route-id is required for route optimization");
                    return true;
                }

                Log.Information("Starting command line route optimization for route {RouteId}", routeId);

                // TODO: Implement actual GrokGlobalAPI call
                // For now, return mock data that matches the PowerShell expected format
                var result = new
                {
                    RouteId = routeId,
                    OptimizationSuggestions = $@"Route Optimization Analysis for {routeId}:

EFFICIENCY IMPROVEMENTS:
• Consolidate stops within 0.3 miles to reduce travel time by 12%
• Optimize pickup sequence by grade level for 8% efficiency gain
• Implement GPS tracking for real-time adjustments

TIME OPTIMIZATION:
• Reduce route time by 15% through strategic stop consolidation
• Adjust departure times based on traffic patterns
• Implement express routes for high-density areas

FUEL EFFICIENCY:
• Route adjustments could save 18% in fuel consumption
• Reduce unnecessary turns and backtracking
• Optimize idle time at stops

IMPLEMENTATION STEPS:
1. Review current route data and student locations
2. Identify consolidation opportunities within walking distance
3. Test optimized route during off-peak hours
4. Gradually implement changes with driver feedback
5. Monitor performance metrics for 2 weeks",
                    EfficiencyGain = 12.5,
                    TimeReduction = 15.0,
                    FuelSavings = 18.0,
                    SafetyImprovements = new[] { "Reduced left turns", "Improved stop visibility", "Better traffic coordination" },
                    ImplementationSteps = new[] { "Review current route data", "Identify consolidation opportunities", "Test optimized route", "Implement changes gradually", "Monitor performance metrics" },
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    AIModel = "Grok-4-CLI-Integration"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, json);
                    Log.Information("Route optimization saved to {OutputPath}", outputPath);
                }

                Console.WriteLine(json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in route optimization");
                Console.WriteLine($"Route optimization error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Handle report generation command line operation
        /// </summary>
        private bool HandleReportGeneration(string[] args, int startIndex)
        {
            try
            {
                // Parse report generation arguments
                string reportType = null;
                string outputPath = null;
                string routeId = null;
                string format = "PDF";

                for (int i = startIndex + 1; i < args.Length; i += 2)
                {
                    if (i >= args.Length - 1)
                    {
                        break;
                    }

                    switch (args[i].ToLower())
                    {
                        case "--report-type":
                            reportType = args[i + 1];
                            break;
                        case "--output":
                            outputPath = args[i + 1];
                            break;
                        case "--route-id":
                            routeId = args[i + 1];
                            break;
                        case "--format":
                            format = args[i + 1];
                            break;
                    }
                }

                if (string.IsNullOrEmpty(reportType) || string.IsNullOrEmpty(outputPath))
                {
                    Console.WriteLine("Error: --report-type and --output are required for report generation");
                    return true;
                }

                Log.Information("Starting command line report generation: {ReportType} -> {OutputPath}", reportType, outputPath);

                // TODO: Implement actual PdfReportService call
                // For now, create a mock PDF/report file
                var reportContent = $@"BusBuddy {reportType} Report
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Format: {format}
{(routeId != null ? $"Route ID: {routeId}" : "")}

This is a mock {reportType.ToLower()} report generated via command line.
In the full implementation, this would use Syncfusion PDF tools to generate a proper {format} report.

Sample data would include:
- Student information
- Route details
- Driver schedules
- Safety protocols
- Performance metrics";

                // Create output directory if needed
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, reportContent);
                Log.Information("Report generated successfully: {OutputPath}", outputPath);

                var result = new
                {
                    ReportType = reportType,
                    OutputPath = outputPath,
                    Format = format,
                    RouteId = routeId,
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    FileSize = new FileInfo(outputPath).Length
                };

                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in report generation");
                Console.WriteLine($"Report generation error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Show command line help
        /// </summary>
        private void ShowCommandLineHelp()
        {
            Console.WriteLine(@"BusBuddy Command Line Interface

Usage:
  BusBuddy.exe [options]

Route Optimization:
  --optimize-route --route-id <id> [options]
    --route-id <id>              Route identifier (required)
    --current-performance <text> Current performance description
    --target-metrics <text>      Target optimization goals
    --constraints <list>         Semicolon-separated constraints
    --output <path>              Output file path for results

Report Generation:
  --generate-report --report-type <type> --output <path> [options]
    --report-type <type>         Report type: Roster, RouteManifest, StudentList, DriverSchedule
    --output <path>              Output file path (required)
    --route-id <id>              Route ID for route-specific reports
    --format <format>            Output format: PDF, Excel, CSV (default: PDF)

General:
  --help, -h                     Show this help message

Examples:
  BusBuddy.exe --optimize-route --route-id ""Route-001"" --target-metrics ""Reduce time by 10%""
  BusBuddy.exe --generate-report --report-type Roster --output ""reports/roster.pdf""
");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("🚌 BusBuddy MVP application shutting down");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        /// <summary>
        /// Ensures Syncfusion license registration (runs only once). Based on Syncfusion WPF licensing documentation.
        /// </summary>
        private static void EnsureSyncfusionLicenseRegistered()
        {
            if (_syncfusionLicenseChecked)
            {
                return; // already attempted
            }
            _syncfusionLicenseChecked = true;
            try
            {
                // Check Process level first, then User level, then Machine level
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ??
                               Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                               Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    _bootstrapLogger?.Warning("⚠️ SYNCFUSION_LICENSE_KEY environment variable not set at Process, User, or Machine level. Running in trial mode.");
                    LogSyncfusionDiagnostics();
                    return; // trial mode – do not attempt registration
                }

                if (ValidateSyncfusionLicenseKey(licenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    _bootstrapLogger?.Information("✅ Syncfusion license registered successfully for version 30.1.42");

                    // Log additional diagnostics to help verify registration
                    _bootstrapLogger?.Information("🔍 License Key Length: {Length} characters", licenseKey.Length);
                    _bootstrapLogger?.Information("💡 If you see trial watermarks, verify your license key is valid and current");
                }
                else
                {
                    _bootstrapLogger?.Warning("⚠️ Provided Syncfusion license key failed validation. Running in trial mode.");
                    _bootstrapLogger?.Information("💡 License key should be a long alphanumeric string from your Syncfusion account");
                    LogSyncfusionDiagnostics();
                }
            }
            catch (Exception ex)
            {
                _bootstrapLogger?.Error(ex, "❌ Syncfusion license registration attempt failed: {ErrorMessage}", ex.Message);
                LogSyncfusionDiagnostics();
                // Allow fallback to trial mode without throwing to keep app usable
            }
        }

        /// <summary>
        /// Validates Syncfusion license key format and provides diagnostic information
        /// Based on Syncfusion documentation for version 30.1.42
        /// </summary>
        private static bool ValidateSyncfusionLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            // Check for common invalid placeholder values
            var invalidPlaceholders = new[] { "YOUR_LICENSE_KEY", "YOUR LICENSE KEY", "PLACEHOLDER", "TRIAL", "DEMO" };
            if (invalidPlaceholders.Any(placeholder =>
                licenseKey.Equals(placeholder, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Basic format validation - Syncfusion keys are typically long base64-like strings
            if (licenseKey.Length < 20)
            {
                return false;
            }

            // Additional validation - license keys shouldn't contain common file paths or environment indicators
            var suspiciousPatterns = new[] { "\\", "/", "C:", "D:", "temp", "test", "dev" };
            if (suspiciousPatterns.Any(pattern => licenseKey.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Provides detailed diagnostic information for Syncfusion licensing issues
        /// </summary>
        private static void LogSyncfusionDiagnostics()
        {
            var logger = _bootstrapLogger ?? Log.Logger;

            logger.Information("🔍 Syncfusion Diagnostics:");
            logger.Information("   Version: 30.1.42 (as defined in Directory.Build.props)");
            logger.Information("   Platform: WPF (.NET 9.0-windows)");
            logger.Information("   License Type: Offline validation (no internet required)");
            logger.Information("   Registration Location: App() constructor (before any control initialization)");

            // Check environment variable
            var envLicenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (string.IsNullOrEmpty(envLicenseKey))
            {
                logger.Information("   Environment Variable SYNCFUSION_LICENSE_KEY: Not Set");
                logger.Information("   💡 To fix: Set SYNCFUSION_LICENSE_KEY environment variable to your license key");
                logger.Information("   💡 Get license key from: https://www.syncfusion.com/account/downloads");
            }
            else
            {
                logger.Information("   Environment Variable SYNCFUSION_LICENSE_KEY: Set (length: {Length})", envLicenseKey.Length);
                logger.Information("   💡 License key format looks {Status}",
                    ValidateSyncfusionLicenseKey(envLicenseKey) ? "valid" : "invalid");
            }

            // Check for common Syncfusion assemblies
            try
            {
                var syncfusionAssembly = typeof(Syncfusion.Licensing.SyncfusionLicenseProvider).Assembly;
                logger.Information("   Syncfusion.Licensing Assembly: {Version}", syncfusionAssembly.GetName().Version);

                // Try to get some version info from a main Syncfusion assembly
                var gridAssembly = System.Reflection.Assembly.LoadFrom(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Syncfusion.SfGrid.WPF.dll"));
                logger.Information("   Syncfusion.SfGrid.WPF Assembly: {Version}", gridAssembly.GetName().Version);
            }
            catch (Exception ex)
            {
                logger.Warning("   Syncfusion Assembly Check: Error loading - {Error}", ex.Message);
                logger.Information("   💡 This may indicate missing Syncfusion packages or incorrect installation");
            }
        }
    }
}
