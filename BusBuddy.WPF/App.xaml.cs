using System.Windows;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Serilog;
using Serilog.Events;
using BusBuddy.WPF.Views.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
// Phase-based extension removed; direct registrations used instead
using BusBuddy.Core.Extensions; // Needed for AddDataServices extension
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Serilog.Formatting.Json;
using Serilog.Settings.Configuration;
using System.Threading.Tasks;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.Utilities;

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

            // Load API keys from macOS Passwords (Keychain) into process environment variables
            // so that documented entry points (EnsureSyncfusionLicenseRegistered, GrokGlobalAPI ctor, etc.)
            // can find them via the standard Environment.GetEnvironmentVariable paths.
            // This bridges Passwords app -> runtime env on macOS.
            // On non-mac, falls back to existing env / machine vars.
            LoadApiKeysFromMacPasswords();

            // Register Syncfusion license before any UI initialization
            EnsureSyncfusionLicenseRegistered();

            // Load configuration from appsettings.json (consolidated)
            IConfiguration configuration = BuildConfiguration();

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

        /// <summary>
        /// Loads sensitive API keys from macOS Passwords app (iCloud Keychain / local keychain)
        /// into the current process environment variables.
        /// This makes them available to the documented entry points:
        /// - EnsureSyncfusionLicenseRegistered()  (looks for SYNCFUSION_LICENSE_KEY)
        /// - GrokGlobalAPI constructor / XaiService paths (looks for XAI_API_KEY)
        /// - mcp.json consumers for Syncfusion_API_Key (if the MCP client inherits process env)
        ///
        /// Uses the standard `security` CLI (no extra dependencies).
        /// Safe on non-macOS (no-op).
        /// Assumes entries in Passwords app have "Name" matching the env var name (e.g. "XAI_API_KEY").
        /// If not found or on error, existing env vars are used (graceful fallback).
        /// </summary>
        private static void LoadApiKeysFromMacPasswords()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                _bootstrapLogger?.Information("Non-macOS platform - skipping Passwords keychain load (rely on system env vars)");
                return;
            }

            _bootstrapLogger?.Information("🔐 Loading API keys from macOS Passwords (Keychain) into process environment...");

            // Keys loaded from macOS Passwords (Name = env var) into process environment.
            var keysToLoad = new[]
            {
                "XAI_API_KEY",
                "GROK_API_KEY",
                "SYNCFUSION_LICENSE_KEY",
                "SYNCFUSION_API_KEY",
                "Syncfusion_API_Key",
                "GCP_BILLING_PROJECT",
                "GOOGLE_CLOUD_PROJECT",
                "GOOGLE_MAPS_API_KEY"
            };

            foreach (var keyName in keysToLoad)
            {
                try
                {
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(keyName)))
                    {
                        _bootstrapLogger?.Information("  {Key} already present in environment - skipping keychain lookup", keyName);
                        continue;
                    }

                    // Use macOS security CLI to retrieve the generic password (the secret value)
                    // -s = service / name in Passwords app. User should name the entry the same as the env var.
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/security",
                        Arguments = $"find-generic-password -s {keyName} -w",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process == null)
                    {
                        _bootstrapLogger?.Warning("  Could not start security process for {Key}", keyName);
                        continue;
                    }

                    string secret = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(secret))
                    {
                        Environment.SetEnvironmentVariable(keyName, secret);
                        _bootstrapLogger?.Information("  ✅ Loaded {Key} from macOS Passwords into process env", keyName);
                    }
                    else
                    {
                        // Not found or error - this is normal if user hasn't added the entry yet
                        _bootstrapLogger?.Information("  ℹ️ {Key} not found in Passwords (or access denied). Will use other sources / placeholders.", keyName);
                    }
                }
                catch (Exception ex)
                {
                    _bootstrapLogger?.Warning(ex, "  ⚠️ Failed to load {Key} from macOS Passwords: {Message}", keyName, ex.Message);
                }
            }

            // Aliases: Passwords may store SYNCFUSION_API_KEY while MCP expects Syncfusion_API_Key
            PromoteEnvIfEmpty("SYNCFUSION_API_KEY", "Syncfusion_API_Key");
            PromoteEnvIfEmpty("Syncfusion_API_Key", "SYNCFUSION_API_KEY");

            // Alternate keychain service names used by Syncfusion tooling
            TryLoadKeychainSecret("com.wileyco.syncfusion.license", "SYNCFUSION_LICENSE_KEY");
            TryLoadKeychainSecret("Syncfusion License Key", "SYNCFUSION_LICENSE_KEY");

            _bootstrapLogger?.Information("🔐 macOS Passwords keychain load complete. Keys are now available to registration methods.");
        }

        private static void PromoteEnvIfEmpty(string sourceVar, string targetVar)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(targetVar)))
            {
                return;
            }

            var value = Environment.GetEnvironmentVariable(sourceVar);
            if (!string.IsNullOrEmpty(value))
            {
                Environment.SetEnvironmentVariable(targetVar, value);
                _bootstrapLogger?.Information("  ✅ Promoted {Source} → {Target} in process env", sourceVar, targetVar);
            }
        }

        private static bool TryLoadKeychainSecret(string serviceName, string envVarName)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVarName)))
            {
                return true;
            }

            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return false;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/security",
                    Arguments = $"find-generic-password -s \"{serviceName}\" -w",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                {
                    return false;
                }

                var secret = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(secret))
                {
                    Environment.SetEnvironmentVariable(envVarName, secret);
                    _bootstrapLogger?.Information("  ✅ Loaded {EnvVar} from keychain service '{Service}'", envVarName, serviceName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _bootstrapLogger?.Warning(ex, "  ⚠️ Keychain lookup failed for service {Service}", serviceName);
            }

            return false;
        }

        /// <summary>
        /// Centralized configuration builder used across App for consistent loading.
        /// Reads appsettings.json, environment-specific JSON, optional azure settings,
        /// and environment variables. Uses AppDomain base directory as base path.
        /// </summary>
        private static IConfiguration BuildConfiguration()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                _bootstrapLogger?.Information("✅ Configuration loaded successfully (BuildConfiguration)");
                return configuration;
            }
            catch (Exception configEx)
            {
                _bootstrapLogger?.Error(configEx, "❌ Configuration loading failed (BuildConfiguration): {ErrorMessage}", configEx.Message);
                throw new InvalidOperationException("Configuration loading failed. Application cannot start.", configEx);
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

                // Removed redundant explicit Wiley seeding. Seeding now handled via EF Core 9 UseSeeding/UseAsyncSeeding

                // Handle command line arguments for PowerShell integration
                if (e.Args.Length > 0 && TryHandleCommandLineArgs(e.Args) is int exitCode)
                {
                    Environment.Exit(exitCode);
                    return;
                }

                // Initialize SyncFusion themes according to v30.1.42 API
                InitializeSyncfusionThemes();

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
                var configuration = BuildConfiguration();

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
                var env2 = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{env2}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Register configuration for DI
                services.AddSingleton<IConfiguration>(configuration);

                // Use the proper extension method that registers IBusBuddyDbContextFactory
                services.AddDataServices(configuration);

                // Route geography from the database. Maps Platform clients are paused (spec 007).
                services.AddSingleton<IGeoDataService>(sp =>
                    new GeoDataService(sp.GetService<IBusBuddyDbContextFactory>()));
                services.AddSingleton<IGeocodingService, OfflineGeocodingService>();
                services.AddSingleton<IEligibilityService>(_ =>
                {
                    var district = Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "WileyDistrict", "WileyDistrict.shp");
                    var town = Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "WileyTown", "WileyTown.shp");
                    return new ShapefileEligibilityService(district, town);
                });

                // Register core business services for Students, Routes, Buses, Drivers
                services.AddScoped<IStudentService, StudentService>();
                services.AddScoped<IDriverService, DriverService>();
                services.AddScoped<IRouteService, RouteService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.IBusService, BusService>();

                // Register UI services (commented out for MVP - services don't exist yet)
                // services.AddTransient<BusBuddy.WPF.Services.DialogService>();
                // services.AddTransient<BusBuddy.WPF.Services.NavigationService>();
                services.AddTransient<BusBuddy.WPF.Services.RouteExportService>();
                services.AddSingleton<BusBuddy.WPF.Services.ISkinManagerService, BusBuddy.WPF.Services.SkinManagerService>();

                // Local AI chat (Ollama by default; graceful fallback when unavailable).
                // Separate HttpClient instances — GrokGlobalAPI mutates DefaultRequestHeaders/Timeout.
                services.AddSingleton<BusBuddy.WPF.Services.IXAIChatService>(sp =>
                {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var provider = cfg["XAI:Provider"] ?? "Ollama";
                    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        return new BusBuddy.WPF.Services.OllamaChatService(new HttpClient(), cfg);
                    }

                    // Disabled / legacy Xai chat: keep mock keyword assistant (no cloud dependency)
                    return new BusBuddy.WPF.Services.XAIChatService();
                });
                services.AddTransient<BusBuddy.Core.Services.GrokGlobalAPI>(sp =>
                    new BusBuddy.Core.Services.GrokGlobalAPI(
                        new HttpClient(),
                        sp.GetRequiredService<IConfiguration>()));

                services.AddScoped<IUserSettingsService, UserSettingsService>();
                services.AddScoped<IFuelService, FuelService>();
                services.AddScoped<IMaintenanceService, MaintenanceService>();
                services.AddScoped<IScheduleService, ScheduleService>();
                services.AddScoped<IActivityScheduleService, ActivityScheduleService>();
                services.AddScoped<BusBuddy.WPF.Services.IDriverAvailabilityService, BusBuddy.WPF.Services.DriverAvailabilityService>();
                services.AddScoped<ISeedDataService, SeedDataService>();
                services.AddScoped<IStudentRouteOptimizer, StudentRouteOptimizer>();
                services.AddSingleton<PdfReportService>();
                services.AddScoped<IOperationalReportService, OperationalReportService>();

                // Register ViewModels for dependency injection (standardized on subfolder organization for dedup)
                services.AddTransient<BusBuddy.WPF.ViewModels.MainWindowViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Dashboard.DashboardViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Activity.ActivityTimelineViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Settings.SettingsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Analytics.AnalyticsDashboardViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Fuel.FuelManagementViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Maintenance.MaintenanceViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverScheduleViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Reports.ReportsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriversViewModel>();
                // Shared map VM: singleton + IServiceScopeFactory so scoped student/bus services are not captured
                services.AddSingleton<BusBuddy.WPF.ViewModels.GoogleEarth.GoogleEarthViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.GoogleEarth.GoogleEarthViewModel(
                        sp.GetRequiredService<IGeoDataService>(),
                        sp.GetService<IEligibilityService>(),
                        sp.GetService<IGeocodingService>(),
                        studentService: null,
                        busService: null,
                        scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()));

                ServiceProvider = services.BuildServiceProvider();

                            // Register ViewModels for dependency injection (cleaned duplicate block during VM dedup)
                            services.AddTransient<BusBuddy.WPF.ViewModels.MainWindowViewModel>();
                            services.AddTransient<BusBuddy.WPF.ViewModels.Dashboard.DashboardViewModel>();
                            services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>();
                            services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>();
                            services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel>();
                            services.AddTransient<BusBuddy.WPF.ViewModels.Bus.BusFormViewModel>();
                            services.AddTransient<BusBuddy.WPF.Views.Bus.BusForm>();
                // Seed database with JSON data if empty
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = ServiceProvider.CreateScope();
                        var contextFactory = scope.ServiceProvider.GetRequiredService<IBusBuddyDbContextFactory>();
                        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                        var seedSvc = new SeedDataService(contextFactory, cfg);
                        using var context = contextFactory.CreateDbContext();

                        // Ensure database is created and up to date with retry strategy
                        await BusBuddy.Core.Utilities.ResilientDbExecution.ExecuteWithResilienceAsync(
                            async () => { await context.Database.EnsureCreatedAsync(); return true; },
                            "Database EnsureCreated",
                            maxRetries: 3
                        );

                        // Import JSON data if database is empty with retry strategy
                        // Deprecated (MVP): JSON seeding disabled. Use CSV import path post-MVP.
                        // await BusBuddy.Core.Utilities.JsonDataImporter.SeedDatabaseIfEmptyAsync(context);

                        // Also support plain array JSON via SeedDataService (uses WileyJsonPath)
                        await seedSvc.SeedFromJsonAsync();
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
        /// <summary>
        /// Returns an exit code when a CLI command was handled; null means start the GUI.
        /// </summary>
        private int? TryHandleCommandLineArgs(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "--optimize-route":
                            return HandleRouteOptimization(args, i);

                        case "--generate-report":
                            return HandleReportGeneration(args, i);

                        case "--help":
                        case "-h":
                            ShowCommandLineHelp();
                            return 0;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling command line arguments");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Handle route optimization command line operation
        /// </summary>
        private int HandleRouteOptimization(string[] args, int startIndex)
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

                    switch (args[i].ToLowerInvariant())
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
                    return 1;
                }

                Log.Information("Starting command line route optimization for route {RouteId}", routeId);

                if (ServiceProvider is null)
                {
                    Console.WriteLine("Error: application services are not initialized");
                    return 1;
                }

                using var scope = ServiceProvider.CreateScope();
                var grok = scope.ServiceProvider.GetRequiredService<GrokGlobalAPI>();
                var request = new BusBuddy.Core.Models.RouteOptimizationRequest
                {
                    RouteId = routeId,
                    CurrentPerformance = currentPerformance,
                    TargetMetrics = targetMetrics,
                    Constraints = constraints
                };
                if (int.TryParse(routeId, out var parsedRouteId))
                {
                    var routes = scope.ServiceProvider.GetService<IRouteService>();
                    var routeResult = routes is null
                        ? null
                        : Task.Run(() => routes.GetRouteByIdAsync(parsedRouteId)).GetAwaiter().GetResult();
                    if (routeResult is { IsSuccess: true, Value: { } route })
                    {
                        request.StudentsServed = route.StudentCount ?? 0;
                        request.CurrentPerformance = string.IsNullOrWhiteSpace(currentPerformance) || currentPerformance == "Standard performance metrics"
                            ? $"{route.RouteName}: {route.StudentCount ?? 0} students"
                            : currentPerformance;
                    }
                }

                var result = Task.Run(() => grok.OptimizeRoutesAsync(request)).GetAwaiter().GetResult();

                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, json);
                    Log.Information("Route optimization saved to {OutputPath}", outputPath);
                }

                Console.WriteLine(json);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in route optimization");
                Console.WriteLine($"Route optimization error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Handle report generation command line operation
        /// </summary>
        private int HandleReportGeneration(string[] args, int startIndex)
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

                    switch (args[i].ToLowerInvariant())
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
                    return 1;
                }

                Log.Information("Starting command line report generation: {ReportType} -> {OutputPath}", reportType, outputPath);

                if (!OperationalReportKindParser.TryParse(reportType, out var kind))
                {
                    Console.WriteLine($"Error: unknown --report-type '{reportType}'. Use an OperationalReportKind name or Roster, RouteManifest, StudentList, DriverSchedule.");
                    return 1;
                }

                if (ServiceProvider is null)
                {
                    Console.WriteLine("Error: application services are not initialized");
                    return 1;
                }

                int? parsedRouteId = null;
                if (!string.IsNullOrWhiteSpace(routeId))
                {
                    if (!int.TryParse(routeId, out var id))
                    {
                        Console.WriteLine("Error: --route-id must be an integer RouteId");
                        return 1;
                    }

                    parsedRouteId = id;
                }

                using var scope = ServiceProvider.CreateScope();
                var reports = scope.ServiceProvider.GetRequiredService<IOperationalReportService>();
                var request = new OperationalReportRequest
                {
                    Kind = kind,
                    OutputFilePath = outputPath,
                    AsCsv = OperationalReportKindParser.IsCsvFormat(format),
                    RouteId = parsedRouteId
                };
                var generated = Task.Run(() => reports.GenerateAsync(request)).GetAwaiter().GetResult();

                Log.Information("Report generated successfully: {OutputPath}", generated.FilePath);

                var result = new
                {
                    ReportType = kind.ToString(),
                    OutputPath = generated.FilePath,
                    Format = generated.FilePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "CSV" : "PDF",
                    RouteId = routeId,
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    FileSize = generated.FileBytes.Length,
                    Status = generated.Status,
                    AiSummary = generated.AiSummary
                };

                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in report generation");
                Console.WriteLine($"Report generation error: {ex.Message}");
                return 1;
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
    --report-type <type>         Roster, RouteManifest, StudentList, DriverSchedule, or any OperationalReportKind
    --output <path>              Output file path (required)
    --route-id <id>              RouteId for Route Summary (integer)
    --format <format>            PDF (default) or CSV/Excel (writes .csv; extension is corrected)

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
        /// Ensures Syncfusion license registration (runs only once).
        /// Enhanced based on Syncfusion WPF licensing documentation and 2025 best practices.
        /// Supports explicit platform validation and better placeholder detection.
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
                // Ensure license key is in env (Passwords / alternate keychain services)
                TryLoadKeychainSecret("SYNCFUSION_LICENSE_KEY", "SYNCFUSION_LICENSE_KEY");
                TryLoadKeychainSecret("com.wileyco.syncfusion.license", "SYNCFUSION_LICENSE_KEY");
                TryLoadSyncfusionLicenseFromKeysFile();

                // Check Process level first, then User level, then Machine level
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ??
                               Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ??
                               Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    _bootstrapLogger?.Warning("⚠️ SYNCFUSION_LICENSE_KEY environment variable not set at Process, User, or Machine level. Running in trial mode.");
                    _bootstrapLogger?.Information("💡 To remove trial limitations, set SYNCFUSION_LICENSE_KEY environment variable");
                    _bootstrapLogger?.Information("💡 Get your license key from: https://www.syncfusion.com/account/downloads");
                    LogSyncfusionDiagnostics();
                    return; // trial mode – do not attempt registration
                }

                if (ValidateSyncfusionLicenseKey(licenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                    // Enhanced validation for Syncfusion v30+ (as per 2025 documentation)
                // Registration successful - v30.1.42 doesn't require explicit platform validation
                _bootstrapLogger?.Information("✅ Syncfusion license registered successfully for version 30.1.42");                    // Log additional diagnostics to help verify registration
                    _bootstrapLogger?.Information("🔍 License Key Preview: {Preview}", GetLicenseKeyPreview(licenseKey));
                    _bootstrapLogger?.Information("💡 If you see trial watermarks, verify your license key is valid and current");
                }
                else
                {
                    _bootstrapLogger?.Warning("⚠️ Provided Syncfusion license key contains placeholder or invalid format. Running in trial mode.");
                    _bootstrapLogger?.Information("� License Key Preview: {Preview}", GetLicenseKeyPreview(licenseKey));
                    _bootstrapLogger?.Information("💡 Replace placeholder with actual license key from Syncfusion account");
                    _bootstrapLogger?.Information("💡 Set via PowerShell: $env:SYNCFUSION_LICENSE_KEY = 'your-actual-license-key'");
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
        /// Windows VM / hybrid drop-in used by utm_run_in_vm.ps1:
        /// keys/SYNCFUSION_LICENSE_KEY.txt (gitignored). Mac Keychain is not available in the guest.
        /// </summary>
        private static void TryLoadSyncfusionLicenseFromKeysFile()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")))
            {
                return;
            }

            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "keys", "SYNCFUSION_LICENSE_KEY.txt")),
                @"C:\dev\BusBuddy-3\keys\SYNCFUSION_LICENSE_KEY.txt",
                Path.Combine(Directory.GetCurrentDirectory(), "keys", "SYNCFUSION_LICENSE_KEY.txt")
            };

            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var text = File.ReadAllText(path).Trim();
                    if (text.Length < 20)
                    {
                        continue;
                    }

                    Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", text);
                    _bootstrapLogger?.Information("Loaded SYNCFUSION_LICENSE_KEY from keys file (length {Length})", text.Length);
                    return;
                }
                catch (Exception ex)
                {
                    _bootstrapLogger?.Warning(ex, "Could not read Syncfusion keys file at {Path}", path);
                }
            }
        }

        /// <summary>
        /// Creates a safe preview of the license key for logging (masks sensitive parts)
        /// </summary>
        private static string GetLicenseKeyPreview(string licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
                return "Not Set";

            if (licenseKey.Length <= 8)
                return new string('*', licenseKey.Length);

            return licenseKey.Substring(0, 4) + "..." + licenseKey.Substring(licenseKey.Length - 4);
        }

        /// <summary>
        /// Validates Syncfusion license key format and provides diagnostic information
        /// Enhanced to detect common placeholders including REPLACE_WI pattern
        /// Based on Syncfusion documentation for version 30.1.42
        /// </summary>
        private static bool ValidateSyncfusionLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return false;
            }

            // Check for common invalid placeholder values and patterns
            var invalidPlaceholders = new[] {
                "YOUR_LICENSE_KEY", "YOUR LICENSE KEY", "PLACEHOLDER", "TRIAL", "DEMO",
                "REPLACE_WITH", "REPLACE_WI", "ENTER_YOUR", "INSERT_YOUR", "ADD_YOUR"
            };
            if (invalidPlaceholders.Any(placeholder =>
                licenseKey.Contains(placeholder, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Placeholder-only checks. Do not reject '/' or short tokens like "dev":
            // Syncfusion keys are often JWT-like and commonly include those characters.
            if (licenseKey.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
                licenseKey.Contains("...", StringComparison.Ordinal))
            {
                return false;
            }

            return licenseKey.Length >= 20;
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

        /// <summary>
        /// Initialize SyncFusion themes according to v30.1.42 API guidelines
        /// Sets up FluentDark as primary theme with FluentLight fallback
        /// </summary>
        private void InitializeSyncfusionThemes()
        {
            Log.Information("Initializing Syncfusion Fluent themes");
            SyncfusionThemeManager.ApplyApplicationTheme(SyncfusionThemeManager.PRIMARY_THEME);
        }
    }
}
