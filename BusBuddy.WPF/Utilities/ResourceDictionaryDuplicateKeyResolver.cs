using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Serilog;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Handles resource dictionary duplicate key errors by allowing controlled loading of resource dictionaries
    /// with duplicate key resolution strategies.
    /// </summary>
    public static class ResourceDictionaryDuplicateKeyResolver
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ResourceDictionaryDuplicateKeyResolver));
        private static readonly HashSet<string> LoadedKeys = new HashSet<string>();
        private static readonly HashSet<string> KnownSyncfusionConflictKeys = new HashSet<string>
        {
            "SurfaceBorder",  // Known conflict with Syncfusion FluentDark theme
            "ProfessionalSurfaceBorder" // May also conflict depending on theme
        };

        /// <summary>
        /// Safely loads a resource dictionary with duplicate key handling
        /// </summary>
        /// <param name="sourcePath">The source path of the resource dictionary</param>
        /// <param name="resolveStrategy">Strategy to use when duplicate keys are found</param>
        /// <returns>The loaded ResourceDictionary or null if loading failed</returns>
        public static ResourceDictionary? SafeLoadResourceDictionary(string sourcePath, DuplicateKeyStrategy resolveStrategy = DuplicateKeyStrategy.KeepExisting)
        {
            try
            {
                // First try to load directly with XamlReader.Load for better error handling
                var uri = new Uri(sourcePath, UriKind.RelativeOrAbsolute);

                using (var stream = Application.GetResourceStream(uri)?.Stream)
                {
                    if (stream == null)
                    {
                        Logger.Error("Failed to load resource dictionary stream from {SourcePath}", sourcePath);
                        return null;
                    }

                    // Reset stream position to beginning
                    stream.Position = 0;

                    // Use XamlReader.Load instead of Parse to handle stream directly
                    if (XamlReader.Load(stream) is not ResourceDictionary resourceDict)
                    {
                        Logger.Error("Failed to parse XAML content as ResourceDictionary from {SourcePath}", sourcePath);
                        return null;
                    }

                    // Create a clean dictionary to hold the filtered resources
                    var cleanDictionary = new ResourceDictionary();

                    // Pre-check for known conflict keys and filter them out
                    var potentialConflictKeys = new List<object>();
                    foreach (var key in resourceDict.Keys)
                    {
                        var keyString = key.ToString() ?? string.Empty;

                        if (KnownSyncfusionConflictKeys.Contains(keyString))
                        {
                            Logger.Warning("Known Syncfusion conflicting key found and will be filtered: {Key} in {SourcePath}", keyString, sourcePath);
                            potentialConflictKeys.Add(key);

                            // Log recommendation to update references
                            if (keyString.Equals("SurfaceBorder", StringComparison.Ordinal))
                            {
                                Logger.Warning("Known problematic key 'SurfaceBorder' detected - this key conflicts with Syncfusion FluentDark theme");
                                Logger.Information("References to 'SurfaceBorder' should be updated to use 'SurfaceBorderBrush' instead");
                            }
                            else if (keyString.Equals("ProfessionalSurfaceBorder", StringComparison.Ordinal))
                            {
                                Logger.Warning("Known problematic key 'ProfessionalSurfaceBorder' detected - this key may conflict with Syncfusion theme");
                                Logger.Information("References to 'ProfessionalSurfaceBorder' should be updated to use 'SurfaceBorderBrush' instead");
                            }
                        }
                    }

                    // Process all keys in the loaded dictionary
                    foreach (var key in resourceDict.Keys)
                    {
                        // Skip known conflict keys
                        if (potentialConflictKeys.Contains(key))
                        {
                            continue;
                        }

                        var keyString = key.ToString() ?? string.Empty;

                        if (LoadedKeys.Contains(keyString))
                        {
                            // We found a duplicate key
                            Logger.Warning("Duplicate resource key found: {Key} in {SourcePath}", keyString, sourcePath);

                            switch (resolveStrategy)
                            {
                                case DuplicateKeyStrategy.KeepExisting:
                                    // Skip this key - don't add it to the clean dictionary
                                    Logger.Debug("Keeping existing value for duplicate key: {Key}", keyString);
                                    break;

                                case DuplicateKeyStrategy.ReplaceExisting:
                                    // Add/replace the key in our tracking and the clean dictionary
                                    cleanDictionary[key] = resourceDict[key];
                                    Logger.Debug("Replacing existing value for duplicate key: {Key}", keyString);
                                    break;

                                case DuplicateKeyStrategy.Rename:
                                    // Create a new key with a suffix
                                    var newKey = $"{keyString}_Renamed";
                                    cleanDictionary[newKey] = resourceDict[key];
                                    LoadedKeys.Add(newKey);
                                    Logger.Debug("Renamed duplicate key: {OriginalKey} to {NewKey}", keyString, newKey);
                                    break;
                            }
                        }
                        else
                        {
                            // New key - add it to our tracking and the clean dictionary
                            LoadedKeys.Add(keyString);
                            cleanDictionary[key] = resourceDict[key];
                        }
                    }

                    Logger.Information("Successfully loaded and filtered resource dictionary from {SourcePath} - removed {ConflictCount} known conflict keys",
                        sourcePath, potentialConflictKeys.Count);
                    return cleanDictionary;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading resource dictionary from {SourcePath}", sourcePath);
                return null;
            }
        }

        /// <summary>
        /// Merges a resource dictionary into the application resources with duplicate key handling
        /// </summary>
        /// <param name="sourcePath">The source path of the resource dictionary</param>
        /// <param name="resolveStrategy">Strategy to use when duplicate keys are found</param>
        public static void SafeMergeResourceDictionary(string sourcePath, DuplicateKeyStrategy resolveStrategy = DuplicateKeyStrategy.KeepExisting)
        {
            var dictionary = SafeLoadResourceDictionary(sourcePath, resolveStrategy);
            if (dictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
                Logger.Information("Successfully merged resource dictionary from {SourcePath} into application resources", sourcePath);
            }
            else
            {
                Logger.Warning("Failed to merge resource dictionary from {SourcePath} - loading returned null", sourcePath);
            }
        }

        /// <summary>
        /// Reset the loaded keys tracking - use this when completely reloading resources
        /// </summary>
        public static void ResetKeyTracking()
        {
            LoadedKeys.Clear();
            Logger.Debug("Resource dictionary key tracking has been reset");
        }
    }

    /// <summary>
    /// Strategy for handling duplicate resource dictionary keys
    /// </summary>
    public enum DuplicateKeyStrategy
    {
        /// <summary>
        /// Keep the existing key and discard the new one (default)
        /// </summary>
        KeepExisting,

        /// <summary>
        /// Replace the existing key with the new one
        /// </summary>
        ReplaceExisting,

        /// <summary>
        /// Rename the new key by adding a suffix
        /// </summary>
        Rename
    }
}
