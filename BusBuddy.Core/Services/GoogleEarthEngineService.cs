using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Google Earth Engine Integration Service
    /// Provides satellite imagery, terrain analysis, and route optimization
    /// using Google Earth Engine private license capabilities
    /// </summary>
    public class GoogleEarthEngineService
    {
        private static readonly ILogger Logger = Log.ForContext<GoogleEarthEngineService>();
        private readonly IConfiguration _configuration;
        private readonly string _projectId;
        private readonly string _serviceAccountEmail;
        private readonly string _serviceAccountKeyPath;
        private readonly bool _isConfigured;

        /// <summary>
        /// Required configuration keys:
        ///   GoogleEarthEngine:ProjectId
        ///   GoogleEarthEngine:ServiceAccountEmail
        ///   GoogleEarthEngine:ServiceAccountKeyPath
        /// Service account must have Earth Engine and Drive read/write permissions.
        /// </summary>
        public GoogleEarthEngineService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Load configuration
            _projectId = _configuration["GoogleEarthEngine:ProjectId"] ?? string.Empty;
            _serviceAccountEmail = _configuration["GoogleEarthEngine:ServiceAccountEmail"] ?? string.Empty;
            _serviceAccountKeyPath = _configuration["GoogleEarthEngine:ServiceAccountKeyPath"] ?? string.Empty;

            _isConfigured = !string.IsNullOrEmpty(_projectId) &&
                           !string.IsNullOrEmpty(_serviceAccountEmail) &&
                           !_projectId.Contains("YOUR_PROJECT_ID_HERE", StringComparison.OrdinalIgnoreCase);

            if (!_isConfigured)
            {
                Logger.Warning("Google Earth Engine not configured. Using mock data. Please update appsettings.json with your GEE credentials.");
            }
            else
            {
                Logger.Information($"Google Earth Engine configured for project: {_projectId}");
                Logger.Information($"Google Earth Engine configured for project: {_projectId}");
            }
        }

        public bool IsConfigured => _isConfigured;

        /// <summary>
        /// Retrieves GeoJSON route data from Google Earth Engine for a given region or asset using the official export workflow.
        /// </summary>
        public async Task<string> GetRouteGeoJsonAsync(string assetIdOrRegion)
        {
            if (!_isConfigured)
            {
                Logger.Warning("GEE not configured. Returning mock GeoJSON.");
                return "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[-98.35,39.5],[-99.35,40.5],[-97.35,41.5]]},\"properties\":{}}]}";
            }

            // 1. Authenticate and get access token (service account or OAuth2)
            string accessToken = await GetGoogleAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("Failed to obtain Google access token.");
            }

            // 2. Start export task to Google Drive (exportTable)
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var exportRequest = new
            {
                description = "BusBuddyRouteExport",
                driveDestination = new { folder = "BusBuddyExports", fileNamePrefix = "routes" },
                collection = $"projects/{_projectId}/assets/{assetIdOrRegion}"
            };
            var exportUrl = $"https://earthengine.googleapis.com/v1/projects/{_projectId}/table:export";
            var exportResponse = await httpClient.PostAsJsonAsync(exportUrl, exportRequest);
            exportResponse.EnsureSuccessStatusCode();
            var exportResultJson = await exportResponse.Content.ReadAsStringAsync();

            // Parse exportResult to get the task name (ID)
            using var exportResult = JsonDocument.Parse(exportResultJson);
            if (!exportResult.RootElement.TryGetProperty("name", out var taskNameElement))
            {
                throw new InvalidOperationException("Export response missing task name.");
            }

            string taskName = taskNameElement.GetString() ?? throw new InvalidOperationException("Task name is null.");

            // 3. Poll for export task completion
            // Configurable polling parameters
            int maxAttempts = _configuration.GetValue<int?>("GoogleEarthEngine:ExportPollingMaxAttempts") ?? 30;
            int initialDelayMs = _configuration.GetValue<int?>("GoogleEarthEngine:ExportPollingInitialDelayMs") ?? 4000;

            string driveFileId = await PollForExportAndGetDriveFileIdAsync(taskName, accessToken, maxAttempts, initialDelayMs);
            if (string.IsNullOrEmpty(driveFileId))
            {
                Logger.Error("Export did not complete or file ID not found. Task: {TaskName}", taskName);
                throw new InvalidOperationException("Export did not complete or file ID not found.");
            }

            // 4. Download the resulting GeoJSON from Google Drive
            string geoJson = await DownloadGeoJsonFromDriveAsync(driveFileId, accessToken);

            // 5. Clean up: Delete file from Drive after download
            bool deleted = await TryDeleteDriveFileAsync(driveFileId, accessToken);
            if (!deleted)
            {
                Logger.Warning("Failed to delete exported file from Drive: {FileId}", driveFileId);
            }

            return geoJson;
        }

        /// <summary>
        /// Polls the GEE export task until completion and returns the resulting Drive file ID.
        /// </summary>
        private static async Task<string> PollForExportAndGetDriveFileIdAsync(string taskName, string accessToken, int maxAttempts, int initialDelayMs)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var statusUrl = $"https://earthengine.googleapis.com/v1/{taskName}";
            int delayMs = initialDelayMs;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var statusResponse = await httpClient.GetAsync(statusUrl);
                    if (!statusResponse.IsSuccessStatusCode)
                    {
                        Logger.Warning("Polling attempt {Attempt}: Non-success status {StatusCode}", attempt, statusResponse.StatusCode);
                        if ((int)statusResponse.StatusCode == 429 || (int)statusResponse.StatusCode == 503)
                        {
                            // Quota or service unavailable, exponential backoff
                            await Task.Delay(delayMs);
                            delayMs = Math.Min(delayMs * 2, 60000); // Cap at 60s
                            continue;
                        }
                        statusResponse.EnsureSuccessStatusCode();
                    }
                    var statusJson = await statusResponse.Content.ReadAsStringAsync();
                    var statusDoc = JsonDocument.Parse(statusJson);
                    if (statusDoc.RootElement.TryGetProperty("state", out var stateElem))
                    {
                        string state = stateElem.GetString() ?? "";
                        if (state == "SUCCEEDED")
                        {
                            // Get Drive file ID
                            if (statusDoc.RootElement.TryGetProperty("driveDestination", out var driveElem) &&
                                driveElem.TryGetProperty("fileId", out var fileIdElem))
                            {
                                return fileIdElem.GetString() ?? string.Empty;
                            }
                            Logger.Error("Export succeeded but fileId not found in response.");
                            return string.Empty;
                        }
                        else if (state == "FAILED" || state == "CANCELLED")
                        {
                            Logger.Error("GEE export failed or cancelled: {State}. Task: {TaskName}", state, taskName);
                            return string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Exception during export polling attempt {Attempt}", attempt);
                }
                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 60000); // Exponential backoff, cap at 60s
            }
            Logger.Error("GEE export polling timed out. Task: {TaskName}", taskName);
            return string.Empty;
        }
        /// <summary>
        /// Attempts to delete a file from Google Drive after download to avoid clutter/quota issues.
        /// </summary>
        private static async Task<bool> TryDeleteDriveFileAsync(string fileId, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var deleteUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}";
                var response = await httpClient.DeleteAsync(deleteUrl);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                Logger.Warning("Drive file delete returned status {StatusCode} for file {FileId}", response.StatusCode, fileId);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception deleting Drive file {FileId}", fileId);
                return false;
            }
        }

        /// <summary>
        /// Downloads the exported GeoJSON file from Google Drive using the Drive API.
        /// </summary>
        private static async Task<string> DownloadGeoJsonFromDriveAsync(string fileId, string accessToken)
        {
            try
            {
                using (var driveService = new Google.Apis.Drive.v3.DriveService(new Google.Apis.Services.BaseClientService.Initializer
                {
                    HttpClientInitializer = null, // We use raw HTTP for this download
                    ApplicationName = "BusBuddy"
                }))
                {
                    // Use HttpClient with Bearer token for direct download
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    var downloadUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                    var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    var geoJson = await response.Content.ReadAsStringAsync();
                    return geoJson;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to download GeoJSON from Google Drive.");
                return string.Empty;
            }
        }

        /// <summary>
        /// Authenticates using a Google service account key and returns an OAuth2 access token for Earth Engine and Drive APIs.
        /// </summary>
        private async Task<string> GetGoogleAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_serviceAccountKeyPath) || !System.IO.File.Exists(_serviceAccountKeyPath))
            {
                Logger.Error($"Service account key file not found: {_serviceAccountKeyPath}");
                return string.Empty;
            }

            try
            {
                // Scopes required for Earth Engine and Drive export
                var scopes = new[] {
                    "https://www.googleapis.com/auth/earthengine",
                    "https://www.googleapis.com/auth/drive.readonly"
                };

                using var stream = System.IO.File.OpenRead(_serviceAccountKeyPath);
                var credential = await Google.Apis.Auth.OAuth2.GoogleCredential.FromStreamAsync(stream, System.Threading.CancellationToken.None);
                var scoped = credential.CreateScoped(scopes);
                var token = await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
                return token;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to obtain Google access token from service account.");
                return string.Empty;
            }
        }
    }
}

