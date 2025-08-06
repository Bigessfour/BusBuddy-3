
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Extensions
{
    /// <summary>
    /// Extension methods for IServiceCollection to register UI services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds UI logging services to the dependency injection container
        /// </summary>
        public static IServiceCollection AddUILogging(this IServiceCollection services)
        {
            // Register UI-specific logging services
            services.AddSingleton<BusBuddyContextEnricher>();
            services.AddTransient<UIOperationEnricher>();
            services.AddSingleton<PerformanceEnricher>();
            return services;
        }
    }
}

