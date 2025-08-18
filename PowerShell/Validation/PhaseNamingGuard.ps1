<#
    PhaseNamingGuard.ps1
    Purpose: Fails if legacy milestone naming (previous Phase labels) re-enters active codebase.
    Safe: Excludes bin/ obj/ TestResults/ .git/ and archive/experiment paths.
#>
param(
    [string[]] $Terms = @('Phase1', 'Phase2', 'Phase3')
)

$excludePatterns = @('bin', 'obj', 'TestResults', '.git', 'Documentation\\Archive', 'experiments', 'PhaseNamingGuard.ps1')
$violations = @()

Get-ChildItem -Recurse -File -Include *.cs, *.ps1, *.md | ForEach-Object {
    $full = $_.FullName
    if ($excludePatterns | Where-Object { $full -match [regex]::Escape($_) }) { return }
    $content = Get-Content -Path $full -Raw
    foreach ($t in $Terms) {
        if ($content -match "\b$t\b") {
            $violations += [pscustomobject]@{ File = $full; Term = $t }
        }
    }
}

if ($violations.Count -gt 0) {
    $details = $violations | Format-Table -AutoSize | Out-String
    Write-Error "Legacy phase naming detected:`n$details"
    exit 1
}
else {
    Write-Output "Phase naming guard passed."
}
