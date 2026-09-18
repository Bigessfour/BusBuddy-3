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
using BusBuddy.Core.Services.Interfaces;
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
                // are registered in AddDataServices above. No hash geocoder; no shapefile geofence.
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
                services.AddSingleton<BusBuddy.Core.Configuration.IDistrictSettingsAccessor,
                    BusBuddy.Core.Configuration.DistrictSettingsAccessor>();
                services.AddScoped<IDriverService, DriverService>();
                services.AddScoped<IRouteService, RouteService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.IBusService, BusService>();

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

                services.AddSingleton<IUserSettingsService, UserSettingsService>();
                services.AddScoped<IFuelService, FuelService>();
                services.AddScoped<IFuelLocationCatalog, FuelLocationCatalog>();
                services.AddScoped<ITripReasonCatalog, TripReasonCatalog>();
                services.AddScoped<IMaintenanceService, MaintenanceService>();
                services.AddScoped<IScheduleService, ScheduleService>();
                services.AddScoped<IActivityScheduleService, ActivityScheduleService>();
                services.AddScoped<BusBuddy.Core.Services.Interfaces.ITripEventService, BusBuddy.Core.Services.TripEventService>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Activity.ActivityManagementViewModel>();
                services.AddScoped<BusBuddy.WPF.Services.IDriverAvailabilityService, BusBuddy.WPF.Services.DriverAvailabilityService>();
                services.AddScoped<ISeedDataService, SeedDataService>();
                services.AddScoped<IStudentRouteOptimizer, StudentRouteOptimizer>();
                services.AddSingleton<PdfReportService>();
                services.AddScoped<IOperationalReportService, OperationalReportService>();

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
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentsViewModel>();
                services.AddTransient<BusBuddy.WPF.ViewModels.Student.StudentFormViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Student.StudentFormViewModel(
                        sp.GetRequiredService<IStudentService>()));
                services.AddTransient<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>(sp =>
                    new BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel(
                        sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                        sp.GetRequiredService<IRouteService>(),
                        sp.GetService<BusBuddy.Core.Services.RouteDetermination.IRouteDeterminationService>(),
                        sp.GetService<BusBuddy.Core.Services.Interfaces.IDestinationService>(),
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
                        sp.GetService<IGeocodingService>(),
                        studentService: null,
                        scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
                        routingService: sp.GetService<BusBuddy.Core.Services.Interfaces.IRoutingService>(),
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
                Log.Information("Applied persisted district geography from user settings");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not overlay district geography from user settings");
            }
        }
    }
}
