using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;

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
    public static void InitializePhase1Async(this IServiceProvider serviceProvider)
    {
        // Phase1DataSeedingService is deprecated/removed for MVP. Data seeding handled by SeedDataService.
    }

    /// <summary>
    /// Registers Phase 1 services
    /// </summary>
    public static IServiceCollection AddPhase1Services(this IServiceCollection services)
    {
        // services.AddScoped<Phase1DataSeedingService>(); // Disabled: service removed for MVP

        // Register GeoDataService with placeholder configuration
        services.AddScoped<IGeoDataService>(serviceProvider =>
        {
            // TODO: Replace with actual configuration values
            var geeApiBaseUrl = "https://earthengine.googleapis.com";
            var geeAccessToken = Environment.GetEnvironmentVariable("GEE_ACCESS_TOKEN") ?? "placeholder_token";
            return new GeoDataService(geeApiBaseUrl, geeAccessToken);
        });

    // Eligibility service removed for MVP — shapefile approach deprecated.

        return services;
    }
}
