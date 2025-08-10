using System;
using System.Windows;
using Serilog;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Helper utility for consistent Syncfusion theme application across all views
    /// </summary>
    public static class SyncfusionThemeManager
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(SyncfusionThemeManager));

        /// <summary>
        /// Default primary theme (FluentDark)
        /// </summary>
        public const string PRIMARY_THEME = "FluentDark";

        /// <summary>
        /// Fallback theme (FluentLight)
        /// </summary>
        public const string FALLBACK_THEME = "FluentLight";

        /// <summary>
        /// Apply theme to a view with proper error handling and fallback
        /// </summary>
        /// <param name="view">The view to apply the theme to</param>
        public static void ApplyTheme(DependencyObject view)
        {
            try
            {
                Logger.Debug("[Theme] Applying {Theme} theme to {ViewType}", PRIMARY_THEME, view.GetType().Name);

                // Use a using statement for proper disposal of the Theme object
                using (var theme = new Theme(PRIMARY_THEME))
                {
                    SfSkinManager.SetTheme(view, theme);
                    Logger.Information("Theme changed to {ThemeName} for {Component}", PRIMARY_THEME, view.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[Theme] Failed to apply {PrimaryTheme}, attempting fallback to {FallbackTheme}",
                    PRIMARY_THEME, FALLBACK_THEME);

                try
                {
                    // Try fallback theme if primary fails
                    using (var fallbackTheme = new Theme(FALLBACK_THEME))
                    {
                        SfSkinManager.SetTheme(view, fallbackTheme);
                    }
                    Logger.Information("[Theme] Successfully applied fallback theme {FallbackTheme} to {ViewType}",
                        FALLBACK_THEME, view.GetType().Name);
                }
                catch (Exception fallbackEx)
                {
                    // If both themes fail, log error but don't crash the application
                    Logger.Error(fallbackEx, "[Theme] Failed to apply both primary and fallback themes to {ViewType}",
                        view.GetType().Name);
                }
            }
        }

        /// <summary>
        /// Validates that critical theme resources are available
        /// </summary>
        public static bool ValidateThemeResources()
        {
            if (Application.Current == null)
            {

                return false;
            }


            var resources = Application.Current.Resources;
            string[] criticalResources = { "MenuItemStyle", "MenuSeparatorStyle", "ContextMenuStyle" };

            bool allResourcesAvailable = true;

            foreach (string resource in criticalResources)
            {
                if (resources[resource] == null)
                {
                    Logger.Warning("[Theme] Critical resource missing: {ResourceName}", resource);
                    allResourcesAvailable = false;
                }
            }

            return allResourcesAvailable;
        }
    }
}
