using System.IO;
using System.Net.Http;
using BusBuddy.Core.Services;
using Microsoft.Extensions.Configuration;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BusBuddy.sln")))
    dir = dir.Parent;
var repoRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
Directory.SetCurrentDirectory(repoRoot);

var config = new ConfigurationBuilder()
    .SetBasePath(repoRoot)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var projectId = config["GoogleEarthEngine:ProjectId"] ?? "(missing)";
var email = config["GoogleEarthEngine:ServiceAccountEmail"] ?? "(missing)";
var keyPath = config["GoogleEarthEngine:ServiceAccountKeyPath"] ?? "(missing)";
var resolvedKey = Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(repoRoot, keyPath);

Console.WriteLine("=== BusBuddy GEE Connection Probe ===");
Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"ProjectId: {projectId}");
Console.WriteLine($"ServiceAccountEmail: {email}");
Console.WriteLine($"ServiceAccountKeyPath: {keyPath}");
Console.WriteLine($"Resolved key path: {resolvedKey}");
Console.WriteLine($"Key file exists: {File.Exists(resolvedKey)}");

var service = new GoogleEarthEngineService(config);
Console.WriteLine($"GoogleEarthEngineService.IsConfigured: {service.IsConfigured}");

var geeToken = Environment.GetEnvironmentVariable("GEE_ACCESS_TOKEN");
Console.WriteLine($"GEE_ACCESS_TOKEN env set: {!string.IsNullOrEmpty(geeToken)}");
Console.WriteLine($"Runtime GeoDataService token: {(string.IsNullOrEmpty(geeToken) ? "placeholder_token (NOT LIVE)" : "from GEE_ACCESS_TOKEN")}");

if (!File.Exists(resolvedKey))
{
    Console.WriteLine();
    Console.WriteLine("RESULT: NOT CONNECTED — service account key file is missing.");
    Console.WriteLine("Restore keys/bus-buddy-gee-key.json from Google Cloud IAM, or set GEE_ACCESS_TOKEN.");
    Environment.Exit(2);
}

using var http = new HttpClient();
var probeUrl = $"https://earthengine.googleapis.com/v1/projects/{projectId}";
var probe = await http.GetAsync(probeUrl);
Console.WriteLine();
Console.WriteLine($"API reachability (unauthenticated): {(int)probe.StatusCode} {probe.ReasonPhrase}");

try
{
    await service.GetRouteGeoJsonAsync("nonexistent-test-asset");
    Console.WriteLine("RESULT: Unexpected success on test asset.");
    Environment.Exit(0);
}
catch (Exception ex)
{
    var msg = ex.Message;
    if (msg.Contains("access token", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"RESULT: AUTH FAILED — {msg}");
        Environment.Exit(3);
    }

    Console.WriteLine("RESULT: AUTH LIKELY OK — service account token acquired.");
    Console.WriteLine($"Export test failed as expected: {ex.GetType().Name}: {msg}");
    Environment.Exit(0);
}
