using System.IO;
using Serilog;
using System.Text.Json;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for managing user-specific settings that persist between application sessions.
    /// </summary>
    public interface IUserSettingsService
    {
        bool EnableActivityLogging { get; }
        bool ShowDashboardOnStartup { get; }
        string CachedTheme { get; }

        Task<T> GetSettingAsync<T>(string key, T defaultValue = default!);
        Task SetSettingAsync<T>(string key, T value);
        Task<bool> SaveSettingsAsync();
        Task LoadSettingsAsync();
        Task<bool> ResetSettingsAsync();
    }

    public class UserSettingsService : IUserSettingsService
    {
        private static readonly ILogger Logger = Log.ForContext<UserSettingsService>();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _settingsFilePath;
        private Dictionary<string, object> _settings;

        public UserSettingsService() : this(null)
        {
        }

        /// <summary>Test-friendly constructor with optional settings file path override.</summary>
        public UserSettingsService(string? settingsFilePath)
        {
            if (!string.IsNullOrWhiteSpace(settingsFilePath))
            {
                _settingsFilePath = settingsFilePath;
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            else
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(appDataPath, "BusBuddy");
                Directory.CreateDirectory(appFolder);
                _settingsFilePath = Path.Combine(appFolder, "user-settings.json");
            }

            _settings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Logger.Debug("UserSettingsService initialized with settings file: {SettingsFilePath}", _settingsFilePath);
        }

        public bool EnableActivityLogging { get; private set; } = true;
        public bool ShowDashboardOnStartup { get; private set; } = true;
        public string CachedTheme { get; private set; } = "FluentDark";

        public Task<T> GetSettingAsync<T>(string key, T defaultValue = default!)
        {
            try
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (typeof(T) == typeof(string))
                        {
                            return Task.FromResult((T)(object)jsonElement.GetString()!);
                        }

                        if (typeof(T) == typeof(bool))
                        {
                            return Task.FromResult((T)(object)jsonElement.GetBoolean());
                        }

                        if (typeof(T) == typeof(int))
                        {
                            return Task.FromResult((T)(object)jsonElement.GetInt32());
                        }

                        var jsonString = jsonElement.GetRawText();
                        var deserializedValue = JsonSerializer.Deserialize<T>(jsonString);
                        return Task.FromResult(deserializedValue ?? defaultValue);
                    }

                    if (value is T directValue)
                    {
                        return Task.FromResult(directValue);
                    }

                    try
                    {
                        return Task.FromResult((T)Convert.ChangeType(value, typeof(T)));
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed to convert setting {Key} from {ValueType} to {TargetType}",
                            key, value.GetType().Name, typeof(T).Name);
                        return Task.FromResult(defaultValue);
                    }
                }

                return Task.FromResult(defaultValue);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting setting {Key}", key);
                return Task.FromResult(defaultValue);
            }
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            try
            {
                if (value != null)
                {
                    _settings[key] = value;
                    Logger.Debug("Setting {Key} updated to {Value}", key, value);
                }
                else
                {
                    _settings.Remove(key);
                    Logger.Debug("Setting {Key} removed", key);
                }

                RefreshCachedPreferences();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error setting {Key} to {Value}", key, value);
                throw;
            }
        }

        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                await File.WriteAllTextAsync(_settingsFilePath, json);

                Logger.Information(
                    "User settings saved Theme={Theme} ActivityLogging={ActivityLogging} DashboardOnStartup={DashboardOnStartup} Path={FilePath}",
                    CachedTheme,
                    EnableActivityLogging,
                    ShowDashboardOnStartup,
                    _settingsFilePath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving user settings to {FilePath}", _settingsFilePath);
                return false;
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loadedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                        if (loadedSettings != null)
                        {
                            _settings.Clear();
                            foreach (var kvp in loadedSettings)
                            {
                                _settings[kvp.Key] = kvp.Value;
                            }

                            Logger.Information(
                                "User settings loaded from {FilePath} Count={Count}",
                                _settingsFilePath,
                                _settings.Count);
                        }
                    }
                }
                else
                {
                    Logger.Information("No existing settings file found at {FilePath}. Starting with defaults.", _settingsFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading user settings from {FilePath}. Starting with defaults.", _settingsFilePath);
                _settings.Clear();
            }

            RefreshCachedPreferences();
        }

        public Task<bool> ResetSettingsAsync()
        {
            try
            {
                _settings.Clear();

                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                }

                RefreshCachedPreferences();
                Logger.Information("User settings reset successfully");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error resetting user settings");
                return Task.FromResult(false);
            }
        }

        private void RefreshCachedPreferences()
        {
            EnableActivityLogging = GetSettingAsync(UserSettingsKeys.EnableActivityLogging, true).GetAwaiter().GetResult();
            ShowDashboardOnStartup = GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, true).GetAwaiter().GetResult();
            CachedTheme = GetSettingAsync(UserSettingsKeys.Theme, "FluentDark").GetAwaiter().GetResult();
        }
    }
}
