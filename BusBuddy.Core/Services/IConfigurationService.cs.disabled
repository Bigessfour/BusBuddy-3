using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    public interface IConfigurationService
    {
        string GetConnectionString(string name);
        T GetValue<T>(string key);
        string GetSyncfusionLicenseKey();
        Task SetValueAsync<T>(string key, T value);
        Task<bool> SaveSettingsAsync();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserSettingsService? _userSettingsService;

        public ConfigurationService(IConfiguration configuration, IUserSettingsService? userSettingsService = null)
        {
            _configuration = configuration;
            _userSettingsService = userSettingsService;
        }

        public string GetConnectionString(string name)
        {
            return _configuration.GetConnectionString(name) ?? string.Empty;
        }

        public T GetValue<T>(string key)
        {
            // First check user settings for overrides
            if (_userSettingsService != null)
            {
                try
                {
                    var userValue = _userSettingsService.GetSettingAsync<T>(key).GetAwaiter().GetResult();
                    if (userValue != null && !userValue.Equals(default(T)))
                    {
                        return userValue;
                    }
                }
                catch
                {
                    // Fall back to configuration if user settings fail
                }
            }

            return _configuration.GetValue<T>(key) ?? default(T)!;
        }

        public async Task SetValueAsync<T>(string key, T value)
        {
            if (_userSettingsService != null)
            {
                await _userSettingsService.SetSettingAsync(key, value);
            }
            // Note: Can't modify IConfiguration at runtime as it's read-only
            // User settings service handles persistence
        }

        public async Task<bool> SaveSettingsAsync()
        {
            if (_userSettingsService != null)
            {
                return await _userSettingsService.SaveSettingsAsync();
            }

            // Without user settings service, we can't persist changes
            return true; // Return true for compatibility
        }

        public string GetSyncfusionLicenseKey()
        {
            return _configuration["SyncfusionLicenseKey"] ??
                   Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ??
                   throw new InvalidOperationException("Syncfusion license key not found in configuration or environment variables");
        }
    }
}
