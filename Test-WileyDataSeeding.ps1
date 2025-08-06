# BusBuddy - Wiley School District Data Seeding Test Script
# Tests the new student data seeding functionality using resilient database patterns
# Author: GitHub Copilot
# Created: August 4, 2025

param(
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

# Configure error handling
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Set up logging
if ($Verbose) {
    $VerbosePreference = "Continue"
}

Write-Host "🚌 BusBuddy - Wiley School District Data Seeding Test" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray
Write-Host ""

try {
    # Validate workspace
    $workspaceRoot = Get-Location
    $projectPath = Join-Path $workspaceRoot "BusBuddy.Core"
    $dataPath = Join-Path $projectPath "Data"
    $jsonFile = Join-Path $dataPath "wiley-school-district-data.json"

    Write-Host "📍 Validating workspace structure..." -ForegroundColor Yellow

    if (!(Test-Path $projectPath)) {
        throw "BusBuddy.Core project directory not found at: $projectPath"
    }

    if (!(Test-Path $dataPath)) {
        throw "Data directory not found at: $dataPath"
    }

    if (!(Test-Path $jsonFile)) {
        throw "Wiley School District data file not found at: $jsonFile"
    }

    Write-Host "✅ Workspace structure validated" -ForegroundColor Green
    Write-Host ""

    # Display data file information
    Write-Host "📊 Data File Information:" -ForegroundColor Yellow
    $fileInfo = Get-Item $jsonFile
    Write-Host "  📄 File: $($fileInfo.Name)"
    Write-Host "  📐 Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB"
    Write-Host "  📅 Modified: $($fileInfo.LastWriteTime)"
    Write-Host ""

    # Parse and validate JSON structure
    Write-Host "🔍 Validating JSON data structure..." -ForegroundColor Yellow

    try {
        $jsonContent = Get-Content $jsonFile -Raw
        $wileyData = $jsonContent | ConvertFrom-Json

        Write-Host "✅ JSON structure valid" -ForegroundColor Green
        Write-Host "  🏫 District: $($wileyData.metadata.district)"
        Write-Host "  📍 Location: $($wileyData.metadata.location)"
        Write-Host "  📊 Families: $($wileyData.families.Count)"
        Write-Host "  👥 Students: $($wileyData.students.Count)"
        Write-Host "  🚌 Routes: $($wileyData.routes.Count)"
        Write-Host "  🛑 Bus Stops: $($wileyData.busStops.Count)"
        Write-Host ""
    }
    catch {
        throw "Failed to parse JSON data: $($_.Exception.Message)"
    }

    if ($DryRun) {
        Write-Host "🔍 DRY RUN MODE - No database operations will be performed" -ForegroundColor Magenta
        Write-Host ""

        # Analyze student data quality
        Write-Host "📈 Data Quality Analysis:" -ForegroundColor Yellow
        $allStudents = $wileyData.students
        $goodQuality = @($allStudents | Where-Object { $_.dataQuality -eq "good" -or $_.dataQuality -eq "partial" })
        $poorQuality = @($allStudents | Where-Object { $_.dataQuality -eq "poor" })

        Write-Host "  ✅ Good/Partial Quality: $($goodQuality.Length) students"
        Write-Host "  ⚠️  Poor Quality: $($poorQuality.Length) students"
        Write-Host ""

        # Show sample students
        if ($goodQuality.Length -gt 0) {
            Write-Host "👥 Sample Student Records (Good Quality):" -ForegroundColor Yellow
            $goodQuality | Select-Object -First 3 | ForEach-Object {
                $gradeTxt = if ($_.grade) { $_.grade } else { "Unknown" }
                $cityTxt = if ($_.city) { $_.city } else { "Unknown" }
                $stateTxt = if ($_.state) { $_.state } else { "Unknown" }
                Write-Host "  • $($_.studentName) (Grade: $gradeTxt) - $cityTxt, $stateTxt"
            }
            Write-Host ""
        }

        if ($poorQuality.Length -gt 0) {
            Write-Host "⚠️  Sample Student Records (Poor Quality):" -ForegroundColor Yellow
            $poorQuality | Select-Object -First 2 | ForEach-Object {
                Write-Host "  • $($_.studentName) - Data quality: $($_.dataQuality)"
            }
            Write-Host ""
        }        return
    }

    # Build the project to ensure latest changes
    Write-Host "🏗️  Building BusBuddy.Core project..." -ForegroundColor Yellow

    $buildResult = dotnet build $projectPath --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host "✅ Build successful" -ForegroundColor Green
    Write-Host ""

    # Test database connection
    Write-Host "🔌 Testing database connection..." -ForegroundColor Yellow

    # Create a simple test program to verify seeding
    $testCode = @"
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/seed-test-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IBusBuddyDbContextFactory, BusBuddyDbContextFactory>();
                    services.AddSingleton<SeedDataService>();
                })
                .Build();

            var seedService = host.Services.GetRequiredService<SeedDataService>();

            Console.WriteLine("🚌 Starting Wiley School District data seeding...");

            var result = await ResilientDbExecution.ExecuteWithResilienceAsync(
                async () => await seedService.SeedWileySchoolDistrictDataAsync(),
                "SeedWileySchoolDistrictData",
                maxRetries: 2
            );

            if (result.Success)
            {
                Console.WriteLine($"✅ Seeding completed successfully!");
                Console.WriteLine($"   📊 Students seeded: {result.StudentsSeeded}");
                Console.WriteLine($"   🏠 Families processed: {result.FamiliesProcessed}");
                Console.WriteLine($"   ⏱️  Duration: {result.Duration.TotalSeconds:F2} seconds");
                return 0;
            }
            else
            {
                Console.WriteLine($"❌ Seeding failed: {result.ErrorMessage}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Unexpected error: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
"@

    # Create temporary test program
    $tempDir = Join-Path $env:TEMP "BusBuddySeeding"
    if (!(Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir | Out-Null
    }

    $tempCsFile = Join-Path $tempDir "SeedingTest.cs"
    $testCode | Out-File -FilePath $tempCsFile -Encoding UTF8

    # Create project file for the test
    $testProjectContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$projectPath\BusBuddy.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="4.1.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>
</Project>
"@

    $testProjectFile = Join-Path $tempDir "SeedingTest.csproj"
    $testProjectContent | Out-File -FilePath $testProjectFile -Encoding UTF8

    Write-Host "🧪 Running seeding test..." -ForegroundColor Yellow

    # Run the seeding test
    Push-Location $tempDir
    try {
        $testResult = dotnet run --project $testProjectFile 2>&1
        $exitCode = $LASTEXITCODE

        Write-Host $testResult

        if ($exitCode -eq 0) {
            Write-Host ""
            Write-Host "🎉 Data seeding completed successfully!" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "❌ Data seeding failed with exit code: $exitCode" -ForegroundColor Red
        }
    }
    finally {
        Pop-Location
        # Clean up temporary files
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the seeded data in the database"
    Write-Host "  2. Manually verify student records for accuracy"
    Write-Host "  3. Update any garbled names from the original forms"
    Write-Host "  4. Assign students to appropriate routes"
    Write-Host "  5. Test route optimization with the new data"
    Write-Host ""

}
catch {
    Write-Host ""
    Write-Host "💥 Error occurred: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor DarkRed
    exit 1
}
finally {
    Write-Host ""
    Write-Host "🚌 BusBuddy data seeding test completed" -ForegroundColor Cyan
    Write-Host "=" * 60 -ForegroundColor Gray
}
