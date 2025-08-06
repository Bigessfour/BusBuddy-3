using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Markup;
using System.IO;
using System.Xml;
using Serilog;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Helper class to validate and analyze resource dictionaries for duplicate keys
    /// </summary>
    public static class ResourceDictionaryValidator
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ResourceDictionaryValidator));

        /// <summary>
        /// Validates a resource dictionary file for duplicate keys
        /// </summary>
        /// <param name="resourceDictionaryPath">Path to the resource dictionary file</param>
        /// <returns>A list of duplicate keys found</returns>
        public static List<string> ValidateResourceDictionary(string resourceDictionaryPath)
        {
            try
            {
                Logger.Debug("Validating resource dictionary for duplicates: {Path}", resourceDictionaryPath);

                var keysFound = new HashSet<string>();
                var duplicateKeys = new List<string>();

                // Load the XAML as XML to find all x:Key attributes
                using (var reader = XmlReader.Create(resourceDictionaryPath))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            // Check if the current element has an x:Key attribute
                            if (reader.HasAttributes)
                            {
                                while (reader.MoveToNextAttribute())
                                {
                                    if (reader.LocalName == "Key" && reader.NamespaceURI == "http://schemas.microsoft.com/winfx/2006/xaml")
                                    {
                                        string keyValue = reader.Value;

                                        // Check if this key has already been found
                                        if (keysFound.Contains(keyValue))
                                        {
                                            duplicateKeys.Add(keyValue);
                                            Logger.Warning("Duplicate key found in resource dictionary: {Key}", keyValue);
                                        }
                                        else
                                        {
                                            keysFound.Add(keyValue);
                                        }
                                    }
                                }

                                // Move back to the element
                                reader.MoveToElement();
                            }
                        }
                    }
                }

                Logger.Information("Resource dictionary validation complete: {TotalKeys} keys found, {DuplicateCount} duplicates",
                    keysFound.Count, duplicateKeys.Count);

                return duplicateKeys;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating resource dictionary: {Path}", resourceDictionaryPath);
                return new List<string> { $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Checks for duplicate keys across all loaded resource dictionaries in the application
        /// </summary>
        /// <returns>A dictionary mapping duplicate keys to the count of occurrences</returns>
        public static Dictionary<string, int> FindDuplicateKeysInApplication()
        {
            var allKeys = new Dictionary<string, int>();
            var duplicateKeys = new Dictionary<string, int>();

            try
            {
                // Process all resource dictionaries in the application
                ProcessResourceDictionary(Application.Current.Resources, allKeys);

                // Find keys with more than one occurrence
                foreach (var key in allKeys)
                {
                    if (key.Value > 1)
                    {
                        duplicateKeys.Add(key.Key, key.Value);
                    }
                }

                // Log findings
                if (duplicateKeys.Count > 0)
                {
                    Logger.Warning("Found {Count} duplicate keys in application resources", duplicateKeys.Count);
                    foreach (var duplicate in duplicateKeys)
                    {
                        Logger.Warning("Duplicate key: {Key} (occurs {Count} times)", duplicate.Key, duplicate.Value);
                    }
                }
                else
                {
                    Logger.Information("No duplicate keys found in application resources (checked {Count} keys)", allKeys.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking for duplicate keys in application resources");
            }

            return duplicateKeys;
        }

        /// <summary>
        /// Recursively processes a resource dictionary and its merged dictionaries to find all keys
        /// </summary>
        private static void ProcessResourceDictionary(ResourceDictionary resources, Dictionary<string, int> allKeys)
        {
            // Process this dictionary's keys
            foreach (var key in resources.Keys)
            {
                string keyString = key?.ToString() ?? "null";
                if (allKeys.ContainsKey(keyString))
                {
                    allKeys[keyString]++;
                }
                else
                {
                    allKeys[keyString] = 1;
                }
            }

            // Process merged dictionaries
            foreach (ResourceDictionary mergedDict in resources.MergedDictionaries)
            {
                ProcessResourceDictionary(mergedDict, allKeys);
            }
        }

        /// <summary>
        /// Fixes common resource dictionary issues by making a corrected copy
        /// </summary>
        /// <param name="sourcePath">Source resource dictionary path</param>
        /// <param name="targetPath">Target path for fixed dictionary</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool FixResourceDictionary(string sourcePath, string targetPath)
        {
            try
            {
                Logger.Information("Attempting to fix resource dictionary: {SourcePath} -> {TargetPath}", sourcePath, targetPath);

                // Load the file as XML
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(sourcePath);

                // Create a namespace manager to work with x:Key attributes
                var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                nsManager.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");

                // Find all elements with x:Key attributes
                var elementsWithKeys = xmlDoc.SelectNodes("//*[@x:Key]", nsManager);

                // Keep track of keys we've seen
                var keysFound = new HashSet<string>();
                var duplicatesFixed = 0;

                if (elementsWithKeys != null)
                {
                    // First pass: identify duplicates
                    foreach (XmlNode node in elementsWithKeys)
                    {
                        var keyAttr = node.Attributes?["Key", "http://schemas.microsoft.com/winfx/2006/xaml"];
                        if (keyAttr != null)
                        {
                            string keyValue = keyAttr.Value;

                            // Check for duplicates
                            if (keysFound.Contains(keyValue))
                            {
                                // Add a suffix to make the key unique
                                var newKeyValue = $"{keyValue}_Unique{duplicatesFixed}";
                                keyAttr.Value = newKeyValue;
                                duplicatesFixed++;

                                Logger.Information("Fixed duplicate key: {OriginalKey} -> {NewKey}", keyValue, newKeyValue);
                            }
                            else
                            {
                                keysFound.Add(keyValue);
                            }
                        }
                    }
                }

                // Save the fixed document
                xmlDoc.Save(targetPath);

                Logger.Information("Resource dictionary fixed successfully: {DuplicatesFixed} duplicates fixed", duplicatesFixed);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error fixing resource dictionary: {SourcePath}", sourcePath);
                return false;
            }
        }
    }
}
