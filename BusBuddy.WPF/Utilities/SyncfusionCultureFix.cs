using System;
using System.Globalization;
using System.Threading;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Provides culture-related fixes for Syncfusion controls
    /// </summary>
    public static class SyncfusionCultureFix
    {
        /// <summary>
        /// Applies culture fixes that prevent XAML parsing errors with Syncfusion controls
        /// </summary>
        public static void ApplyCultureFixes()
        {
            try
            {
                // Set invariant culture to prevent XAML parsing issues with star (*) characters
                var invariantCulture = CultureInfo.InvariantCulture;

                // Apply to current thread
                Thread.CurrentThread.CurrentCulture = invariantCulture;
                Thread.CurrentThread.CurrentUICulture = invariantCulture;

                // Apply as default for new threads
                CultureInfo.DefaultThreadCurrentCulture = invariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = invariantCulture;

                // Force the application to use invariant culture for all numeric parsing
                System.Windows.FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(System.Windows.FrameworkElement),
                    new System.Windows.FrameworkPropertyMetadata(
                        System.Windows.Markup.XmlLanguage.GetLanguage(invariantCulture.IetfLanguageTag)));

                System.Diagnostics.Debug.WriteLine("SyncfusionCultureFix: Applied invariant culture fixes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncfusionCultureFix: Error applying fixes: {ex.Message}");
            }
        }
    }
}
