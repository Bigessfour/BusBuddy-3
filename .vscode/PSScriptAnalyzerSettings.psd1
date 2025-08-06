# PSScriptAnalyzer Settings for BusBuddy - PowerShell 7.5.2 Standards
# Enforces approved verb patterns and Bus Buddy coding standards

@{
    # Include default PowerShell best practice rules
    IncludeDefaultRules = $true

    # Severity levels: Error, Warning, Information
    Severity            = @('Error', 'Warning', 'Information')

    # Rules configuration
    Rules               = @{
        # CRITICAL: PowerShell approved verbs enforcement
        PSUseApprovedVerbs                             = @{
            Enable   = $true
            Severity = 'Error'
        }

        # Function naming standards
        PSUseSingularNouns                             = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # Reserved parameter enforcement
        PSReservedCmdletChar                           = @{
            Enable   = $true
            Severity = 'Error'
        }

        PSReservedParams                               = @{
            Enable   = $true
            Severity = 'Error'
        }

        # Code quality rules
        PSAvoidUsingCmdletAliases                      = @{
            Enable   = $true
            Severity = 'Warning'
        }

        PSUseDeclaredVarsMoreThanAssignments           = @{
            Enable   = $true
            Severity = 'Warning'
        }

        PSAvoidGlobalVars                              = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # Security rules
        PSAvoidUsingPlainTextForPassword               = @{
            Enable   = $true
            Severity = 'Error'
        }

        PSAvoidUsingConvertToSecureStringWithPlainText = @{
            Enable   = $true
            Severity = 'Error'
        }

        PSUsePSCredentialType                          = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # Best practices
        PSUseShouldProcessForStateChangingFunctions    = @{
            Enable   = $true
            Severity = 'Warning'
        }

        PSAvoidUsingInvokeExpression                   = @{
            Enable   = $true
            Severity = 'Warning'
        }

        PSUseCmdletCorrectly                           = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # Documentation
        PSProvideCommentHelp                           = @{
            Enable   = $true
            Severity = 'Information'
        }
    }

    # Exclude paths
    ExcludeRules        = @()

    IncludeRules        = @(
        'PSUseApprovedVerbs',
        'PSUseSingularNouns',
        'PSReservedCmdletChar',
        'PSReservedParams',
        'PSAvoidUsingCmdletAliases',
        'PSUseDeclaredVarsMoreThanAssignments',
        'PSAvoidGlobalVars',
        'PSAvoidUsingPlainTextForPassword',
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSUsePSCredentialType',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSAvoidUsingInvokeExpression',
        'PSUseCmdletCorrectly',
        'PSProvideCommentHelp'
    )
}
