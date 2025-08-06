# ========================================
# PowerShell 7.5.2 Syntax Compatibility Wrapper
# Addresses VS Code / PSScriptAnalyzer parsing issues
# ========================================

# This file contains PowerShell 7.5.2 syntax in a form that won't trigger
# PowerShell 5.1 parser errors in analysis tools

# ========================================
# COMPATIBILITY FUNCTIONS FOR MODERN SYNTAX
# ========================================

function Invoke-TernaryOperator {
    <#
    .SYNOPSIS
    Implements ternary operator functionality for PowerShell 7.5.2 compatibility

    .DESCRIPTION
    Provides ternary operator (?:) functionality in a way that's compatible with
    both PowerShell 5.1 analysis tools and PowerShell 7.5.2 execution

    .PARAMETER Condition
    The condition to evaluate

    .PARAMETER TrueValue
    Value to return if condition is true

    .PARAMETER FalseValue
    Value to return if condition is false

    .EXAMPLE
    $result = Invoke-TernaryOperator -Condition ($x -gt 5) -TrueValue "High" -FalseValue "Low"
    # Equivalent to: $result = $x -gt 5 ? "High" : "Low"
    #>
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [object]$TrueValue,

        [Parameter(Mandatory)]
        [object]$FalseValue
    )

    if ($Condition) { return $TrueValue } else { return $FalseValue }
}

function Invoke-NullCoalescing {
    <#
    .SYNOPSIS
    Implements null coalescing operator functionality for PowerShell 7.5.2 compatibility

    .DESCRIPTION
    Provides null coalescing operator (??) functionality in a way that's compatible with
    both PowerShell 5.1 analysis tools and PowerShell 7.5.2 execution

    .PARAMETER PrimaryValue
    The primary value to check for null

    .PARAMETER FallbackValue
    Value to return if primary value is null

    .EXAMPLE
    $result = Invoke-NullCoalescing -PrimaryValue $maybeNull -FallbackValue "Default"
    # Equivalent to: $result = $maybeNull ?? "Default"
    #>
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$PrimaryValue,

        [Parameter(Mandatory)]
        [object]$FallbackValue
    )

    if ($null -ne $PrimaryValue) { return $PrimaryValue } else { return $FallbackValue }
}

function Invoke-PipelineChain {
    <#
    .SYNOPSIS
    Implements pipeline chain operators for PowerShell 7.5.2 compatibility

    .DESCRIPTION
    Provides pipeline chain operator (&&, ||) functionality in a way that's compatible with
    both PowerShell 5.1 analysis tools and PowerShell 7.5.2 execution

    .PARAMETER FirstCommand
    The first command to execute

    .PARAMETER SecondCommand
    The second command to execute

    .PARAMETER Operator
    The operator type: 'And' (&&) or 'Or' (||)

    .EXAMPLE
    Invoke-PipelineChain -FirstCommand { Test-Path $file } -SecondCommand { Write-Host "File exists" } -Operator "And"
    # Equivalent to: Test-Path $file && Write-Host "File exists"
    #>
    param(
        [Parameter(Mandatory)]
        [scriptblock]$FirstCommand,

        [Parameter(Mandatory)]
        [scriptblock]$SecondCommand,

        [Parameter(Mandatory)]
        [ValidateSet('And', 'Or')]
        [string]$Operator
    )

    $firstResult = & $FirstCommand
    $firstSuccess = $?

    switch ($Operator) {
        'And' {
            if ($firstSuccess) {
                & $SecondCommand
            }
            else {
                return $firstResult
            }
        }
        'Or' {
            if (-not $firstSuccess) {
                & $SecondCommand
            }
            else {
                return $firstResult
            }
        }
    }
}

function Get-SafePropertyValue {
    <#
    .SYNOPSIS
    Implements null conditional operator functionality for PowerShell 7.5.2 compatibility

    .DESCRIPTION
    Provides null conditional operator (?.) functionality in a way that's compatible with
    both PowerShell 5.1 analysis tools and PowerShell 7.5.2 execution

    .PARAMETER Object
    The object to access property on

    .PARAMETER PropertyName
    The property name to access

    .EXAMPLE
    $result = Get-SafePropertyValue -Object $command -PropertyName "Source"
    # Equivalent to: $result = $command?.Source
    #>
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory)]
        [string]$PropertyName
    )

    if ($null -ne $Object -and (Get-Member -InputObject $Object -Name $PropertyName -ErrorAction SilentlyContinue)) {
        return $Object.$PropertyName
    }
    return $null
}

# ========================================
# MODERN SYNTAX DETECTION AND EXECUTION
# ========================================

function Test-ModernPowerShellSyntax {
    <#
    .SYNOPSIS
    Tests if the current PowerShell session supports modern syntax

    .DESCRIPTION
    Safely tests modern PowerShell 7.5.2 syntax features without causing parse errors
    in analysis tools that use PowerShell 5.1 parsing

    .OUTPUTS
    Hashtable with test results for each modern syntax feature

    .EXAMPLE
    $syntaxSupport = Test-ModernPowerShellSyntax
    if ($syntaxSupport.TernaryOperator) {
        # Use modern syntax
    } else {
        # Use compatibility functions
    }
    #>

    $results = @{
        TernaryOperator     = $false
        NullCoalescing      = $false
        PipelineChains      = $false
        NullConditional     = $false
        PSVersion           = $PSVersionTable.PSVersion
        ModernSyntaxSupport = $false
    }

    # Test ternary operator
    try {
        $testScript = '$true ? "works" : "failed"'
        $null = [scriptblock]::Create($testScript)
        $results.TernaryOperator = $true
    }
    catch {
        $results.TernaryOperator = $false
    }

    # Test null coalescing
    try {
        $testScript = '$null ?? "default"'
        $null = [scriptblock]::Create($testScript)
        $results.NullCoalescing = $true
    }
    catch {
        $results.NullCoalescing = $false
    }

    # Test pipeline chains
    try {
        $testScript = '$true && "success"'
        $null = [scriptblock]::Create($testScript)
        $results.PipelineChains = $true
    }
    catch {
        $results.PipelineChains = $false
    }

    # Test null conditional
    try {
        $testScript = '$null?.Property'
        $null = [scriptblock]::Create($testScript)
        $results.NullConditional = $true
    }
    catch {
        $results.NullConditional = $false
    }

    # Overall modern syntax support
    $results.ModernSyntaxSupport = $results.TernaryOperator -and
    $results.NullCoalescing -and
    $results.PipelineChains -and
    $results.NullConditional

    return $results
}

# ========================================
# USAGE EXAMPLES AND BEST PRACTICES
# ========================================

<#
.EXAMPLE
# Instead of using modern syntax directly (which may cause parse errors):
$result = $condition ? "true" : "false"  # May cause parse error in PS5.1 analysis

# Use compatibility approach:
if ($PSVersionTable.PSVersion.Major -ge 7) {
    # Runtime check for PowerShell 7+
    $result = Invoke-Expression '$condition ? "true" : "false"'
} else {
    $result = Invoke-TernaryOperator -Condition $condition -TrueValue "true" -FalseValue "false"
}

.EXAMPLE
# Instead of:
$value = $maybeNull ?? "default"  # May cause parse error

# Use:
$value = Invoke-NullCoalescing -PrimaryValue $maybeNull -FallbackValue "default"

.EXAMPLE
# Instead of:
Test-Path $file && Write-Host "File exists"  # May cause parse error

# Use:
Invoke-PipelineChain -FirstCommand { Test-Path $file } -SecondCommand { Write-Host "File exists" } -Operator "And"
#>

# ========================================
# EXPORT MODULE MEMBERS
# ========================================

# Export functions for use in other scripts
Export-ModuleMember -Function @(
    'Invoke-TernaryOperator',
    'Invoke-NullCoalescing',
    'Invoke-PipelineChain',
    'Get-SafePropertyValue',
    'Test-ModernPowerShellSyntax'
)
