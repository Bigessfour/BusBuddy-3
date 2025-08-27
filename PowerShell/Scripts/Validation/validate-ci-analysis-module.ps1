# Enhanced BusBuddy-CIAnalysis-Enhanced Module Validation Script
# Properly declares all variables following PowerShell best practices

[CmdletBinding()]
param()

# Check if validation is disabled via environment variable
if ($env:BUSBUDDY_DISABLE_VALIDATION -eq '1' -or $env:BUSBUDDY_SKIP_AST_VALIDATION -eq '1') {
    Write-Information "ℹ️ Module validation disabled via environment variable" -InformationAction Continue
    exit 0
}

Write-Information "🔍 Validating BusBuddy-CIAnalysis-Enhanced.psm1..." -InformationAction Continue

try {
    # Initialize all variables at the beginning
    $modulePath = 'Powershell\Modules\BusBuddy-CIAnalysis-Enhanced.psm1'
    $content = $null
    $functionCount = 0
    $validationResults = @()

    # Test 1: Check if file exists
    if (-not (Test-Path $modulePath)) {
        throw "Module file not found at: $modulePath"
    }
    $validationResults += "✅ Module file exists"

    # Test 2: Read and validate content
    $content = Get-Content $modulePath -Raw -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($content)) {
        throw "Module file is empty or unreadable"
    }
    $validationResults += "✅ Module content readable"

    # Test 3: Basic PowerShell syntax validation using AST
    $errors = $null
    $tokens = $null
    [System.Management.Automation.Language.Parser]::ParseInput($content, [ref]$tokens, [ref]$errors) | Out-Null

    if ($errors.Count -eq 0) {
        $validationResults += "✅ PowerShell syntax validation: PASSED"
    } else {
        $validationResults += "❌ PowerShell syntax errors: $($errors.Count)"
        $errors | ForEach-Object {
            $validationResults += "   Error: $($_.Message) (Line: $($_.Extent.StartLineNumber))"
        }
    }

    # Test 4: Count functions
    $functionMatches = [regex]::Matches($content, 'function\s+[\w-]+')
    $functionCount = $functionMatches.Count
    $validationResults += "✅ Functions found: $functionCount"

    # Test 5: List function names
    if ($functionCount -gt 0) {
        $validationResults += "📋 Function names:"
        foreach ($match in $functionMatches) {
            $functionName = ($match.Value -replace 'function\s+', '').Trim()
            $validationResults += "   • $functionName"
        }
    }

    # Test 6: Check for Export-ModuleMember
    if ($content -match 'Export-ModuleMember') {
        $validationResults += "✅ Export-ModuleMember declaration: FOUND"
    } else {
        $validationResults += "⚠️ Export-ModuleMember declaration: MISSING"
    }

    # Test 7: Check for required attributes
    $cmdletBindingCount = ([regex]::Matches($content, '\[CmdletBinding\(\)\]')).Count
    $validationResults += "✅ [CmdletBinding()] attributes: $cmdletBindingCount"

    # Test 8: Check for aliases
    if ($content -match 'New-Alias') {
        $aliasMatches = [regex]::Matches($content, "New-Alias.*-Name\s+'([^']+)'")
        $validationResults += "✅ Aliases defined: $($aliasMatches.Count)"
        foreach ($aliasMatch in $aliasMatches) {
            $aliasName = $aliasMatch.Groups[1].Value
            $validationResults += "   • $aliasName"
        }
    }

    # Test 9: Module metadata check
    if ($content -match '\$ModuleInfo\s*=\s*@{') {
        $validationResults += "✅ Module metadata: FOUND"
    } else {
        $validationResults += "⚠️ Module metadata: MISSING"
    }

    # Display all validation results
    Write-Information "" -InformationAction Continue
    Write-Information "📊 Validation Results:" -InformationAction Continue
    Write-Information "======================" -InformationAction Continue
    foreach ($result in $validationResults) {
        Write-Information $result -InformationAction Continue
    }

    # Summary
    $passedTests = ($validationResults | Where-Object { $_ -match "^✅" }).Count
    $totalTests = ($validationResults | Where-Object { $_ -match "^[✅❌⚠️]" }).Count

    Write-Information "" -InformationAction Continue
    Write-Information "📈 Summary: $passedTests/$totalTests tests passed" -InformationAction Continue

    if ($errors.Count -eq 0) {
        Write-Information "🎉 Module validation completed successfully!" -InformationAction Continue
        return $true
    } else {
        Write-Warning "⚠️ Module has syntax errors that need to be addressed"
        return $false
    }

} catch {
    Write-Error "❌ Validation failed: $($_.Exception.Message)"
    return $false
} finally {
    # Clean up variables
    Remove-Variable -Name content, functionCount, validationResults -ErrorAction SilentlyContinue
}
