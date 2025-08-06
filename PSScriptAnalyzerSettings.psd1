# ========================================
# PSScriptAnalyzer Settings for BusBuddy
# PowerShell 7.5.2+ Compatible Configuration
# ========================================

@{
    # Include all default rules for comprehensive analysis
    IncludeDefaultRules = $true

    # Exclude rules that cause false positives in PS7+ or don't fit BusBuddy workflow
    ExcludeRules        = @(
        'PSUseDeclaredVarsMoreThanAssignments',     # False positives with globals/modules
        'PSAvoidUsingPositionalParameters',         # Allow modern parameter flexibility
        'PSUseShouldProcessForStateChangingFunctions',  # Not needed for all functions
        'PSAvoidUsingWriteHost',                    # Allow Write-Host for interactive scripts and tests
        'PSAvoidUsingPlainTextForPassword',        # Allow for development/testing scenarios
        'PSAvoidDefaultValueSwitchParameter',      # Allow modern switch parameter patterns
        'PSUseBOMForUnicodeEncodedFile'            # Allow Unicode emojis without BOM requirement
    )

    # Include compatibility rules for cross-version support if needed
    IncludeRules        = @(
        # Don't include PSUseCompatibleSyntax as it may flag modern PS 7.5.2 features
        # 'PSUseCompatibleSyntax',
        # 'PSUseCompatibleCommands'
    )

    # Report both Errors (critical) and Warnings (recommended fixes)
    # Include 'Information' if verbose output is desired
    Severity            = @('Error', 'Warning')

    # Path to custom rules (uncomment and add paths as needed)
    # CustomRulePath = @('Tools\Scripts\CustomPSScriptAnalyzerRules.psm1')

    # Rule-specific configurations
    Rules               = @{
        # Allow modern PowerShell 7+ operators that might be flagged as aliases
        PSAvoidUsingCmdletAliases = @{
            AllowList = @('?', '??', '&&', '||', '?.', '?[]')
        }

        # Configure compatibility settings for PowerShell 7.5.2+
        PSUseCompatibleSyntax     = @{
            # Target PowerShell 7.5.2+ exclusively - don't check older versions
            Enable = $false
        }

        # Allow modern syntax patterns
        PSUseCompatibleCommands   = @{
            Enable = $false
        }

        # Configure parameter binding to allow modern patterns
        PSReviewUnusedParameter   = @{
            # Allow parameters that might be used in advanced scenarios
            CommandsToTraverse = @('function', 'filter', 'workflow')
        }
    }
}
