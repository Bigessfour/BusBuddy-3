# 🚌 BusBuddy PSScriptAnalyzer Settings
# Enhanced PowerShell 7.5.2 compliance and anti-pattern prevention
# Reference: https://docs.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/

@{
    # Include default rules
    IncludeDefaultRules = $true
    
    # Severity levels to enforce
    Severity = @('Error', 'Warning', 'Information')
    
    # Enhanced rules for PowerShell 7.5.2 compliance
    IncludeRules = @(
        # Output stream enforcement
        'PSAvoidUsingWriteHost',                    # ❌ Prevent Write-Host usage
        'PSUseDeclaredVarsMoreThanAssignments',     # ✅ Ensure variables are used
        'PSUseCorrectCasing',                       # ✅ Enforce proper PowerShell casing
        'PSUseSingularNouns',                       # ✅ Function naming standards
        'PSUseApprovedVerbs',                       # ✅ Approved PowerShell verbs
        
        # Parameter and validation enforcement
        'PSUseConsistentIndentation',               # ✅ Consistent 4-space indentation
        'PSUseConsistentWhitespace',                # ✅ Whitespace consistency
        'PSUseShouldProcessForStateChangingFunctions', # ✅ WhatIf/Confirm support
        'PSProvideCommentHelp',                     # ✅ Help documentation
        
        # Security and best practices
        'PSAvoidUsingConvertToSecureStringWithPlainText', # 🔒 Security enforcement
        'PSUsePSCredentialType',                    # 🔒 Credential handling
        'PSAvoidGlobalVars',                        # ✅ Avoid global variables
        'PSReservedCmdletChar',                     # ✅ Reserved character compliance
        'PSReservedParams',                         # ✅ Reserved parameter compliance
        
        # PowerShell 7.5.2 modern syntax
        'PSUseOutputTypeCorrectly',                 # ✅ Output type declarations
        'PSAvoidUsingPlainTextForPassword',         # 🔒 Password security
        'PSUseBOMForUnicodeEncodedFile'             # ✅ Unicode file encoding
    )
    
    # Rules to exclude (if any specific exceptions needed)
    ExcludeRules = @(
        # Temporarily exclude if needed for legacy compatibility
    )
    
    # Custom rule configurations
    Rules = @{
        # Write-Host prevention with detailed messaging
        PSAvoidUsingWriteHost = @{
            Enable = $true
        }
        
        # Consistent indentation (4 spaces, matching .editorconfig)
        PSUseConsistentIndentation = @{
            Enable = $true
            IndentationSize = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            Kind = 'space'
        }
        
        # Consistent whitespace (matching .editorconfig)
        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckPipe = $true
            CheckPipeForRedundantWhitespace = $true
            CheckSeparator = $true
            CheckParameter = $false
        }
        
        # Comment-based help enforcement
        PSProvideCommentHelp = @{
            Enable = $true
            ExportedOnly = $false
            BlockComment = $true
            VSCodeSnippetCorrection = $true
            Placement = 'before'
        }
        
        # Correct casing for PowerShell elements
        PSUseCorrectCasing = @{
            Enable = $true
        }
    }
}
