using System.IO;
using Google.Apis.Auth.OAuth2;
using Serilog;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Bootstraps Google Cloud / Earth Engine credentials from environment or macOS Passwords.
/// Supports production via GEE_SERVICE_ACCOUNT_JSON or GOOGLE_APPLICATION_CREDENTIALS.
/// </summary>
public static class GcpCredentialBootstrap
{
    private static readonly ILogger Logger = Log.ForContext(typeof(GcpCredentialBootstrap));

    private static readonly string[] EarthEngineScopes =
    {
        "https://www.googleapis.com/auth/earthengine",
        "https://www.googleapis.com/auth/drive.readonly"
    };

    public static string GetCredentialStoreDirectory()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BusBuddy",
                "keys");
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "BusBuddy", "keys");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".busbuddy", "keys");
    }

    public static string DefaultServiceAccountKeyPath()
        => Path.Combine(GetCredentialStoreDirectory(), "bus-buddy-gee-key.json");

    /// <summary>
    /// Writes service account JSON to the secure app data key path when provided via env.
    /// Sets GOOGLE_APPLICATION_CREDENTIALS and GoogleEarthEngine__* overrides.
    /// </summary>
    public static string? MaterializeServiceAccountFromEnvironment()
    {
        var existingPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(existingPath) && File.Exists(existingPath))
        {
            ApplyConfigurationOverrides(existingPath);
            Logger.Information("Using existing GOOGLE_APPLICATION_CREDENTIALS at {Path}", existingPath);
            return existingPath;
        }

        var json = Environment.GetEnvironmentVariable("GEE_SERVICE_ACCOUNT_JSON");
        if (string.IsNullOrWhiteSpace(json))
        {
            return existingPath is { Length: > 0 } ? existingPath : null;
        }

        var targetPath = DefaultServiceAccountKeyPath();
        try
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(targetPath, json);
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                try
                {
                    File.SetUnixFileMode(targetPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch
                {
                    // Best effort on platforms that support it
                }
            }

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", targetPath);
            ApplyConfigurationOverrides(targetPath);
            Logger.Information("Materialized GEE service account key to {Path}", targetPath);
            return targetPath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to materialize GEE service account JSON to {Path}", targetPath);
            return null;
        }
    }

    public static void ApplyConfigurationOverrides(string keyPath)
    {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);
        Environment.SetEnvironmentVariable("GoogleEarthEngine__ServiceAccountKeyPath", keyPath);

        var projectId = Environment.GetEnvironmentVariable("GEE_PROJECT_ID");
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            Environment.SetEnvironmentVariable("GoogleEarthEngine__ProjectId", projectId);
        }

        var email = Environment.GetEnvironmentVariable("GEE_SERVICE_ACCOUNT_EMAIL");
        if (!string.IsNullOrWhiteSpace(email))
        {
            Environment.SetEnvironmentVariable("GoogleEarthEngine__ServiceAccountEmail", email);
        }
    }

    public static async Task<string?> TryGetEarthEngineAccessTokenAsync(string? keyPath = null)
    {
        var directToken = Environment.GetEnvironmentVariable("GEE_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(directToken) && directToken != "placeholder_token")
        {
            return directToken;
        }

        keyPath ??= Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            keyPath = MaterializeServiceAccountFromEnvironment();
        }

        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(keyPath);
            var credential = await GoogleCredential.FromStreamAsync(stream, CancellationToken.None);
            var scoped = credential.CreateScoped(EarthEngineScopes);
            return await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to obtain Earth Engine access token from {Path}", keyPath);
            return null;
        }
    }
}
