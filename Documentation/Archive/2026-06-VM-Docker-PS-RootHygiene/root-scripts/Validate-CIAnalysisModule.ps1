# BusBuddy-CIAnalysis-Enhanced Module Validation Script
# Validates PowerShell module syntax, structure, and compliance

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ModulePath = "Powershell\Modules\BusBuddy-CIAnalysis-Enhanced.psm1"
)

Write-Information "🔍 Validating BusBuddy-CIAnalysis-Enhanced.psm1..." -InformationAction Continue
Write-Information "=================================================" -InformationAction Continue

$validationResults = @{
    SyntaxValid = $false
    FunctionCount = 0
    ExportsFound = $false
    RequiresVersion = $null
    ModuleInfo = $null
    Errors = @()
}

try {
    # Check if file exists
    if (-not (Test-Path $ModulePath)) {
        throw "Module file not found: $ModulePath"
    }

    # Read module content
    $moduleContent = Get-Content $ModulePath -Raw
    if ([string]::IsNullOrEmpty($moduleContent)) {
        throw "Module file is empty or could not be read"
    }

    Write-Information "✅ File exists and readable" -InformationAction Continue

    # Test PowerShell syntax using AST parsing
    $parseErrors = $null
    $tokens = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput(
        $moduleContent,
        [ref]$tokens,
        [ref]$parseErrors
    )

    if ($parseErrors.Count -gt 0) {
        $validationResults.Errors += $parseErrors | ForEach-Object { $_.Message }
        throw "PowerShell syntax errors found: $($parseErrors.Count) errors"
    }

    $validationResults.SyntaxValid = $true
    Write-Information "✅ PowerShell syntax validation: PASSED" -InformationAction Continue

    # Count functions using AST
    $functionDefinitions = $ast.FindAll({
        $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst]
    }, $true)

    $validationResults.FunctionCount = $functionDefinitions.Count
    Write-Information "✅ Functions found: $($validationResults.FunctionCount)" -InformationAction Continue

    # List function names
    $functionNames = $functionDefinitions | ForEach-Object { $_.Name }
    Write-Information "📋 Function names:" -InformationAction Continue
    foreach ($funcName in $functionNames) {
        Write-Information "   • $funcName" -InformationAction Continue
    }

    # Check for Export-ModuleMember
    if ($moduleContent -match 'Export-ModuleMember') {
        $validationResults.ExportsFound = $true
        Write-Information "✅ Export-ModuleMember declaration: FOUND" -InformationAction Continue
    } else {
        Write-Warning "⚠️ Export-ModuleMember declaration: NOT FOUND"
    }

    # Check PowerShell version requirement
    if ($moduleContent -match '#requires -Version (\d+\.?\d*)') {
        $validationResults.RequiresVersion = $Matches[1]
        Write-Information "✅ PowerShell version requirement: $($validationResults.RequiresVersion)" -InformationAction Continue
    }

    # Extract module metadata
    if ($moduleContent -match '\$ModuleInfo\s*=\s*@{([^}]+)}') {
        Write-Information "✅ Module metadata found" -InformationAction Continue

        # Parse module info block
        if ($moduleContent -match "Name\s*=\s*'([^']+)'") {
            Write-Information "   Name: $($Matches[1])" -InformationAction Continue
        }
        if ($moduleContent -match "Version\s*=\s*'([^']+)'") {
            Write-Information "   Version: $($Matches[1])" -InformationAction Continue
        }
        if ($moduleContent -match "Description\s*=\s*'([^']+)'") {
            Write-Information "   Description: $($Matches[1])" -InformationAction Continue
        }
    }

    # Validate specific BusBuddy CI Analysis functions
    $expectedFunctions = @(
        'Invoke-EnhancedCIAnalysis',
        'Get-GitHubActionsData',
        'Get-LocalBuildArtifact',
        'Invoke-SynchronousCIAnalysis',
        'Invoke-EnhancedPatternAnalysis',
        'Show-EnhancedResult',
        'Start-AsyncCIAnalysis',
        'Show-AnalysisHelp'
    )

    $missingFunctions = @()
    foreach ($expectedFunc in $expectedFunctions) {
        if ($functionNames -notcontains $expectedFunc) {
            $missingFunctions += $expectedFunc
        }
    }

    if ($missingFunctions.Count -eq 0) {
        Write-Information "✅ All expected functions present" -InformationAction Continue
    } else {
        Write-Warning "⚠️ Missing expected functions:"
        foreach ($missing in $missingFunctions) {
            Write-Warning "   • $missing"
        }
    }

    # Check for proper CmdletBinding usage
    $functionsWithoutCmdletBinding = @()
    foreach ($funcDef in $functionDefinitions) {
        $funcText = $moduleContent.Substring($funcDef.Extent.StartOffset, $funcDef.Extent.EndOffset - $funcDef.Extent.StartOffset)
        if ($funcText -notmatch '\[CmdletBinding\(\)\]') {
            $functionsWithoutCmdletBinding += $funcDef.Name
        }
    }

    if ($functionsWithoutCmdletBinding.Count -eq 0) {
        Write-Information "✅ All functions use [CmdletBinding()]" -InformationAction Continue
    } else {
        Write-Warning "⚠️ Functions missing [CmdletBinding()]:"
        foreach ($funcName in $functionsWithoutCmdletBinding) {
            Write-Warning "   • $funcName"
        }
    }

    # Validate aliases
    $aliasPattern = 'New-Alias\s+-Name\s+[''"]([^''"]+)[''"]'
    $aliases = [regex]::Matches($moduleContent, $aliasPattern) | ForEach-Object { $_.Groups[1].Value }

    if ($aliases.Count -gt 0) {
        Write-Information "✅ Aliases found: $($aliases.Count)" -InformationAction Continue
        foreach ($alias in $aliases) {
            Write-Information "   • $alias" -InformationAction Continue
        }
    }

    Write-Information "" -InformationAction Continue
    Write-Information "🎉 Module validation completed successfully!" -InformationAction Continue
    Write-Information "✅ Syntax: Valid" -InformationAction Continue
    Write-Information "✅ Functions: $($validationResults.FunctionCount)" -InformationAction Continue
    Write-Information "✅ Exports: $(if($validationResults.ExportsFound){'Found'}else{'Missing'})" -InformationAction Continue
    Write-Information "✅ Aliases: $($aliases.Count)" -InformationAction Continue

    return $validationResults

} catch {
    $validationResults.Errors += $_.Exception.Message
    Write-Error "❌ Validation failed: $($_.Exception.Message)"
    Write-Information "📋 Validation Results:" -InformationAction Continue
    Write-Information "   Syntax Valid: $($validationResults.SyntaxValid)" -InformationAction Continue
    Write-Information "   Function Count: $($validationResults.FunctionCount)" -InformationAction Continue
    Write-Information "   Exports Found: $($validationResults.ExportsFound)" -InformationAction Continue

    if ($validationResults.Errors.Count -gt 0) {
        Write-Information "   Errors:" -InformationAction Continue
        foreach ($error in $validationResults.Errors) {
            Write-Information "     • $error" -InformationAction Continue
        }
    }

    return $validationResults
}
