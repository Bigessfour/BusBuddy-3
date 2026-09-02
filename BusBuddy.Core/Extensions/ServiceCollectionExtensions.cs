using BusBuddy.Core.Data;
using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Data.Repositories;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusBuddy.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering data services with dependency injection
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register all data services including DbContext, repositories, and Unit of Work
        /// </summary>
        public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext with proper configuration-based connection string
            services.AddTransient<BusBuddy.Core.Data.BusBuddyDbContext>(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

                // Highest precedence: environment override for quick diagnostics
                var envOverride = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
                if (!string.IsNullOrWhiteSpace(envOverride))
                {
                    if (envOverride.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                        envOverride.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                    {
                        optionsBuilder.UseNpgsql(envOverride);
                    }
                    else
                    {
                        optionsBuilder.UseSqlServer(envOverride);
                    }
                    return new BusBuddyDbContext(optionsBuilder.Options);
                }

                // Get connection string based on configuration
                var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(configuration);
                var databaseProvider = configuration["DatabaseProvider"] ?? "LocalDB";

                // Configure based on database provider
                if (databaseProvider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlServer(connectionString);
                }
                else if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                         databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseNpgsql(connectionString);
                }
                else if (databaseProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlite(connectionString);
                }
                else
                {
                    // Default to in-memory for unknown providers or testing
                    optionsBuilder.UseInMemoryDatabase("BusBuddyDb");
                }

                return new BusBuddyDbContext(optionsBuilder.Options);
            });

            // Register DbContext Factory for thread-safe context creation with access to IConfiguration via IServiceProvider
            services.AddSingleton<IBusBuddyDbContextFactory>(sp => new BusBuddyDbContextFactory(sp));

            // Register repositories - use fully qualified names to avoid ambiguity
            services.AddScoped<IVehicleRepository, BusBuddy.Core.Data.Repositories.VehicleRepository>();
            services.AddScoped<IActivityRepository, BusBuddy.Core.Data.Repositories.ActivityRepository>();
            services.AddScoped<IBusRepository, BusBuddy.Core.Data.Repositories.BusRepository>();
            services.AddScoped<IDriverRepository, BusBuddy.Core.Data.Repositories.DriverRepository>();
            services.AddScoped<IRouteRepository, BusBuddy.Core.Data.Repositories.RouteRepository>();
            services.AddScoped<IStudentRepository, BusBuddy.Core.Data.Repositories.StudentRepository>();
            services.AddScoped<IFuelRepository, BusBuddy.Core.Data.Repositories.FuelRepository>();
            services.AddScoped<IMaintenanceRepository, BusBuddy.Core.Data.Repositories.MaintenanceRepository>();
            services.AddScoped<IScheduleRepository, BusBuddy.Core.Data.Repositories.ScheduleRepository>();
            services.AddScoped<ISchoolCalendarRepository, BusBuddy.Core.Data.Repositories.SchoolCalendarRepository>();
            services.AddScoped<IActivityScheduleRepository, BusBuddy.Core.Data.Repositories.ActivityScheduleRepository>();

            // Register generic repository
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Register Unit of Work
            services.AddScoped<IUnitOfWork, BusBuddy.Core.Data.UnitOfWork.UnitOfWork>();

            // Register User Context Service
            services.AddScoped<IUserContextService, UserContextService>();

            // Register memory caching services - CRITICAL for BusCachingService
            services.AddMemoryCache();
            services.AddSingleton<IBusCachingService, BusCachingService>();
            services.AddSingleton<IEnhancedCachingService, EnhancedCachingService>();

            // Register Business Services
            services.AddScoped<IBusService, BusService>();
            services.AddScoped<IDriverService, DriverService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<BusBuddy.Core.Services.RouteDetermination.AssignFitnessEvaluator>();
            services.AddScoped<IRouteService, RouteService>();
            services.AddScoped<IStudentRouteOptimizer, StudentRouteOptimizer>();
            services.AddSingleton<PdfReportService>();
            services.AddScoped<IOperationalReportService, OperationalReportService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IDestinationService, DestinationService>();
            services.AddScoped<IPickupStopService, PickupStopService>();
            services.AddScoped<IStudentSchoolTransferService, StudentSchoolTransferService>();
            services.AddScoped<IRouteWaypointRebuildService, RouteWaypointRebuildService>();
            services.AddScoped<IDriverTrainingService, DriverTrainingService>();
            services.AddScoped<BusBuddy.Core.Services.RouteDetermination.IRouteDeterminationService,
                BusBuddy.Core.Services.RouteDetermination.RouteDeterminationService>();
            services.AddScoped<IFuelService, FuelService>();
            services.AddScoped<IMaintenanceService, MaintenanceService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<IStudentScheduleService, StudentScheduleService>();
            services.AddScoped<IFleetMonitoringService, FleetMonitoringService>();
            // REMOVED: ITicketService - deprecated module

            // Geospatial: Google Maps Platform (Address Validation). Do not register OfflineGeocodingService in production.
            services.Configure<BusBuddy.Core.Configuration.GoogleMapsOptions>(
                configuration.GetSection(BusBuddy.Core.Configuration.GoogleMapsOptions.SectionName));
            services.Configure<BusBuddy.Core.Configuration.RoutingDistrictSettings>(
                configuration.GetSection(BusBuddy.Core.Configuration.RoutingDistrictSettings.SectionName));
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleAddressValidationClient(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });
            services.AddSingleton<IGeocodingService>(sp =>
                sp.GetRequiredService<BusBuddy.Core.Services.GoogleMaps.GoogleAddressValidationClient>());
            services.AddSingleton<BusBuddy.Core.Services.Interfaces.IRoutingService>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleRoutingService(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });

            // Register Address Validation Service (delegates to Maps client when key present)
            services.AddScoped<IAddressValidationService>(sp =>
                new AddressValidationService(
                    sp.GetRequiredService<IUnitOfWork>(),
                    sp.GetService<BusBuddy.Core.Services.GoogleMaps.GoogleAddressValidationClient>()));

            // Register Activity Log Service
            services.AddScoped<IActivityLogService, ActivityLogService>();

            // Register Dashboard Metrics Service
            services.AddScoped<IDashboardMetricsService, DashboardMetricsService>();

            // Note: Legacy Phase seeders, DataIntegrity, DatabaseNullFix etc. archived in Final-Portfolio-Baseline-2026-06-Legacy-Cleanse.
            // Core seeding is via SeedDataService (Postgres/Docker primary for testing).

            return services;
        }
    }
}
