using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BusBuddy.Core.Configuration;
using System.Net.Http;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services;

/// <summary>
/// Azure Configuration Service - Phase 1 Simplified
/// Provides centralized configuration management for Azure deployment with fallback to local development.
/// </summary>
public interface IAzureConfigurationService
{
    bool IsAzureDeployment { get; }
    GoogleEarthEngineOptions GoogleEarthEngineOptions { get; }
    XaiOptions XaiOptions { get; }
    AppSettingsOptions AppSettingsOptions { get; }
    Task<bool> ValidateConfigurationAsync();
    void RegisterServices(IServiceCollection services);
}

public class AzureConfigurationService : IAzureConfigurationService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AzureConfigurationService>();
    private readonly IConfiguration _configuration;

    public GoogleEarthEngineOptions GoogleEarthEngineOptions { get; }
    public XaiOptions XaiOptions { get; }
    public AppSettingsOptions AppSettingsOptions { get; }

    public bool IsAzureDeployment => AppSettingsOptions.DatabaseProvider == "Azure";

    public AzureConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Bind configuration sections to strongly-typed options
        GoogleEarthEngineOptions = new GoogleEarthEngineOptions();
        _configuration.GetSection(GoogleEarthEngineOptions.SectionName).Bind(GoogleEarthEngineOptions);

        XaiOptions = new XaiOptions();
        _configuration.GetSection(XaiOptions.SectionName).Bind(XaiOptions);

        AppSettingsOptions = new AppSettingsOptions();
        _configuration.GetSection(AppSettingsOptions.SectionName).Bind(AppSettingsOptions);

        Logger.Information("Azure Configuration Service initialized. Deployment mode: {DeploymentMode}",
            IsAzureDeployment ? "Azure" : "Local");
    }

    public Task<bool> ValidateConfigurationAsync()
    {
        using (LogContext.PushProperty("Operation", "ValidateConfiguration"))
        {
            try
            {
                var validationResults = new List<string>();

                // Validate Google Earth Engine configuration
                if (string.IsNullOrEmpty(GoogleEarthEngineOptions.ProjectId) ||
                    GoogleEarthEngineOptions.ProjectId.Contains("YOUR_PROJECT_ID"))
                {
                    validationResults.Add("Google Earth Engine ProjectId not configured");
                }

                if (string.IsNullOrEmpty(GoogleEarthEngineOptions.ServiceAccountKeyPath) ||
                    !System.IO.File.Exists(GoogleEarthEngineOptions.ServiceAccountKeyPath))
                {
                    validationResults.Add($"Google Earth Engine service account key not found: {GoogleEarthEngineOptions.ServiceAccountKeyPath}");
                }

                // Validate xAI configuration
                if (string.IsNullOrEmpty(XaiOptions.ApiKey) ||
                    XaiOptions.ApiKey.Contains("YOUR_XAI_API_KEY") ||
                    XaiOptions.ApiKey.StartsWith("${", StringComparison.Ordinal))
                {
                    validationResults.Add("xAI API key not configured or environment variable not resolved");
                }

                // Validate database connection
                var connectionString = IsAzureDeployment
                    ? _configuration.GetConnectionString("DefaultConnection")
                    : _configuration.GetConnectionString("LocalConnection");

                if (string.IsNullOrEmpty(connectionString) ||
                    connectionString.Contains("${DATABASE_CONNECTION_STRING}"))
                {
                    validationResults.Add($"Database connection string not configured for {(IsAzureDeployment ? "Azure" : "Local")} deployment");
                }

                // Validate Syncfusion license
                var syncfusionLicense = _configuration["SyncfusionLicenseKey"];
                if (string.IsNullOrEmpty(syncfusionLicense) ||
                    syncfusionLicense.Contains("${SYNCFUSION_LICENSE_KEY}"))
                {
                    validationResults.Add("Syncfusion license key not configured");
                }

                if (validationResults.Count > 0)
                {
                    Logger.Warning("Configuration validation failed with {ErrorCount} issues: {Issues}",
                        validationResults.Count, string.Join("; ", validationResults));
                    return Task.FromResult(false);
                }

                Logger.Information("Configuration validation completed successfully");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during configuration validation");
                return Task.FromResult(false);
            }
        }
    }

    public void RegisterServices(IServiceCollection services)
    {
        using (LogContext.PushProperty("Operation", "RegisterServices"))
        {
            try
            {
                // Register configuration options
                services.Configure<GoogleEarthEngineOptions>(options =>
                {
                    _configuration.GetSection(GoogleEarthEngineOptions.SectionName).Bind(options);
                });

                services.Configure<XaiOptions>(options =>
                {
                    _configuration.GetSection(XaiOptions.SectionName).Bind(options);
                });

                services.Configure<AppSettingsOptions>(options =>
                {
                    _configuration.GetSection(AppSettingsOptions.SectionName).Bind(options);
                });

                // Register HttpClient for services
                services.AddHttpClient<GoogleEarthEngineService>(client =>
                {
                    client.BaseAddress = new Uri(GoogleEarthEngineOptions.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(GoogleEarthEngineOptions.TimeoutSeconds);
                });

                services.AddHttpClient<XAIService>(client =>
                {
                    client.BaseAddress = new Uri(XaiOptions.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(XaiOptions.TimeoutSeconds);
                    if (!string.IsNullOrEmpty(XaiOptions.ApiKey) && !XaiOptions.ApiKey.StartsWith("${", StringComparison.Ordinal))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", XaiOptions.ApiKey);
                    }
                });

                // Register services
                services.AddScoped<GoogleEarthEngineService>();
                services.AddScoped<XAIService>();
                services.AddSingleton<IAzureConfigurationService>(this);

                // Register memory caching for API responses
                services.AddMemoryCache();

                Logger.Information("Azure configuration services registered successfully. Mode: {DeploymentMode}",
                    IsAzureDeployment ? "Azure" : "Local");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error registering Azure configuration services");
                throw;
            }
        }
    }
}
