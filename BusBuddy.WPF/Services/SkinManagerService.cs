using System;
using BusBuddy.WPF.Utilities;
using Serilog;

namespace BusBuddy.WPF.Services
{
    public interface ISkinManagerService
    {
        void ApplyTheme(string themeName);
        void ApplyFluentDark();
        void ApplyFluentLight();
        void ApplyThemeToElement(System.Windows.FrameworkElement? element, string? themeName = null);
        string CurrentTheme { get; }
        bool IsThemeApplied { get; }
    }

    public class SkinManagerService : ISkinManagerService
    {
        private static readonly ILogger Logger = Log.ForContext<SkinManagerService>();

        public string CurrentTheme => SyncfusionThemeManager.CurrentThemeName;
        public bool IsThemeApplied => !string.IsNullOrWhiteSpace(CurrentTheme);

        public void ApplyTheme(string themeName)
        {
            Logger.Information("Applying theme {ThemeName}", themeName);
            SyncfusionThemeManager.ApplyApplicationTheme(themeName);
        }

        public void ApplyFluentDark() => ApplyTheme(SyncfusionThemeManager.PRIMARY_THEME);

        public void ApplyFluentLight() => ApplyTheme(SyncfusionThemeManager.FALLBACK_THEME);

        public void ApplyThemeToElement(System.Windows.FrameworkElement? element, string? themeName = null)
        {
            if (element is null)
            {
                Logger.Warning("Cannot apply theme to null element");
                return;
            }

            if (!string.IsNullOrWhiteSpace(themeName))
            {
                SyncfusionThemeManager.ApplyApplicationTheme(themeName);
            }

            SyncfusionThemeManager.ApplyTheme(element);
        }
    }
}
