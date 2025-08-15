# bb-health.ps1 — BusBuddy environment validation (PowerShell 7.5.2 compliant)

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [switch]$CheckNuGet,
    [switch]$Quiet
)

function Test-Net8References {
    [CmdletBinding()]
    param()
    try {
        $files = Get-ChildItem -Recurse -Include *.csproj,*.props,*.md,*.ps1,*.yml,*.yaml -ErrorAction Stop
        $net8Matches = @()
        foreach ($file in $files) {
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content -match 'net8\.0') { $net8Matches += $file.FullName }
        }
        if ($net8Matches.Count -gt 0) {
            Write-Warning "Found net8.0 references in:"
            $net8Matches | ForEach-Object { Write-Information $_ -InformationAction Continue }
            return 1
        } else {
            Write-Information "No net8.0 references found." -InformationAction Continue
            return 0
        }
    } catch {
        Write-Error ("bb-health: Failed scanning for net8.0 references — {0}" -f $_.Exception.Message)
        return 2
    }
}

function Check-TestSdkUpdate {
    <#
    .SYNOPSIS
        Monitor for new Microsoft.NET.Test.Sdk releases (for .NET 9 support)
    .LINK
        https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/invoke-webrequest
    #>
    [CmdletBinding()]
    param()
    try {
        $uri = "https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json"
        $latest = (Invoke-WebRequest -Uri $uri -UseBasicParsing | ConvertFrom-Json).versions[-1]
        if ($latest -gt "17.14.1") {
            Write-Information "New Microsoft.NET.Test.Sdk ($latest) available! Supports .NET 9?" -InformationAction Continue
        } else {
            if (-not $Quiet) { Write-Information "No new Microsoft.NET.Test.Sdk beyond 17.14.1 yet." -InformationAction Continue }
        }
    } catch {
        Write-Warning ("bb-health: Could not query NuGet for Test SDK — {0}" -f $_.Exception.Message)
    }
}

# Execute checks
$status = Test-Net8References
if ($PSCmdlet.ShouldProcess('Environment','Check Test SDK')) { if ($CheckNuGet) { Check-TestSdkUpdate } }

# Return non-zero for callers that care, but avoid terminating the session here
if ($status -ne 0) { Write-Verbose ("bb-health completed with status {0}" -f $status) }
