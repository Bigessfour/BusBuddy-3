using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Persists the Fluent theme selection in the same AppData file as
    /// <c>UserSettingsService</c> so MainWindow and Settings stay in sync.
    /// </summary>
    public static class ThemePreferenceStore
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ThemePreferenceStore));

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public const string ThemeKey = "Theme";

        /// <summary>
        /// Override in tests. Defaults to %AppData%/BusBuddy/user-settings.json.
        /// </summary>
        public static string SettingsFilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BusBuddy",
            "user-settings.json");

        public static string Load(string fallback = SyncfusionThemeManager.PRIMARY_THEME)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return SyncfusionThemeManager.NormalizeThemeName(fallback);
                }

                var json = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return SyncfusionThemeManager.NormalizeThemeName(fallback);
                }

                using var doc = JsonDocument.Parse(json);
                if (TryReadTheme(doc.RootElement, out var saved))
                {
                    return SyncfusionThemeManager.NormalizeThemeName(saved);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not load persisted theme from {Path}", SettingsFilePath);
            }

            return SyncfusionThemeManager.NormalizeThemeName(fallback);
        }

        public static void Save(string themeName)
        {
            var name = SyncfusionThemeManager.NormalizeThemeName(themeName);
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Dictionary<string, JsonElement> data = new(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(SettingsFilePath))
                {
                    var existing = File.ReadAllText(SettingsFilePath);
                    if (!string.IsNullOrWhiteSpace(existing))
                    {
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existing);
                        if (loaded is not null)
                        {
                            foreach (var pair in loaded)
                            {
                                data[pair.Key] = pair.Value;
                            }
                        }
                    }
                }

                using var themeDoc = JsonDocument.Parse($"\"{name}\"");
                data[ThemeKey] = themeDoc.RootElement.Clone();

                var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in data)
                {
                    payload[pair.Key] = JsonSerializer.Deserialize<object>(pair.Value.GetRawText());
                }

                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(payload, JsonOptions));
                Logger.Debug("Persisted theme {Theme} to {Path}", name, SettingsFilePath);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not persist theme {Theme} to {Path}", name, SettingsFilePath);
            }
        }

        private static bool TryReadTheme(JsonElement root, out string? theme)
        {
            theme = null;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(ThemeKey, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    theme = property.Value.GetString();
                    return !string.IsNullOrWhiteSpace(theme);
                }
            }

            return false;
        }
    }
}
