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
            services.AddTransient<BusBuddyDbContext>(provider =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

                // Get connection string based on configuration
                var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(configuration);
                var databaseProvider = configuration["DatabaseProvider"] ?? "LocalDB";

                // Configure based on database provider
                if (databaseProvider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlServer(connectionString);
                }
                else if (databaseProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
                {
                    optionsBuilder.UseSqlServer(connectionString);
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

            // Register DbContext Factory for thread-safe context creation
            services.AddSingleton<IBusBuddyDbContextFactory, BusBuddyDbContextFactory>();

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
            services.AddScoped<IRouteService, RouteService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IFuelService, FuelService>();
            services.AddScoped<IMaintenanceService, MaintenanceService>();
            services.AddScoped<IScheduleService, ScheduleService>();
            services.AddScoped<IStudentScheduleService, StudentScheduleService>();
            // REMOVED: ITicketService - deprecated module

            // Register Address Validation Service
            services.AddScoped<IAddressValidationService, AddressValidationService>();

            // Register Activity Log Service
            services.AddScoped<IActivityLogService, ActivityLogService>();

            // Register Dashboard Metrics Service
            services.AddScoped<IDashboardMetricsService, DashboardMetricsService>();

            // Register Database NULL Fix Service
            services.AddScoped<BusBuddy.Core.Services.DatabaseNullFixService>();

            // Register Phase 2 Data Seeding Service
            services.AddScoped<IPhase2DataSeederService, Phase2DataSeederService>();

            return services;
        }
    }
}
