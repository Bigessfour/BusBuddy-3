# Workspace-wide PowerShell parser scan (temporary utility)
# Scans all .ps1 and .psm1 files under the specified root for syntax errors using
# System.Management.Automation.Language.Parser.ParseFile.
param(
    [string]$Root = (Get-Location).Path
)

Write-Output "--- PowerShell Parser Scan ---"
Write-Output "Root: $Root"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$allFiles = Get-ChildItem -Path $Root -Recurse -File -Include *.ps1,*.psm1 -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch "\\(?:bin|obj|.git|.vs|TestResults|packages)\\"
    }

if (-not $allFiles -or $allFiles.Count -eq 0) {
    Write-Output "No PowerShell files found."
    exit 0
}

$errorsFound = @()

foreach ($file in $allFiles) {
    try {
        $tokens = $null
        $errors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
        if ($errors -and $errors.Count -gt 0) {
            foreach ($e in $errors) {
                $errorsFound += [PSCustomObject]@{
                    File   = $file.FullName
                    Line   = $e.Extent.StartLineNumber
                    Column = $e.Extent.StartColumnNumber
                    Message= $e.Message
                }
            }
        }
    }
    catch {
        $errorsFound += [PSCustomObject]@{
            File   = $file.FullName
            Line   = 0
            Column = 0
            Message= $_.Exception.Message
        }
    }
}

$stopwatch.Stop()

if ($errorsFound.Count -eq 0) {
    Write-Output ("Parser OK: 0 errors across {0} files in {1:N2}s." -f $allFiles.Count, $stopwatch.Elapsed.TotalSeconds)
    exit 0
} else {
    Write-Output ("Parser ERRORS: {0} issues across {1} files in {2:N2}s." -f $errorsFound.Count, $allFiles.Count, $stopwatch.Elapsed.TotalSeconds)
    $errorsFound | Sort-Object File, Line, Column | Format-Table -AutoSize File, Line, Column, Message | Out-String -Width 500 | Write-Output
    # Non-terminating exit to allow pipeline continuation while signaling issues
    exit 0
}
