# Helper to verify FETCHABILITY-INDEX-COMPLETE.json
$p = Join-Path -Path $PSScriptRoot -ChildPath '..\FETCHABILITY-INDEX-COMPLETE.json' | Resolve-Path -ErrorAction SilentlyContinue
if (-not $p) { $p = 'c:\Users\biges\Desktop\BusBuddy\FETCHABILITY-INDEX-COMPLETE.json' }
if (Test-Path $p) {
    Write-Output "✅ File exists: $p"
    Get-Item $p | Select-Object FullName, Length | Format-List
    Write-Output "`n--- Preview (first 40 lines) ---"
    Get-Content -Path $p -TotalCount 40
    Write-Output "`n--- meta.total_files ---"
    try {
        $json = Get-Content -Raw -Path $p | ConvertFrom-Json -ErrorAction Stop
        Write-Output $json.meta.total_files
    } catch {
        Write-Output "❌ Failed to parse JSON: $($_.Exception.Message)"
    }
} else {
    Write-Error "❌ File not found: $p"
    exit 1
}
