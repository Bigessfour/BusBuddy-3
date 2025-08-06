# bb-health.ps1 - BusBuddy .NET 9 migration validation

function Test-Net8References {
    $files = Get-ChildItem -Recurse -Include *.csproj,*.props,*.md,*.ps1,*.yml,*.yaml
    $net8Matches = @()
    foreach ($file in $files) {
        $content = Get-Content $file.FullName
        if ($content -match 'net8\.0') {
            $net8Matches += $file.FullName
        }
    }
    if ($net8Matches.Count -gt 0) {
        Write-Host "Found net8.0 references in:" -ForegroundColor Red
        $net8Matches | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
        exit 1
    } else {
        Write-Host "No net8.0 references found." -ForegroundColor Green
    }
}

Test-Net8References

# Monitor for new Microsoft.NET.Test.Sdk releases (for .NET 9 support)
function Check-TestSdkUpdate {
    $latest = (Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json" | ConvertFrom-Json).versions[-1]
    if ($latest -gt "17.14.1") {
        Write-Host "New Microsoft.NET.Test.Sdk ($latest) available! Supports .NET 9?" -ForegroundColor Cyan
    } else {
        Write-Host "No new Microsoft.NET.Test.Sdk beyond 17.14.1 yet." -ForegroundColor Gray
    }
}

Check-TestSdkUpdate
