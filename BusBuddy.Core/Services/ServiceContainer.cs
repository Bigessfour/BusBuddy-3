using BusBuddy.Core.Data;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.Core.Services
{
    public static class ServiceContainer
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Note: DbContext is registered in the WPF App.xaml.cs to avoid duplicate registration
            // services.AddDbContext<BusBuddyDbContext> is handled by the main application

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IActivityService, ActivityService>();

            // Register other core services and repositories here
            // e.g., services.AddScoped<IBusRepository, BusRepository>();
        }
    }
}
