using BusBuddy.Core.Data;
using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Data.Repositories;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusBuddy.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<BusBuddy.Core.Data.BusBuddyDbContext>(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();
                var envOverride = PostgresConnectionResolver.ResolveAndApply()
                    ?? Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
                if (!string.IsNullOrWhiteSpace(envOverride))
                {
                    if (PostgresConnectionResolver.IsPostgresConnection(envOverride))
                    {
                        optionsBuilder.UseBusBuddyPostgres(envOverride);
                    }
                    else
                    {
                        optionsBuilder.UseSqlServer(envOverride);
                    }
                    return new BusBuddyDbContext(optionsBuilder.Options);
                }

                var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(configuration);
                var databaseProvider = EnvironmentHelper.GetDatabaseProvider(configuration);
                if (databaseProvider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlServer(connectionString);
                }
                else if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                         databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseBusBuddyPostgres(connectionString);
                }
                else if (databaseProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlite(connectionString);
                }
                else
                {
                    optionsBuilder.UseInMemoryDatabase("BusBuddyDb");
                }

                return new BusBuddyDbContext(optionsBuilder.Options);
            });

            services.AddSingleton<IBusBuddyDbContextFactory>(sp => new BusBuddyDbContextFactory(sp));
            services.AddScoped<IActivityRepository, BusBuddy.Core.Data.Repositories.ActivityRepository>();
            services.AddScoped<IBusRepository, BusBuddy.Core.Data.Repositories.BusRepository>();
            services.AddScoped<IDriverRepository, BusBuddy.Core.Data.Repositories.DriverRepository>();
            services.AddScoped<IRouteRepository, BusBuddy.Core.Data.Repositories.RouteRepository>();
            services.AddScoped<IFuelRepository, BusBuddy.Core.Data.Repositories.FuelRepository>();
            services.AddScoped<IMaintenanceRepository, BusBuddy.Core.Data.Repositories.MaintenanceRepository>();
            services.AddScoped<IScheduleRepository, BusBuddy.Core.Data.Repositories.ScheduleRepository>();
            services.AddScoped<ISchoolCalendarRepository, BusBuddy.Core.Data.Repositories.SchoolCalendarRepository>();
            services.AddScoped<IActivityScheduleRepository, BusBuddy.Core.Data.Repositories.ActivityScheduleRepository>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, BusBuddy.Core.Data.UnitOfWork.UnitOfWork>();
            services.AddScoped<IUserContextService, UserContextService>();
            services.AddMemoryCache();
            services.AddSingleton<IBusCachingService, BusCachingService>();
            services.AddSingleton<IEnhancedCachingService, EnhancedCachingService>();
            services.AddScoped<IBusService, BusService>();
            services.AddScoped<IDriverService, DriverService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<BusBuddy.Core.Services.RouteDetermination.AssignFitnessEvaluator>();
            services.AddScoped<IRouteService, RouteService>();
            services.AddScoped<IStudentRouteOptimizer, StudentRouteOptimizer>();
            services.AddSingleton<PdfReportService>();
            services.AddScoped<IOperationalReportService, OperationalReportService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IFamilyService>(sp =>
                new FamilyService(
                    sp.GetRequiredService<BusBuddyDbContext>(),
                    sp.GetService<Serilog.ILogger>() ?? Serilog.Log.ForContext<FamilyService>()));
            services.AddScoped<IGuardianService>(sp =>
                new GuardianService(
                    sp.GetRequiredService<BusBuddyDbContext>(),
                    sp.GetService<Serilog.ILogger>() ?? Serilog.Log.ForContext<GuardianService>()));
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

            // Geospatial: Google Maps Platform (Address Validation, Places, Routes, Map Tiles) + SfMap.
            services.AddGoogleMapsOptions(configuration);
            services.Configure<BusBuddy.Core.Configuration.RoutingDistrictSettings>(
                configuration.GetSection(BusBuddy.Core.Configuration.RoutingDistrictSettings.SectionName));
            services.AddSingleton<BusBuddy.Core.Configuration.IDistrictSettingsAccessor,
                BusBuddy.Core.Configuration.DistrictSettingsAccessor>();
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleAddressValidationClient(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });
            services.AddSingleton<BusBuddy.Core.Services.GoogleMaps.IMapsAddressCache>(sp =>
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BusBuddy",
                    "maps-address-cache.json");
                return new BusBuddy.Core.Services.GoogleMaps.MapsAddressCache(path);
            });
            services.AddSingleton<BusBuddy.Core.Services.GoogleMaps.IMapsGeoService>(sp =>
                new BusBuddy.Core.Services.GoogleMaps.MapsGeoService(
                    sp.GetRequiredService<BusBuddy.Core.Services.GoogleMaps.GoogleAddressValidationClient>(),
                    sp.GetRequiredService<BusBuddy.Core.Services.GoogleMaps.IMapsAddressCache>()));
            services.AddSingleton<IGeocodingService>(sp =>
                sp.GetRequiredService<BusBuddy.Core.Services.GoogleMaps.IMapsGeoService>());
            services.AddSingleton<BusBuddy.Core.Services.Interfaces.IRoutingService>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleRoutingService(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });
            services.AddSingleton<BusBuddy.Core.Services.GoogleMaps.IRouteOptimizationService>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleRouteOptimizationService(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });
            services.AddSingleton<BusBuddy.Core.Services.GoogleMaps.IPlacesAutocompleteService>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GooglePlacesAutocompleteService(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true,
                    districtSettings: sp.GetService<BusBuddy.Core.Configuration.IDistrictSettingsAccessor>());
            });
            services.AddSingleton<BusBuddy.Core.Services.GoogleMaps.IGoogleMapTileSessionService>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.GoogleMapsOptions>>();
                return new BusBuddy.Core.Services.GoogleMaps.GoogleMapTileSessionService(
                    new System.Net.Http.HttpClient(),
                    opts,
                    ownsHttpClient: true);
            });

            services.AddScoped<IAddressValidationService>(sp =>
                new AddressValidationService(
                    sp.GetService<BusBuddy.Core.Services.GoogleMaps.IMapsGeoService>()));
            services.AddScoped<IActivityLogService>(sp =>
                new ActivityLogService(
                    sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                    sp.GetService<IUserSettingsService>()));
            services.AddScoped<IDashboardMetricsService, DashboardMetricsService>();
            services.AddScoped<ITripEventService, TripEventService>();
            return services;
        }

        public static IServiceCollection AddGoogleMapsOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BusBuddy.Core.Configuration.GoogleMapsOptions>(
                configuration.GetSection(BusBuddy.Core.Configuration.GoogleMapsOptions.SectionName));
            services.PostConfigure<BusBuddy.Core.Configuration.GoogleMapsOptions>(opts =>
            {
                opts.QuotaProject = BusBuddy.Core.Configuration.GoogleMapsOptions.ResolveQuotaProject(opts.QuotaProject);
            });
            return services;
        }
    }
}
