#requires -Version 7.0
<#
.SYNOPSIS
    Analyze remaining Write-Host violations in PowerShell modules for manual refactoring guidance
.DESCRIPTION
    This script identifies the specific types and patterns of remaining Write-Host violations
    that require manual attention after automated refactoring.
.EXAMPLE
    .\Analyze-RemainingViolations.ps1 -ModulePath "PowerShell\Modules\BusBuddy\BusBuddy.psm1"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path $_ })]
    [string]$ModulePath
)

function Write-AnalysisResult {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Finding', 'Recommendation', 'Summary')]
        [string]$Type = 'Info'
    )

    $prefix = switch ($Type) {
        'Info'           { "🔍" }
        'Finding'        { "📋" }
        'Recommendation' { "💡" }
        'Summary'        { "📊" }
    }

    Write-Information "$prefix $Message" -InformationAction Continue
}

try {
    Write-AnalysisResult "Analyzing remaining Write-Host violations in: $ModulePath" -Type Info

    # Read module content
    $content = Get-Content $ModulePath -Raw

    # Find all remaining Write-Host patterns
    $writeHostMatches = [regex]::Matches($content, 'Write-Host\s+[^`r`n]+', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if ($writeHostMatches.Count -eq 0) {
        Write-AnalysisResult "✅ No Write-Host violations found!" -Type Summary
        return
    }

    Write-AnalysisResult "Found $($writeHostMatches.Count) remaining Write-Host violations" -Type Summary

    # Categorize violations
    $categories = @{
        ComplexExpressions = @()
        VariableOutput = @()
        ConditionalOutput = @()
        LoopOutput = @()
        MultilineOutput = @()
        UnknownPatterns = @()
    }

    $lineNumber = 1
    $lines = $content -split "`r?`n"

    foreach ($line in $lines) {
        if ($line -match 'Write-Host') {
            $violation = @{
                LineNumber = $lineNumber
                Content = $line.Trim()
                Context = ""
            }

            # Add context from surrounding lines
            $startContext = [Math]::Max(0, $lineNumber - 3)
            $endContext = [Math]::Min($lines.Count - 1, $lineNumber + 1)
            $violation.Context = ($lines[$startContext..$endContext] | ForEach-Object { "    $_" }) -join "`n"

            # Categorize the violation
            switch -Regex ($line) {
                'Write-Host\s+\$[^-\s]*\s*$' {
                    $categories.VariableOutput += $violation
                }
                'Write-Host.*\$\(' {
                    $categories.ComplexExpressions += $violation
                }
                'if.*Write-Host|Write-Host.*if' {
                    $categories.ConditionalOutput += $violation
                }
                'foreach.*Write-Host|Write-Host.*foreach' {
                    $categories.LoopOutput += $violation
                }
                'Write-Host.*\\n|Write-Host.*`n' {
                    $categories.MultilineOutput += $violation
                }
                default {
                    $categories.UnknownPatterns += $violation
                }
            }
        }
        $lineNumber++
    }

    # Report findings by category
    foreach ($category in $categories.Keys) {
        $violations = $categories[$category]
        if ($violations.Count -gt 0) {
            Write-AnalysisResult "Category: $category ($($violations.Count) violations)" -Type Finding

            $recommendations = switch ($category) {
                'ComplexExpressions' {
                    "Replace with: Write-Information `"expression result`" -InformationAction Continue"
                }
                'VariableOutput' {
                    "Replace with: Write-Output `$variable or Write-Information `$variable -InformationAction Continue"
                }
                'ConditionalOutput' {
                    "Move Write-Host inside proper if/else blocks, then apply stream replacements"
                }
                'LoopOutput' {
                    "Consider using Write-Progress for status updates in loops"
                }
                'MultilineOutput' {
                    "Split into multiple Write-Information calls or use here-strings"
                }
                'UnknownPatterns' {
                    "Manual review required - may need custom refactoring approach"
                }
            }

            Write-AnalysisResult "Recommendation: $recommendations" -Type Recommendation

            # Show first few examples
            $examples = $violations | Select-Object -First 3
            foreach ($example in $examples) {
                Write-AnalysisResult "  Line $($example.LineNumber): $($example.Content)" -Type Finding
            }

            if ($violations.Count -gt 3) {
                Write-AnalysisResult "  ... and $($violations.Count - 3) more similar violations" -Type Finding
            }

            Write-AnalysisResult "" -Type Info
        }
    }

    # Generate summary report
    Write-AnalysisResult "=== REFACTORING PRIORITY SUMMARY ===" -Type Summary
    Write-AnalysisResult "1. Variable Output ($($categories.VariableOutput.Count)) - Easy automated fixes" -Type Summary
    Write-AnalysisResult "2. Complex Expressions ($($categories.ComplexExpressions.Count)) - Medium complexity" -Type Summary
    Write-AnalysisResult "3. Conditional Output ($($categories.ConditionalOutput.Count)) - Requires restructuring" -Type Summary
    Write-AnalysisResult "4. Loop Output ($($categories.LoopOutput.Count)) - Consider Write-Progress" -Type Summary
    Write-AnalysisResult "5. Multiline Output ($($categories.MultilineOutput.Count)) - Requires splitting" -Type Summary
    Write-AnalysisResult "6. Unknown Patterns ($($categories.UnknownPatterns.Count)) - Manual review needed" -Type Summary

    # Export detailed report
    $reportPath = "Documentation\Write-Host-Analysis-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $categories | ConvertTo-Json -Depth 3 | Set-Content $reportPath
    Write-AnalysisResult "Detailed analysis exported to: $reportPath" -Type Info

}
catch {
    Write-AnalysisResult "Analysis failed: $($_.Exception.Message)" -Type Info
    throw
}
