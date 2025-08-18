<#
Runs formatting for YAML and PowerShell files in the repository.

Strategy:
- For YAML: prefer using Prettier (via npx/prettier). If unavailable, prints instructions.
- For PS1: use Invoke-Formatter (from PSScriptAnalyzer). If unavailable, prints instructions.

This script is safe to run locally or from CI. It will return non-zero only on unexpected failures.
#>

param(
    [switch]$CommitChanges
)

Set-StrictMode -Version Latest
Push-Location (Split-Path -Path $MyInvocation.MyCommand.Definition -Parent) | Out-Null
Push-Location .. | Out-Null

try {
    Write-Host "[format-code] Searching for YAML files..."
    $yamlFiles = Get-ChildItem -Path . -Recurse -Include *.yml,*.yaml -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName

    if ($yamlFiles) {
        Write-Host "[format-code] Found $($yamlFiles.Count) YAML files." -ForegroundColor Cyan

        # Prefer npx prettier if available
        $prettierCmd = $null
        if (Get-Command npx -ErrorAction SilentlyContinue) {
            $prettierCmd = 'npx prettier --write'
        } elseif (Get-Command prettier -ErrorAction SilentlyContinue) {
            $prettierCmd = 'prettier --write'
        }

        if ($prettierCmd) {
            Write-Host "[format-code] Running Prettier to format YAML files..."
            & pwsh -NoProfile -Command {
                param($cmd, $files)
                $filesArg = $files -join ' '
                Write-Host "[format-code] Executing: $cmd $filesArg"
                Invoke-Expression "$cmd $filesArg"
            } -ArgumentList $prettierCmd, $yamlFiles
        } else {
            Write-Warning "Prettier not found. Install it (npm i -g prettier) or ensure 'npx' is available to format YAML files."
        }
    } else {
        Write-Host "[format-code] No YAML files found."
    }

    Write-Host "[format-code] Searching for PowerShell files..."
    $psFiles = git ls-files '*.ps1' -split "`n" | Where-Object { $_ -ne '' }

    if ($psFiles.Count -gt 0) {
        Write-Host "[format-code] Found $($psFiles.Count) PowerShell files." -ForegroundColor Cyan

        if (Get-Command Invoke-Formatter -ErrorAction SilentlyContinue) {
            Write-Host "[format-code] Running Invoke-Formatter on PowerShell files..."
            foreach ($f in $psFiles) {
                try {
                    Invoke-Formatter -FilePath $f -Confirm:$false -ErrorAction Stop
                } catch {
                    Write-Warning "Failed to format $f with Invoke-Formatter: $_"
                }
            }
        } else {
            Write-Warning "Invoke-Formatter (PSScriptAnalyzer) not available. Install via: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force"
        }
    } else {
        Write-Host "[format-code] No PowerShell files found."
    }

    if ($CommitChanges) {
        # Commit any changes and push back to current branch (used in CI)
        $status = git status --porcelain
        if ($status) {
            Write-Host "[format-code] Changes detected; committing fixes..."
            git config user.name "github-actions[bot]"
            git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
            git add -A
            git commit -m "chore: auto-format YAML and PowerShell files"
            # Push to current branch
            $branch = git rev-parse --abbrev-ref HEAD
            git push origin "$branch"
        } else {
            Write-Host "[format-code] No formatting changes to commit."
        }
    }

    Write-Host "[format-code] Formatting complete."
    exit 0
}
catch {
    Write-Error "[format-code] Unexpected error: $_"
    exit 2
}
finally {
    Pop-Location; Pop-Location
}
