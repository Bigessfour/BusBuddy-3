$root = 'C:\Users\biges\Desktop\BusBuddy\'
$col = Join-Path $root 'logs\collected'
if (-not (Test-Path $col)) { New-Item -ItemType Directory -Path $col -Force | Out-Null }

Get-ChildItem -Path $root -Recurse -Include *.log, *.txt -File -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $rel = $_.FullName.Substring($root.Length)
        $safe = $rel -replace '[\\/:]', '_'
        $dest = Join-Path $col $safe
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item -Path $_.FullName -Destination $dest -Force -ErrorAction Stop
        Write-Output "Copied: $($_.FullName) -> $dest"
    }
    catch {
        Write-Output "Failed to copy: $($_.FullName) — $($_.Exception.Message)"
    }
}
