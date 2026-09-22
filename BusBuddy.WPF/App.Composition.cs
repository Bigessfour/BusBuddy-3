using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Extensions;
using BusBuddy.Core.Utilities;

namespace BusBuddy.WPF
{
    public partial class App
    {
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

                var configuration = BuildConfiguration();

                // Register configuration for DI
                services.AddSingleton<IConfiguration>(configuration);

                // Core students, routes, buses, maps, and trips are registered once here.
                services.AddDataServices(configuration);

                services.AddTransient<BusBuddy.WPF.Services.RouteExportService>();
                services.AddSingleton<BusBuddy.WPF.Services.ISkinManagerService, BusBuddy.WPF.Services.SkinManagerService>();

                // Local AI chat (Ollama). Separate HttpClient — OllamaAiService mutates DefaultRequestHeaders/Timeout.
                services.AddSingleton<BusBuddy.WPF.Services.IAiChatService>(sp =>
                    new BusBuddy.WPF.Services.OllamaChatService(
                        new HttpClient(),
                        sp.GetRequiredService<IConfiguration>()));
                services.AddTransient<BusBuddy.Core.Services.OllamaAiService>(sp =>
                    new BusBuddy.Core.Services.OllamaAiService(
                        new HttpClient(),
                        sp.GetRequiredService<IConfiguration>()));

                services.AddTransient<BusBuddy.WPF.ViewModels.Activity.ActivityManagementViewModel>();
                services.AddScoped<BusBuddy.WPF.Services.IDriverAvailabilityService, BusBuddy.WPF.Services.DriverAvailabilityService>();

                // Register ViewModels for dependency injection (standardized on subfolder organization for dedup)
                services.AddTransient<BusBuddy.WPF.ViewModels.MainWindowViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Dashboard.DashboardViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Activity.ActivityTimelineViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Activity.ActivityTimelineViewModel(
                        sp.GetService<IActivityLogService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Settings.SettingsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Analytics.AnalyticsDashboardViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Fuel.FuelManagementViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Maintenance.MaintenanceViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverScheduleViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Reports.ReportsViewModel>();
                // Explicit factory: StudentsViewModel also has a parameterless XAML ctor and a
                // BusBuddyDbContext test ctor. Both DbContext and IBusBuddyDbContextFactory are
                // registered, so AddTransient<T>() hits "constructors are ambiguous" at runtime.
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Student.StudentsViewModel(
                        sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                        sp.GetService<IStudentService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentFormViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Student.StudentFormViewModel(
                        sp.GetRequiredService<IStudentService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel(
                        sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                        sp.GetRequiredService<IRouteService>(),
                        sp.GetService<BusBuddy.Core.Services.RouteDetermination.IRouteDeterminationService>(),
                        sp.GetService<BusBuddy.Core.Services.IDestinationService>(),
                        sp.GetService<BusBuddy.Core.Services.GoogleMaps.IRouteOptimizationService>(),
                        sp.GetService<BusBuddy.WPF.ViewModels.Map.MapViewModel>(),
                        sp.GetService<IScheduleService>(),
                        sp.GetService<BusBuddy.WPF.Services.RouteExportService>(),
                        sp.GetService<IOperationalReportService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Driver.DriversViewModel>();
                // Shared map VM: singleton + IServiceScopeFactory so scoped student/bus services are not captured
                services.AddSingleton<BusBuddy.WPF.ViewModels.Map.MapViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Map.MapViewModel(
                        sp.GetRequiredService<IGeoDataService>(),
                        studentService: null,
                        scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
                        routingService: sp.GetService<BusBuddy.Core.Services.IRoutingService>(),
                        districtSettings: sp.GetService<IDistrictSettingsAccessor>()));
                services.AddSingleton<BusBuddy.WPF.Services.IDistrictMapSync, BusBuddy.WPF.Services.DistrictMapSync>();

                ServiceProvider = services.BuildServiceProvider();
                ApplyPersistedDistrictSettings();
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = ServiceProvider.CreateScope();
                        var contextFactory = scope.ServiceProvider.GetRequiredService<IBusBuddyDbContextFactory>();
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

                        // Do not auto-seed students. SeedSpecialNeedsTransportPrep used to re-insert
                        // deleted TEST_STUDENT_* rows on every launch. That insert is gone.
                        // Roster is clerk-entered (Add Student / Import CSV).
                        Log.Information(
                            "Startup student seed skipped (SeedFromJson/EnsureMapDemoGeo). Use Import CSV to load a roster.");
                    }
                    catch (Exception schemaEx)
                    {
                        Log.Warning(schemaEx, "Failed to apply database schema: {Error}", schemaEx.Message);
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
                    var configuration = BuildConfiguration();

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

        private static void ApplyPersistedDistrictSettings()
        {
            try
            {
                var settings = ServiceProvider?.GetService<IUserSettingsService>();
                var district = ServiceProvider?.GetService<IDistrictSettingsAccessor>();
                if (settings is null || district is null)
                {
                    return;
                }

                settings.LoadSettingsAsync().GetAwaiter().GetResult();
                district.OverlayFromUserSettings(settings);
                var applied = district.Current;
                Log.Information(
                    "Applied persisted district geography Path={Path} HasDepotKey={HasDepotKey} DepotLat={DepotLat} DepotLon={DepotLon} BBoxMinLat={BBoxMinLat} BBoxMaxLat={BBoxMaxLat}",
                    settings.FilePath,
                    settings.HasKey(UserSettingsKeys.DistrictDepotLatitude),
                    applied.DepotLatitude,
                    applied.DepotLongitude,
                    applied.BoundingBoxMinLat,
                    applied.BoundingBoxMaxLat);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not overlay district geography from user settings");
            }
        }
    }
}
