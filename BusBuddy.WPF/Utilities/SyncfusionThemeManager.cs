using System;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.FluentDark.WPF;
using Syncfusion.Themes.FluentLight.WPF;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Single source of truth for FluentDark / FluentLight. Views inherit
    /// <see cref="SfSkinManager.ApplicationTheme"/>; windows/dialogs take the current theme.
    /// Skin dictionaries are NOT merged in XAML — ApplicationTheme owns the skin.
    /// </summary>
    public static class SyncfusionThemeManager
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(SyncfusionThemeManager));
        private static bool _suppressPersist;

        public const string PRIMARY_THEME = "FluentDark";
        public const string FALLBACK_THEME = "FluentLight";

        public static string CurrentThemeName { get; private set; } = PRIMARY_THEME;

        public static string NormalizeThemeName(string? themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                return PRIMARY_THEME;
            }

            var key = themeName.Trim();
            if (key.Equals(FALLBACK_THEME, StringComparison.OrdinalIgnoreCase)
                || key.Equals("FluentWhite", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                return FALLBACK_THEME;
            }

            // Retired Office2019Colorful (and any other leftover name) maps to FluentDark.
            return PRIMARY_THEME;
        }

        /// <summary>
        /// Apply the active application theme to a window or dialog.
        /// UserControls inherit <see cref="SfSkinManager.ApplicationTheme"/> so docking panels stay even.
        /// </summary>
        public static void ApplyTheme(DependencyObject view)
        {
            if (view is null)
            {
                return;
            }

            try
            {
                SfSkinManager.ApplyStylesOnApplication = true;
                SfSkinManager.ApplyThemeAsDefaultStyle = true;

                if (view is not Window)
                {
                    Logger.Debug("[Theme] {ViewType} inherits application theme {Theme}", view.GetType().Name, CurrentThemeName);
                    return;
                }

                var theme = new Theme(CurrentThemeName);
                SfSkinManager.SetTheme(view, theme);
                Logger.Information("Theme changed to {ThemeName} for {Component}", CurrentThemeName, view.GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[Theme] Failed to apply {Theme} to {ViewType}, trying {Fallback}",
                    CurrentThemeName, view.GetType().Name, FALLBACK_THEME);
                try
                {
                    var fallback = new Theme(FALLBACK_THEME);
                    SfSkinManager.SetTheme(view, fallback);
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error(fallbackEx, "[Theme] Failed to apply both themes to {ViewType}", view.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Load the last saved Fluent theme (or <see cref="PRIMARY_THEME"/>) and apply it.
        /// </summary>
        public static void ApplySavedApplicationTheme()
        {
            _suppressPersist = true;
            try
            {
                var theme = ResolveSavedThemeName();
                ApplyApplicationTheme(theme);
            }
            finally
            {
                _suppressPersist = false;
            }
        }

        /// <summary>
        /// Preview a theme without persisting (Settings combo before Save).
        /// </summary>
        public static void ApplyApplicationThemePreview(string? themeName)
        {
            _suppressPersist = true;
            try
            {
                ApplyApplicationTheme(themeName);
            }
            finally
            {
                _suppressPersist = false;
            }
        }

        private static string ResolveSavedThemeName()
        {
            try
            {
                var settings = BusBuddy.WPF.App.ServiceProvider?.GetService(typeof(BusBuddy.Core.Services.IUserSettingsService))
                    as BusBuddy.Core.Services.IUserSettingsService;
                if (settings is not null)
                {
                    settings.LoadSettingsAsync().GetAwaiter().GetResult();
                    return NormalizeThemeName(settings.CachedTheme);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[Theme] Could not load theme from IUserSettingsService; falling back to ThemePreferenceStore");
            }

            return ThemePreferenceStore.Load();
        }

        /// <summary>
        /// Switch FluentDark / FluentLight for the whole app, including custom brush dictionaries.
        /// </summary>
        public static void ApplyApplicationTheme(string? themeName)
        {
            var name = NormalizeThemeName(themeName);
            try
            {
                SfSkinManager.ApplyStylesOnApplication = true;
                SfSkinManager.ApplyThemeAsDefaultStyle = true;

                if (name == FALLBACK_THEME)
                {
                    SfSkinManager.RegisterThemeSettings(FALLBACK_THEME, new FluentLightThemeSettings());
                }
                else
                {
                    SfSkinManager.RegisterThemeSettings(PRIMARY_THEME, new FluentDarkThemeSettings());
                }

                var theme = new Theme(name);
                SfSkinManager.ApplicationTheme = theme;
                CurrentThemeName = name;
                SwapThemeBrushDictionary(name);

                if (Application.Current is not null)
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        try
                        {
                            SfSkinManager.SetTheme(win, theme);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "[Theme] Could not apply {Theme} to {Window}", name, win.GetType().Name);
                        }
                    }
                }

                if (!_suppressPersist)
                {
                    PersistThemePreference(name);
                }

                Logger.Information("Theme changed to {ThemeName} at application scope", name);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[Theme] Failed to apply {Theme}, falling back to {Fallback}", name, FALLBACK_THEME);
                if (!name.Equals(FALLBACK_THEME, StringComparison.Ordinal))
                {
                    ApplyApplicationTheme(FALLBACK_THEME);
                }
            }
        }

        public static bool ValidateThemeResources()
        {
            if (Application.Current == null)
            {
                return false;
            }

            var resources = Application.Current.Resources;
            string[] criticalResources = { "MenuItemStyle", "MenuSeparatorStyle", "ContextMenuStyle" };
            var allResourcesAvailable = true;
            foreach (var resource in criticalResources)
            {
                if (resources[resource] == null)
                {
                    Logger.Warning("[Theme] Critical resource missing: {ResourceName}", resource);
                    allResourcesAvailable = false;
                }
            }

            return allResourcesAvailable;
        }

        public static string ThemeDictionaryPath(string themeName) =>
            NormalizeThemeName(themeName) == FALLBACK_THEME
                ? "Resources/Themes/FluentLightTheme.xaml"
                : "Resources/Themes/FluentDarkTheme.xaml";

        private static void PersistThemePreference(string themeName)
        {
            try
            {
                var settings = BusBuddy.WPF.App.ServiceProvider?.GetService(typeof(BusBuddy.Core.Services.IUserSettingsService))
                    as BusBuddy.Core.Services.IUserSettingsService;
                if (settings is not null)
                {
                    settings.SetSettingAsync(BusBuddy.Core.Services.UserSettingsKeys.Theme, themeName)
                        .GetAwaiter().GetResult();
                    settings.SaveSettingsAsync().GetAwaiter().GetResult();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[Theme] Could not persist theme via IUserSettingsService; using ThemePreferenceStore");
            }

            ThemePreferenceStore.Save(themeName);
        }

        private static void SwapThemeBrushDictionary(string themeName)
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries is null)
            {
                return;
            }

            var path = ThemeDictionaryPath(themeName);
            var uri = new Uri($"/BusBuddy.WPF;component/{path}", UriKind.Relative);
            ResourceDictionary? existing = null;
            foreach (var dictionary in app.Resources.MergedDictionaries)
            {
                var source = dictionary.Source?.OriginalString ?? string.Empty;
                if (source.Contains("FluentDarkTheme.xaml", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("FluentLightTheme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    existing = dictionary;
                    break;
                }
            }

            var next = new ResourceDictionary { Source = uri };
            if (existing is not null)
            {
                var index = app.Resources.MergedDictionaries.IndexOf(existing);
                app.Resources.MergedDictionaries[index] = next;
            }
            else
            {
                app.Resources.MergedDictionaries.Add(next);
            }
        }
    }
}
