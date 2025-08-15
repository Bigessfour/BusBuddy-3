using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

// BusBuddyValidator: Verifies presence of BusBuddy.Testing.dll in Modules folder.
// Writes a JSON error file with instructions if missing.

var modulesDir = Path.Combine(AppContext.BaseDirectory, "Modules");
var targetAssembly = "BusBuddy.Testing.dll";

var result = new ValidationResult
{
    CheckedAt = DateTime.UtcNow,
    ModulesPath = modulesDir,
    TargetAssembly = targetAssembly,
    Present = false
};

try
{
    if (!Directory.Exists(modulesDir))
    {
        result.Message = "Modules directory not found.";
        result.Severity = "Error";
        WriteResultAndExit(result, 2);
    }

    var found = Directory.EnumerateFiles(modulesDir, targetAssembly, SearchOption.AllDirectories).FirstOrDefault();
    if (found is not null)
    {
        result.Present = true;
        result.Message = "Required assembly found.";
        result.Severity = "Info";
        WriteResultAndExit(result, 0);
    }

    result.Message = $"Required assembly '{targetAssembly}' not found under '{modulesDir}'.";
    result.Severity = "Error";
    result.Action = new[] {
        "Run: dotnet restore in the solution root",
        "Ensure Test projects are built: dotnet build BusBuddy.sln",
        "See setup docs: ./SETUP-GUIDE.md"
    };

    WriteResultAndExit(result, 3);
}
catch (Exception ex)
{
    result.Message = "Unexpected error during validation: " + ex.Message;
    result.Severity = "Error";
    result.Exception = ex.ToString();
    WriteResultAndExit(result, 4);
}

static void WriteResultAndExit(ValidationResult result, int exitCode)
{
    var outPath = Path.Combine(AppContext.BaseDirectory, "busbuddy-validator-result.json");
    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(outPath, JsonSerializer.Serialize(result, options));
    Console.WriteLine($"Validation complete. Result written to: {outPath}");
    Environment.Exit(exitCode);
}

internal record ValidationResult
{
    public DateTime CheckedAt { get; init; }
    public string ModulesPath { get; init; } = string.Empty;
    public string TargetAssembly { get; init; } = string.Empty;
    public bool Present { get; init; }
    public string? Message { get; set; }
    public string? Severity { get; set; }
    public string[]? Action { get; set; }
    public string? Exception { get; set; }
}
