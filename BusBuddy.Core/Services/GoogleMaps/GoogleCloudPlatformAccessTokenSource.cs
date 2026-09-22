using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Access token for Route Optimization <c>optimizeTours</c>.
/// That method requires OAuth scope <c>cloud-platform</c>, not the Maps API key.
/// </summary>
internal interface IGoogleCloudAccessTokenSource
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Refreshes gcloud application-default user credentials, then falls back to the gcloud CLI.
/// Never logs the token, client secret, or refresh token.
/// </summary>
internal sealed class GoogleCloudPlatformAccessTokenSource : IGoogleCloudAccessTokenSource
{
    internal static readonly GoogleCloudPlatformAccessTokenSource Instance = new();

    private static readonly Uri TokenEndpoint = new("https://oauth2.googleapis.com/token");
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedUntil;

    internal GoogleCloudPlatformAccessTokenSource(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _cachedUntil)
        {
            return _cachedToken;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _cachedUntil)
            {
                return _cachedToken;
            }

            var refreshed = await RefreshAuthorizedUserAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refreshed))
            {
                refreshed = await TryGcloudPrintAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(refreshed))
            {
                _cachedToken = null;
                return null;
            }

            _cachedToken = refreshed.Trim();
            if (_cachedUntil <= DateTimeOffset.UtcNow)
            {
                _cachedUntil = DateTimeOffset.UtcNow.AddMinutes(45);
            }

            return _cachedToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static IEnumerable<string> CredentialPaths()
    {
        var fromEnv = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "gcloud", "application_default_credentials.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".config", "gcloud", "application_default_credentials.json");
        }
    }

    private async Task<string?> RefreshAuthorizedUserAsync(CancellationToken cancellationToken)
    {
        foreach (var path in CredentialPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            AuthorizedUserCredential? credential;
            try
            {
                credential = ReadAuthorizedUser(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Serilog.Log.Debug(ex, "Could not read Google application-default credentials");
                continue;
            }

            if (credential is null)
            {
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = credential.ClientId,
                    ["client_secret"] = credential.ClientSecret,
                    ["refresh_token"] = credential.RefreshToken,
                }),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Serilog.Log.Warning(
                    "Google application-default token refresh failed HTTP {Status}",
                    (int)response.StatusCode);
                continue;
            }

            if (TryReadAccessToken(body, out var accessToken, out var expiresIn))
            {
                var lifetime = expiresIn > 120 ? expiresIn - 60 : expiresIn;
                _cachedUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, lifetime));
                return accessToken;
            }
        }

        return null;
    }

    internal static AuthorizedUserCredential? ReadAuthorizedUser(string path)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        if (!root.TryGetProperty("type", out var typeEl)
            || typeEl.GetString() != "authorized_user")
        {
            return null;
        }

        var clientId = ReadString(root, "client_id");
        var clientSecret = ReadString(root, "client_secret");
        var refreshToken = ReadString(root, "refresh_token");
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        return new AuthorizedUserCredential(clientId, clientSecret, refreshToken);
    }

    internal static bool TryReadAccessToken(string json, out string accessToken, out int expiresIn)
    {
        accessToken = string.Empty;
        expiresIn = 3600;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        accessToken = ReadString(root, "access_token");
        if (root.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var seconds))
        {
            expiresIn = seconds;
        }

        return !string.IsNullOrWhiteSpace(accessToken);
    }

    private static async Task<string?> TryGcloudPrintAccessTokenAsync(CancellationToken cancellationToken)
    {
        foreach (var args in new[] { "auth application-default print-access-token", "auth print-access-token" })
        {
            var token = await RunGcloudAsync(args, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token.Trim();
            }
        }

        return null;
    }

    private static async Task<string?> RunGcloudAsync(string arguments, CancellationToken cancellationToken)
    {
        var fileName = OperatingSystem.IsWindows() ? "gcloud.cmd" : "gcloud";
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            return process.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    internal sealed record AuthorizedUserCredential(string ClientId, string ClientSecret, string RefreshToken);
}
