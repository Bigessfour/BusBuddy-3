<# Hard Archived 2025-08-12: MinimalOutputCapture.ps1 merged into module utilities.
Archive: Documentation/Archive/LegacyScripts/MinimalOutputCapture.ps1
#>
throw "Archived: Use module utility version"
$output = $process.StandardOutput.ReadToEnd()
$stderrOutput = $process.StandardError.ReadToEnd()
$process.WaitForExit()

return @{
    Output      = $output
    ErrorOutput = $stderrOutput
    ExitCode    = $process.ExitCode
    Combined    = $output + $stderrOutput
}
}

# Simple build function with full output (using approved verb)
function Invoke-BusBuddyBuildNoTruncate {
    <#
    .SYNOPSIS
        Build with full output capture - MVP minimal approach
    #>
    Write-Host "🚌 Building with full output capture..." -ForegroundColor Cyan

    $result = Invoke-CommandWithFullOutput -Command "dotnet" -Arguments @("build", "BusBuddy.sln", "--verbosity", "normal")

    # Show errors prominently
    $errorLines = $result.Combined -split "`n" | Where-Object { $_ -match "error|CS\d+|MSB\d+" }

    if ($errorLines) {
        Write-Host "❌ ERRORS FOUND:" -ForegroundColor Red
        $errorLines | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    } else {
        Write-Host "✅ Build successful!" -ForegroundColor Green
    }

    return $result.ExitCode
}

# Simple test function with full output (using approved verb)
function Invoke-BusBuddyTestNoTruncate {
    <#
    .SYNOPSIS
        Test with full output capture - MVP minimal approach
    #>
    Write-Host "🧪 Running tests with full output capture..." -ForegroundColor Cyan

    $result = Invoke-CommandWithFullOutput -Command "dotnet" -Arguments @("test", "BusBuddy.sln", "--verbosity", "normal")

    # Show test results
    Write-Host $result.Output
    if ($result.ErrorOutput) { Write-Host $result.ErrorOutput -ForegroundColor Yellow }

    return $result.ExitCode
}

# Create aliases for bb* commands
Set-Alias -Name "bbBuildFull" -Value "Invoke-BusBuddyBuildNoTruncate" -Description "MVP build with no truncation"
Set-Alias -Name "bbTestFull" -Value "Invoke-BusBuddyTestNoTruncate" -Description "MVP test with no truncation"

Export-ModuleMember -Function Invoke-CommandWithFullOutput, Invoke-BusBuddyBuildNoTruncate, Invoke-BusBuddyTestNoTruncate -Alias "bbBuildFull", "bbTestFull"
