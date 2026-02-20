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
// Phase-based extension removed; direct registrations used instead
using BusBuddy.Core.Extensions; // Needed for AddDataServices extension
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Serilog.Formatting.Json;
using Serilog.Settings.Configuration;
using System.Threading.Tasks;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.FluentDark.WPF;
using Syncfusion.Themes.FluentLight.WPF;
using BusBuddy.WPF.Services;

namespace BusBuddy.WPF
{
    /// <summary>
    /// BusBuddy WPF Application startup with modern .NET 9 patterns
    /// Simplified architecture with clean dependency injection and error handling
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
            // Initialize logging first
            ConfigureLogging();

            // Register Syncfusion license
            RegisterSyncfusionLicense();

            // Setup dependency injection
            ConfigureServices();
        }

        /// <summary>
        /// Configure Serilog logging with modern patterns
        /// </summary>
        private void ConfigureLogging()
        {
            var configuration = BuildConfiguration();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .CreateLogger();

            Log.Information("🚌 BusBuddy application starting...");
        }

        /// <summary>
        /// Build configuration from multiple sources
        /// </summary>
        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.azure.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        /// <summary>
        /// Register Syncfusion license from environment variable
        /// </summary>
        private void RegisterSyncfusionLicense()
        {
            try
            {
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("Syncfusion license registered successfully");
                }
                else
                {
                    Log.Warning("Syncfusion license key not found in environment variables");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register Syncfusion license");
            }
        }

        /// <summary>
        /// Configure services using modern DI patterns
        /// </summary>
        private void ConfigureServices()
        {
            var services = new ServiceCollection();
            var configuration = BuildConfiguration();

            // Configuration
            services.AddSingleton<IConfiguration>(configuration);

            // Database and core services
            services.AddDataServices(configuration);

            // Business services
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IDriverService, DriverService>();
            services.AddScoped<IRouteService, RouteService>();
            services.AddScoped<IBusService, BusService>();

            // WPF services
            services.AddSingleton<ISkinManagerService, SkinManagerService>();
            services.AddTransient<RouteExportService>();

            // ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<StudentsViewModel>();
            services.AddTransient<RouteManagementViewModel>();
            services.AddTransient<DriverFormViewModel>();
            services.AddTransient<BusFormViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<GoogleEarthViewModel>();

            // Lazy ViewModel service
            services.AddSingleton<ILazyViewModelService, LazyViewModelService>();

            ServiceProvider = services.BuildServiceProvider();
            Log.Information("✅ Dependency injection configured successfully");
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global error handlers
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            try
            {
                Log.Information("Initializing main window...");

                // Initialize Syncfusion themes
                InitializeSyncfusionThemes();

                // Create and show main window
                var mainWindow = CreateMainWindow();
                mainWindow.Show();

                // Seed data in background
                _ = Task.Run(async () => await SeedDataAsync());

                Log.Information("🚌 BusBuddy application started successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to start application");
                MessageBox.Show($"Failed to start application: {ex.Message}", "BusBuddy Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Initialize Syncfusion themes
        /// </summary>
        private void InitializeSyncfusionThemes()
        {
            try
            {
                SfSkinManager.ApplyStylesOnApplication = true;
                Log.Information("Syncfusion themes initialized");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Syncfusion themes");
            }
        }

        /// <summary>
        /// Create the main window with proper ViewModel injection
        /// </summary>
        private MainWindow CreateMainWindow()
        {
            if (ServiceProvider == null)
                throw new InvalidOperationException("Service provider not initialized");

            var viewModel = ServiceProvider.GetService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            return mainWindow;
        }

        /// <summary>
        /// Seed initial data
        /// </summary>
        private async Task SeedDataAsync()
        {
            try
            {
                if (ServiceProvider == null) return;

                using var scope = ServiceProvider.CreateScope();
                var seedService = scope.ServiceProvider.GetService<ISeedDataService>();

                if (seedService != null && IsDevelopmentEnvironment())
                {
                    await seedService.SeedBusesAsync(12);
                    Log.Information("Development data seeded successfully");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to seed development data");
            }
        }

        /// <summary>
        /// Check if running in development environment
        /// </summary>
        private bool IsDevelopmentEnvironment()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Handle unhandled UI exceptions
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled UI exception: {Message}", e.Exception.Message);

            var result = MessageBox.Show(
                $"An unexpected error occurred: {e.Exception.Message}\n\nContinue running application?",
                "BusBuddy Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.No)
            {
                Shutdown();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handle unhandled non-UI exceptions
        /// </summary>
        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled application exception");

            if (e.IsTerminating)
            {
                MessageBox.Show("A critical error occurred. The application will close.",
                    "BusBuddy Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("🚌 BusBuddy application shutting down");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
