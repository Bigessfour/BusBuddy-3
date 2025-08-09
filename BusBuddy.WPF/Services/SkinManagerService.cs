using System;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.Themes.FluentDark.WPF;
using Syncfusion.Themes.FluentLight.WPF;
using Serilog;

namespace BusBuddy.WPF.Services
{
    public interface ISkinManagerService
    {
        void ApplyTheme(string themeName);
        void ApplyFluentDark();
        void ApplyFluentLight();
    void ApplyThemeToElement(FrameworkElement? element, string? themeName = null);
        string CurrentTheme { get; }
        bool IsThemeApplied { get; }
    }

    public class SkinManagerService : ISkinManagerService
    {
        private static readonly ILogger Logger = Log.ForContext<SkinManagerService>();
        private string _currentTheme = "FluentDark";
        private bool _isThemeApplied;

        public string CurrentTheme => _currentTheme;
        public bool IsThemeApplied => _isThemeApplied;

        public void ApplyTheme(string themeName)
        {
            try
            {
                Logger.Information("Applying theme {ThemeName}", themeName);

                switch (themeName?.ToLower())
                {
                    case "fluentdark":
                        ApplyFluentDark();
                        break;
                    case "fluentlight":
                    case "fluentwhite":
                        ApplyFluentLight();
                        break;
                    default:
                        Logger.Warning("Unknown theme {ThemeName}, applying FluentDark as default", themeName);
                        ApplyFluentDark();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply theme {ThemeName}, falling back to FluentLight", themeName);
                ApplyFluentLight();
            }
        }

        public void ApplyFluentDark()
        {
            try
            {
                Logger.Debug("Registering FluentDark theme settings");

                // Register Fluent Dark theme following official Syncfusion documentation
                SfSkinManager.RegisterThemeSettings("FluentDark", new FluentDarkThemeSettings());

                // Set as application-wide theme
                SfSkinManager.ApplyStylesOnApplication = true;

                // Apply to main window if available
                if (Application.Current?.MainWindow != null)
                {
                    using var theme = new Theme("FluentDark");
                    SfSkinManager.SetTheme(Application.Current.MainWindow, theme);
                    Logger.Debug("Applied FluentDark theme to MainWindow");
                }

                _currentTheme = "FluentDark";
                _isThemeApplied = true;

                Logger.Information("Successfully applied FluentDark theme");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply FluentDark theme, attempting FluentLight fallback");
                ApplyFluentLight();
            }
        }

        public void ApplyFluentLight()
        {
            try
            {
                Logger.Debug("Registering FluentLight theme settings");

                // Register Fluent Light theme following official Syncfusion documentation
                SfSkinManager.RegisterThemeSettings("FluentLight", new FluentLightThemeSettings());

                // Set as application-wide theme
                SfSkinManager.ApplyStylesOnApplication = true;

                // Apply to main window if available
                if (Application.Current?.MainWindow != null)
                {
                    using var theme = new Theme("FluentLight");
                    SfSkinManager.SetTheme(Application.Current.MainWindow, theme);
                    Logger.Debug("Applied FluentLight theme to MainWindow");
                }

                _currentTheme = "FluentLight";
                _isThemeApplied = true;

                Logger.Information("Successfully applied FluentLight theme");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply FluentLight theme, continuing with default styling");
                _currentTheme = "Default";
                _isThemeApplied = false;
            }
        }

        public void ApplyThemeToElement(FrameworkElement? element, string? themeName = null)
        {
            if (element == null)
            {
                Logger.Warning("Cannot apply theme to null element");
                return;
            }

            var themeToApply = themeName ?? _currentTheme;

            try
            {
                using var theme = new Theme(themeToApply);
                SfSkinManager.SetTheme(element, theme);

                Logger.Debug("Applied {ThemeName} theme to {ElementType}", themeToApply, element.GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to apply {ThemeName} theme to {ElementType}, trying fallback",
                    themeToApply, element.GetType().Name);

                try
                {
                    var fallbackTheme = themeToApply == "FluentDark" ? "FluentLight" : "FluentDark";
                    using var theme = new Theme(fallbackTheme);
                    SfSkinManager.SetTheme(element, theme);

                    Logger.Debug("Applied fallback {FallbackTheme} theme to {ElementType}",
                        fallbackTheme, element.GetType().Name);
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error(fallbackEx, "Failed to apply any theme to {ElementType}", element.GetType().Name);
                }
            }
        }
    }
}
