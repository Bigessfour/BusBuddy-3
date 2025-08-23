# Fix Write-Host violations according to BusBuddy coding standards
# Reference: Microsoft PowerShell Output Streams - https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

[CmdletBinding()]
param()

function Repair-WriteHostViolation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    Write-Information "Processing file: $FilePath" -InformationAction Continue

    try {
        $content = Get-Content -Path $FilePath -Raw -ErrorAction Stop
        $originalContent = $content

        # Replace Write-Host patterns with Write-Information for informational messages
        # Keep foreground/background color parameters for compatibility
        $content = $content -replace 'Write-Host\s+"([^"]+)"\s+-ForegroundColor\s+(\w+)', 'Write-Information "$1" -InformationAction Continue'
        $content = $content -replace 'Write-Host\s+"([^"]+)"\s+-ForegroundColor\s+(\w+)\s+-BackgroundColor\s+(\w+)', 'Write-Information "$1" -InformationAction Continue'
        $content = $content -replace 'Write-Host\s+([^-\r\n]+)-ForegroundColor\s+(\w+)', 'Write-Information $1 -InformationAction Continue'
        $content = $content -replace 'Write-Host\s+"([^"]+)"', 'Write-Information "$1" -InformationAction Continue'
        $content = $content -replace 'Write-Host\s+([^\r\n]+)', 'Write-Information $1 -InformationAction Continue'

        if ($content -ne $originalContent) {
            Set-Content -Path $FilePath -Value $content -Encoding UTF8
            Write-Information "Fixed Write-Host violations in: $FilePath" -InformationAction Continue
            return $true
        }
        else {
            Write-Information "No Write-Host violations found in: $FilePath" -InformationAction Continue
            return $false
        }
    }
    catch {
        Write-Warning "Failed to process file: $FilePath - $($_.Exception.Message)"
        return $false
    }
}

# Get all PowerShell files with Write-Host violations
$filesWithViolations = @(
    'emergency-security-audit.ps1',
    'test-grok-enhanced.ps1',
    'test-grok-integration-safe.ps1'
)

$fixedCount = 0
foreach ($file in $filesWithViolations) {
    $fullPath = Join-Path $PWD $file
    if (Test-Path $fullPath) {
        if (Repair-WriteHostViolation -FilePath $fullPath) {
            $fixedCount++
        }
    }
    else {
        Write-Warning "File not found: $fullPath"
    }
}

Write-Information "Fixed Write-Host violations in $fixedCount files" -InformationAction Continue
Write-Information "Next: Run PSScriptAnalyzer to verify fixes" -InformationAction Continue
