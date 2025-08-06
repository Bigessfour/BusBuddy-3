using System;
using System.IO;
using Serilog;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Helper class for safe file operations with comprehensive error handling
    /// </summary>
    public static class FileAccessHelper
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(FileAccessHelper));

        /// <summary>
        /// Safely checks if a file exists without throwing exceptions
        /// </summary>
        public static bool SafeFileExists(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error checking file existence: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Safely gets the full path without throwing exceptions
        /// </summary>
        public static string? SafeGetFullPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error getting full path: {Path}", path);
                return path;
            }
        }

        /// <summary>
        /// Safely creates a directory if it doesn't exist
        /// </summary>
        public static bool SafeCreateDirectory(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Logger.Information("Created directory: {Path}", directoryPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create directory: {Path}", directoryPath);
                return false;
            }
        }

        /// <summary>
        /// Safely reads all text from a file with fallback
        /// </summary>
        public static string SafeReadAllText(string filePath, string fallbackContent = "")
        {
            try
            {
                if (SafeFileExists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                else
                {
                    Logger.Warning("File not found, using fallback content: {FilePath}", filePath);
                    return fallbackContent;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error reading file, using fallback: {FilePath}", filePath);
                return fallbackContent;
            }
        }
    }
}
