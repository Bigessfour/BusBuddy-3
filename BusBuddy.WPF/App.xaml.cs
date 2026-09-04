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
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Serilog.Formatting.Json;
using Serilog.Settings.Configuration;
using System.Threading.Tasks;
using BusBuddy.WPF.Utilities;
using Syncfusion.Licensing;

namespace BusBuddy.WPF
{
    /// <summary>
    /// BusBuddy WPF Application startup with dual-mode operation
    /// - EF Migration Mode: Minimal services for database operations only
    /// - UI Mode: Full dependency injection with robust error handling
    /// Features: Pure Serilog logging, Syncfusion license management, comprehensive error capture
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }
        private static ILogger? _bootstrapLogger;
        private static bool _syncfusionLicenseChecked; // guard to ensure single execution

        public App()
        {
            EntityFrameworkPostgresExtensions.ConfigureNpgsqlAppContext();

            // Syncfusion WPF docs: register in App() before any Syncfusion control is initialized.
            // https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
            LoadKeysDotEnv();
            RegisterSyncfusionLicenseOnce();

            // Initialize bootstrap logger after license (logging does not touch Syncfusion UI assemblies).
            InitializeBootstrapLogger();

            _bootstrapLogger?.Information("BusBuddy bootstrap starting (Syncfusion license step complete)");

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

            Log.Information("🚌 BusBuddy starting...");
        }

        /// <summary>Load keys/.env into process environment (SYNCFUSION_LICENSE_KEY, etc.).</summary>
        private static void LoadKeysDotEnv()
        {
            var count = EnvFileLoader.LoadIntoEnvironment(EnvFileLoader.GetKeysEnvFileCandidates());
            if (count > 0)
            {
                Console.WriteLine($"BusBuddy: loaded {count} value(s) from keys/.env");
            }

            // Mac Passwords + legacy keys file for other secrets (do not overwrite .env).
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX))
            {
                LoadApiKeysFromMacPasswords();
            }

            TryLoadSecretsFromKeysFile();
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

            // Syncfusion inputs ignore NumPad on inner TextBox hosts — fix app-wide.
            NumpadInputHelper.RegisterApplicationWide();
            InputCaretHelper.RegisterApplicationWide();

            try
            {
                Log.Information("🚌 Initializing BusBuddy application");

                var resolvedConnection = PostgresConnectionResolver.ResolveAndApply();
                Log.Information(
                    "Postgres endpoint after resolver: {Endpoint}",
                    PostgresConnectionResolver.DescribeEndpoint(resolvedConnection)
                        ?? "(not configured — using appsettings provider)");

                // Setup minimal DI for Students, Routes, Buses, Drivers (synchronous)
                ConfigureServices();

                // Removed redundant explicit district JSON seeding. Seeding now handled via EF Core 9 UseSeeding/UseAsyncSeeding

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

                Log.Information("🚌 BusBuddy application started successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "🚌 Failed to start BusBuddy application");
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

                // Route geography. Maps Platform clients (IGeocodingService / IRoutingService)
                // are registered in AddDataServices above — do not register OfflineGeocodingService here.
                // District/town shapefile eligibility was removed: those polygons were for another district.
                services.AddSingleton<IGeoDataService>(sp =>
                    new GeoDataService(sp.GetService<IBusBuddyDbContextFactory>()));

                // Register core business services for Students, Routes, Buses, Drivers
                services.AddScoped<IStudentService, StudentService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.IDestinationService, BusBuddy.Core.Services.DestinationService>();
                services.AddScoped<BusBuddy.Core.Services.IStudentSchoolTransferService, BusBuddy.Core.Services.StudentSchoolTransferService>();
                services.AddScoped<BusBuddy.Core.Services.IRouteWaypointRebuildService, BusBuddy.Core.Services.RouteWaypointRebuildService>();
                services.AddScoped<BusBuddy.Core.Services.IDriverTrainingService, BusBuddy.Core.Services.DriverTrainingService>();
                services.AddScoped<BusBuddy.Core.Services.RouteDetermination.AssignFitnessEvaluator>();
                services.AddScoped<BusBuddy.Core.Services.RouteDetermination.IRouteDeterminationService,
                    BusBuddy.Core.Services.RouteDetermination.RouteDeterminationService>();
                services.Configure<BusBuddy.Core.Configuration.RoutingDistrictSettings>(
                    configuration.GetSection(BusBuddy.Core.Configuration.RoutingDistrictSettings.SectionName));
                services.AddScoped<IDriverService, DriverService>();
                services.AddScoped<IRouteService, RouteService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.IBusService, BusService>();

                // Register UI services (commented out — services don't exist yet)
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

                services.AddSingleton<IUserSettingsService, UserSettingsService>();
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
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentFormViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Student.StudentFormViewModel(
                        sp.GetRequiredService<IStudentService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel(
                        sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                        sp.GetService<IRouteService>(),
                        sp.GetService<BusBuddy.Core.Services.RouteDetermination.IRouteDeterminationService>(),
                        sp.GetService<BusBuddy.Core.Services.Interfaces.IDestinationService>(),
                        sp.GetService<BusBuddy.Core.Services.Interfaces.IRoutingService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriversViewModel>();
                // Shared map VM: singleton + IServiceScopeFactory so scoped student/bus services are not captured
                services.AddSingleton<BusBuddy.WPF.ViewModels.Map.MapViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Map.MapViewModel(
                        sp.GetRequiredService<IGeoDataService>(),
                        sp.GetService<IGeocodingService>(),
                        studentService: null,
                        busService: null,
                        scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
                        routingService: sp.GetService<BusBuddy.Core.Services.Interfaces.IRoutingService>()));

                ServiceProvider = services.BuildServiceProvider();

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

                        await BusBuddy.Core.Utilities.ResilientDbExecution.ExecuteWithResilienceAsync(
                            async () =>
                            {
                                await RelationalSchemaApplier.ApplyAsync(context.Database);
                                return true;
                            },
                            "Database Migrate",
                            maxRetries: 3
                        );

                        // Import JSON data if database is empty with retry strategy
                        // JSON seeding disabled. Use CSV import path.
                        // await BusBuddy.Core.Utilities.JsonDataImporter.SeedDatabaseIfEmptyAsync(context);

                        // Also support plain array JSON via SeedDataService (uses StudentJsonPath)
                        await seedSvc.SeedFromJsonAsync();
                        await seedSvc.EnsureMapDemoGeoAsync();
                    }
                    catch (Exception seedEx)
                    {
                        Log.Warning(seedEx, "Failed to seed database (JSON or map demo geo): {Error}", seedEx.Message);
                    }
                }); Log.Information("✅ Full DI container configured successfully for UI application");
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
                            return new MainWindow(viewModel);
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
                    Log.Warning("Creating MainWindow without DI; dock grids stay empty until services are available");
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
            Log.Information("🚌 BusBuddy application shutting down");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        /// <summary>
        /// Sole Syncfusion license registration point (App() constructor, before any control init).
        /// https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
        /// </summary>
        private static void RegisterSyncfusionLicenseOnce()
        {
            if (_syncfusionLicenseChecked)
            {
                return;
            }
            _syncfusionLicenseChecked = true;

            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")?.Trim();
            if (string.IsNullOrEmpty(licenseKey) || licenseKey.StartsWith("${", StringComparison.Ordinal))
            {
                Console.WriteLine("BusBuddy: SYNCFUSION_LICENSE_KEY not set — add keys/.env or set env var.");
                return;
            }

            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            Console.WriteLine($"BusBuddy: Syncfusion license registered in App() (length {licenseKey.Length}).");
        }

        /// <summary>Legacy keys/*.txt fallback for Google Maps only (Syncfusion uses keys/.env).</summary>
        private static void TryLoadSecretsFromKeysFile()
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "keys", "SYNCFUSION_LICENSE_KEY.txt"),
                @"C:\dev\BusBuddy-3\keys\SYNCFUSION_LICENSE_KEY.txt",
                @"C:\dev\busbuddy\keys\SYNCFUSION_LICENSE_KEY.txt",
            };

            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string? mapsKey = null;
                    foreach (var raw in File.ReadAllLines(path))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith('#'))
                        {
                            continue;
                        }

                        if (TryParseLabeledSecret(line, "Google_Maps_Demo_Key", out var demoMaps) ||
                            TryParseLabeledSecret(line, "GOOGLE_MAPS_API_KEY", out demoMaps))
                        {
                            mapsKey = demoMaps;
                        }
                    }

                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY")) &&
                        !string.IsNullOrWhiteSpace(mapsKey))
                    {
                        Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", mapsKey.Trim());
                    }

                    return;
                }
                catch (Exception ex)
                {
                    _bootstrapLogger?.Warning(ex, "Could not read secrets keys file at {Path}", path);
                }
            }
        }

        private static bool TryParseLabeledSecret(string line, string label, out string value)
        {
            value = string.Empty;
            var prefix = label + ":";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = line[prefix.Length..].Trim();
            return value.Length > 0;
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
        /// Provides detailed diagnostic information for Syncfusion licensing issues
        /// </summary>
        private static void LogSyncfusionDiagnostics()
        {
            var logger = _bootstrapLogger ?? Log.Logger;

            logger.Information("🔍 Syncfusion Diagnostics:");
            logger.Information("   NuGet pin: 34.2.3 (Directory.Build.props SyncfusionVersion)");
            logger.Information("   Platform: WPF (.NET 9.0-windows)");
            logger.Information("   Registration: App() constructor from keys/.env SYNCFUSION_LICENSE_KEY");

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
        /// Initialize SyncFusion themes. SfSkinManager.ApplicationTheme owns the skin.
        /// Restores the last theme saved from MainWindow / Settings.
        /// </summary>
        private void InitializeSyncfusionThemes()
        {
            Log.Information("Initializing Syncfusion Fluent themes from saved preference");
            SyncfusionThemeManager.ApplySavedApplicationTheme();
        }
    }
}
