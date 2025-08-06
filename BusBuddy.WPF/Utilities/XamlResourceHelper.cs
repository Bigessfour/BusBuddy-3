using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Utility class for diagnosing and fixing XAML resource loading issues
    /// </summary>
    public static class XamlResourceHelper
    {
        private static ILogger? _logger;

        /// <summary>
        /// Initialize with a logger
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Verifies a XAML resource dictionary by parsing it to ensure it's valid
        /// </summary>
        /// <param name="resourceUri">The URI of the resource to verify</param>
        /// <returns>True if verification passed, false otherwise</returns>
        public static bool VerifyXamlResource(string resourceUri)
        {
            try
            {
                _logger?.Debug("[XAML] Verifying XAML resource: {Uri}", resourceUri);

                // Create a URI object from the string
                Uri uri = new Uri(resourceUri, UriKind.Absolute);

                // Try to create a ResourceDictionary from the URI
                ResourceDictionary resourceDict = new ResourceDictionary();
                resourceDict.Source = uri;

                // Log success and some basic stats
                _logger?.Information("[XAML] Successfully verified XAML resource: {Uri} with {Count} resources",
                    resourceUri, resourceDict.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "[XAML] Error verifying XAML resource {Uri}: {Message}", resourceUri, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Checks if the IOException during XAML loading is due to a file access issue
        /// </summary>
        /// <param name="assemblyName">The assembly name containing the resource</param>
        /// <param name="resourcePath">The resource path</param>
        /// <returns>A diagnostic report about resource accessibility</returns>
        public static string DiagnoseResourceAccess(string assemblyName, string resourcePath)
        {
            try
            {
                _logger?.Debug("[XAML] Diagnosing resource access for: {Assembly}/{Path}", assemblyName, resourcePath);

                // Check if the assembly is loaded
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly == null)
                {
                    _logger?.Warning("[XAML] Assembly not loaded: {Assembly}", assemblyName);
                    return $"Assembly '{assemblyName}' is not loaded in the current AppDomain.";
                }

                // Check if the resource exists
                string[] resources = assembly.GetManifestResourceNames();
                bool resourceExists = resources.Any(r => r.Contains(resourcePath.Replace('/', '.')));

                if (!resourceExists)
                {
                    _logger?.Warning("[XAML] Resource not found: {Resource} in {Assembly}", resourcePath, assemblyName);
                    return $"Resource '{resourcePath}' was not found in assembly '{assemblyName}'.";
                }

                _logger?.Information("[XAML] Resource exists: {Resource} in {Assembly}", resourcePath, assemblyName);
                return $"Resource '{resourcePath}' exists in assembly '{assemblyName}' and should be accessible.";
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "[XAML] Error diagnosing resource access: {Message}", ex.Message);
                return $"Error diagnosing resource access: {ex.Message}";
            }
        }

        /// <summary>
        /// Diagnostic method that detects circular references in resource dictionaries
        /// </summary>
        /// <returns>A report of any circular references found</returns>
        public static string DetectCircularReferences()
        {
            try
            {
                _logger?.Debug("[XAML] Detecting circular references in resource dictionaries");

                // We can't fully detect all circular references, but we can check for common patterns
                if (Application.Current == null || Application.Current.Resources.MergedDictionaries == null)
                {
                    return "No resource dictionaries found to check.";
                }

                var dictionaries = Application.Current.Resources.MergedDictionaries;
                var sources = new HashSet<Uri>();
                var potentialCircular = new List<Uri>();

                foreach (var dict in dictionaries)
                {
                    if (dict.Source != null)
                    {
                        if (!sources.Add(dict.Source))
                        {
                            potentialCircular.Add(dict.Source);
                        }

                        // Check merged dictionaries within this dictionary
                        foreach (var mergedDict in dict.MergedDictionaries)
                        {
                            if (mergedDict.Source != null && !sources.Add(mergedDict.Source))
                            {
                                potentialCircular.Add(mergedDict.Source);
                            }
                        }
                    }
                }

                if (potentialCircular.Count > 0)
                {
                    _logger?.Warning("[XAML] Detected potential circular references: {Count}", potentialCircular.Count);
                    return $"Potential circular references detected: {string.Join(", ", potentialCircular)}";
                }

                _logger?.Information("[XAML] No circular references detected");
                return "No circular references detected.";
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "[XAML] Error detecting circular references: {Message}", ex.Message);
                return $"Error detecting circular references: {ex.Message}";
            }
        }

        /// <summary>
        /// Verifies a specific style exists in the resource system
        /// </summary>
        /// <param name="styleKey">The key of the style to verify</param>
        /// <returns>True if the style exists and is valid, false otherwise</returns>
        public static bool VerifyStyleExists(string styleKey)
        {
            try
            {
                _logger?.Debug("[XAML] Verifying style exists: {StyleKey}", styleKey);

                if (Application.Current == null)
                {
                    _logger?.Warning("[XAML] Application.Current is null");
                    return false;
                }

                bool styleExists = Application.Current.Resources.Contains(styleKey);

                if (styleExists)
                {
                    // Verify it's actually a Style
                    bool isStyle = Application.Current.Resources[styleKey] is System.Windows.Style;
                    _logger?.Information("[XAML] Style {StyleKey} exists: {Exists}, IsStyle: {IsStyle}",
                        styleKey, styleExists, isStyle);
                    return isStyle;
                }

                // Check in merged dictionaries
                foreach (var dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict.Contains(styleKey))
                    {
                        bool isStyle = dict[styleKey] is System.Windows.Style;
                        _logger?.Information("[XAML] Style {StyleKey} exists in merged dictionary: {Exists}, IsStyle: {IsStyle}",
                            styleKey, true, isStyle);
                        return isStyle;
                    }

                    // Recursive check in nested dictionaries
                    foreach (var nestedDict in dict.MergedDictionaries)
                    {
                        if (nestedDict.Contains(styleKey))
                        {
                            bool isStyle = nestedDict[styleKey] is System.Windows.Style;
                            _logger?.Information("[XAML] Style {StyleKey} exists in nested dictionary: {Exists}, IsStyle: {IsStyle}",
                                styleKey, true, isStyle);
                            return isStyle;
                        }
                    }
                }

                _logger?.Warning("[XAML] Style not found: {StyleKey}", styleKey);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "[XAML] Error verifying style {StyleKey}: {Message}", styleKey, ex.Message);
                return false;
            }
        }
    }
}
