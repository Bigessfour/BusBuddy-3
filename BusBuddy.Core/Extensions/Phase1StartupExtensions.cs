using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Core.Extensions;

/// <summary>
/// Phase 1 Startup Extensions
/// Handles Phase 1 initialization including data seeding
/// </summary>
public static class Phase1StartupExtensions
{
    /// <summary>
    /// Initializes Phase 1 data and services
    /// </summary>
    public static async Task InitializePhase1Async(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = Serilog.Log.ForContext<Phase1DataSeedingService>();

        try
        {
            logger.Information("🚀 Starting Phase 1 initialization...");

            // Initialize database and seed data
            var dataSeeder = scope.ServiceProvider.GetRequiredService<Phase1DataSeedingService>();
            await dataSeeder.SeedPhase1DataAsync();

            // Get data summary
            var summary = await dataSeeder.GetDataSummaryAsync();
            logger.Information("{Summary}", summary);

            logger.Information("✅ Phase 1 initialization completed successfully!");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "❌ Phase 1 initialization failed");
            throw;
        }
    }

    /// <summary>
    /// Registers Phase 1 services
    /// </summary>
    public static IServiceCollection AddPhase1Services(this IServiceCollection services)
    {
        services.AddScoped<Phase1DataSeedingService>();

        // Register GeoDataService with placeholder configuration
        services.AddScoped<IGeoDataService>(serviceProvider =>
        {
            // TODO: Replace with actual configuration values
            var geeApiBaseUrl = "https://earthengine.googleapis.com";
            var geeAccessToken = Environment.GetEnvironmentVariable("GEE_ACCESS_TOKEN") ?? "placeholder_token";
            return new GeoDataService(geeApiBaseUrl, geeAccessToken);
        });

        return services;
    }
}
