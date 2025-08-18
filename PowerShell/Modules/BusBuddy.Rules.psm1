#Requires -Version 7.5

<#
.SYNOPSIS
    Custom PSScriptAnalyzer rules for BusBuddy PowerShell standards

.DESCRIPTION
    Enforces BusBuddy-specific PowerShell coding standards and best practices.
    Extends PSScriptAnalyzer with custom rules for consistent code quality.

.NOTES
    File Name      : BusBuddy-PowerShell.psm1
    Author         : BusBuddy Development Team
    Prerequisite   : PowerShell 7.5.2, PSScriptAnalyzer
    Copyright 2025 - BusBuddy
#>

function Test-BusBuddyBBPrefix {
    <#
    .SYNOPSIS
        Enforce bb- prefix for BusBuddy-specific functions

    .DESCRIPTION
        Custom rule to ensure BusBuddy functions follow the bb- naming convention
    #>
    param (
        [System.Management.Automation.Language.Ast]$ast
    )

    $results = @()

    # Find function definitions
    $functionDefinitions = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst]
        }, $true)

    foreach ($function in $functionDefinitions) {
        $functionName = $function.Name

        # Check if it's a BusBuddy function that should have bb- prefix
        if ($functionName -like "*BusBuddy*" -and $functionName -notlike "bb-*" -and $functionName -notlike "*-BusBuddy*") {
            $result = [Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord]@{
                Message  = "BusBuddy function '$functionName' should have a corresponding bb- alias"
                Extent   = $function.Extent
                RuleName = 'BusBuddyUseBBPrefix'
                Severity = 'Warning'
            }
            $results += $result
        }
    }

    return $results
}

function Test-BusBuddyErrorActionPreference {
    <#
    .SYNOPSIS
        Enforce ErrorActionPreference in scripts

    .DESCRIPTION
        Ensures that scripts have proper error handling by checking for ErrorActionPreference
    #>
    param (
        [System.Management.Automation.Language.Ast]$ast
    )

    $results = @()

    # Check if script sets ErrorActionPreference
    $assignments = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.AssignmentStatementAst]
        }, $true)

    $hasErrorActionPreference = $false
    foreach ($assignment in $assignments) {
        if ($assignment.Left.VariablePath.UserPath -eq 'ErrorActionPreference') {
            $hasErrorActionPreference = $true
            break
        }
    }

    # Also check for try-catch blocks as alternative error handling
    $tryCatchBlocks = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.TryStatementAst]
        }, $true)

    if (-not $hasErrorActionPreference -and $tryCatchBlocks.Count -eq 0) {
        $result = [Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord]@{
            Message  = "Script should set `$ErrorActionPreference or use try-catch for error handling"
            Extent   = $ast.Extent
            RuleName = 'BusBuddyRequireErrorActionPreference'
            Severity = 'Information'
        }
        $results += $result
    }

    return $results
}

function Test-BusBuddyNoHardcodedPath {
    <#
    .SYNOPSIS
        Avoid hardcoded paths in favor of dynamic path resolution

    .DESCRIPTION
        Detects hardcoded Windows paths and suggests using Join-Path or $PSScriptRoot
    #>
    param (
        [System.Management.Automation.Language.Ast]$ast
    )

    $results = @()

    # Find string constants that look like Windows paths
    $stringConstants = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.StringConstantExpressionAst]
        }, $true)

    foreach ($string in $stringConstants) {
        $value = $string.Value

        # Check for hardcoded Windows paths (C:\, \, absolute paths)
        if ($value -match '^[A-Za-z]:\\' -or $value -match '^\\\\' -or $value -match '\\Users\\') {
            $result = [Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord]@{
                Message  = "Avoid hardcoded path '$value'. Use Join-Path, `$PSScriptRoot, or relative paths"
                Extent   = $string.Extent
                RuleName = 'BusBuddyAvoidHardcodedPaths'
                Severity = 'Warning'
            }
            $results += $result
        }
    }

    return $results
}

function Test-BusBuddyWriteHostUsage {
    <#
    .SYNOPSIS
        Suggest using Write-Information or Write-Verbose instead of Write-Host

    .DESCRIPTION
        Detects excessive use of Write-Host and suggests alternatives for better PowerShell practices
    #>
    param (
        [System.Management.Automation.Language.Ast]$ast
    )

    $results = @()

    # Find Write-Host command calls
    $writeHostCalls = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -eq 'Write-Host'
        }, $true)

    if ($writeHostCalls.Count -gt 10) {
        $result = [Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord]@{
            Message  = "Consider using Write-Information, Write-Verbose, or Write-Debug instead of excessive Write-Host calls ($($writeHostCalls.Count) found)"
            Extent   = $writeHostCalls[0].Extent
            RuleName = 'BusBuddyUseWriteHostSparingly'
            Severity = 'Information'
        }
        $results += $result
    }

    return $results
}

function Test-BusBuddyVersion75 {
    <#
    .SYNOPSIS
        Ensure scripts require PowerShell 7.5

    .DESCRIPTION
        Checks for #Requires -Version 7.5 directive in BusBuddy scripts
    #>
    param (
        [System.Management.Automation.Language.Ast]$ast
    )

    $results = @()

    # Check for #Requires directive
    $requiresStatements = $ast.ScriptRequirements

    $hasCorrectVersion = $false
    if ($requiresStatements -and $requiresStatements.RequiredPSVersion) {
        $version = $requiresStatements.RequiredPSVersion
        if ($version.Major -ge 7 -and $version.Minor -ge 5) {
            $hasCorrectVersion = $true
        }
    }

    if (-not $hasCorrectVersion) {
        $result = [Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord]@{
            Message  = "BusBuddy scripts should include '#Requires -Version 7.5' directive"
            Extent   = $ast.Extent
            RuleName = 'BusBuddyRequireVersion75'
            Severity = 'Warning'
        }
        $results += $result
    }

    return $results
}

# Export the custom rules
Export-ModuleMember -Function Test-BusBuddyBBPrefix, Test-BusBuddyErrorActionPreference, Test-BusBuddyNoHardcodedPaths, Test-BusBuddyWriteHostUsage, Test-BusBuddyVersion75
